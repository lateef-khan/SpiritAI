import { act, cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";

import type { HandoffSummary } from "@/api/types.gen";
import { reviveHistory } from "@/lib/history";
import { queryWrapper } from "@/test/query";

import { applyClaimed, applyMessage } from "../cache/handoffCache";
import { InboxScreen } from "./InboxScreen";

/**
 * The screen, against a mocked wire.
 *
 * `listHandoffs` and `countHandoffs` are mocked at the generated client, and the picked chat's
 * transcript arrives revived rather than over the wire: what is worth holding in place here is
 * that a row click carries the picked handoff from the panel into the chat pane, not the
 * network underneath. The host does the filtering and the ordering, so the mock answers by the
 * query it is sent. `@/hooks/use-mobile` is mocked separately per test, since `InboxScreen`
 * reads it to choose which of the two layouts to render.
 */
vi.mock("@/api/sdk.gen", () => ({
  listHandoffs: vi.fn(),
  countHandoffs: vi.fn(),
  claimHandoff: vi.fn(),
  markHandoffSeen: vi.fn(),
}));
vi.mock("@/hooks/use-mobile", () => ({ useIsMobile: vi.fn(() => false) }));
// No socket here: the screen's list and pick are what is under test, and the real hub would try
// to reach a host that does not exist.
vi.mock("@/lib/realtime/SocketProvider", () => ({
  useSocketEvents: () => ({ signal: async () => {} }),
}));

const { listHandoffs, countHandoffs, claimHandoff, markHandoffSeen } =
  await import("@/api/sdk.gen");
const { useIsMobile } = await import("@/hooks/use-mobile");

/** What the host would list for each query: open rows by ask, done rows by close. */
function hostWith(open: HandoffSummary[], done: HandoffSummary[] = []) {
  vi.mocked(listHandoffs).mockImplementation((options) => {
    const query = options?.query ?? {};
    const rows = query.view === "done" ? done : open;
    const kept =
      query.owner === "me"
        ? rows.filter((row) => row.assignee?.key === MeKey)
        : query.owner === "none"
          ? rows.filter((row) => row.assignee === null)
          : rows;
    const sorted = [...kept].sort((a, b) =>
      query.view === "done"
        ? (a.doneAt ?? "").localeCompare(b.doneAt ?? "")
        : a.askedAt.localeCompare(b.askedAt),
    );
    const items =
      (query.view === "done") !== (query.order === "oldest") ? sorted.reverse() : sorted;
    return Promise.resolve({ data: { items, nextCursor: null } }) as never;
  });
  vi.mocked(countHandoffs).mockImplementation((options) => {
    const rows = options?.query?.view === "done" ? done : open;
    return Promise.resolve({
      data: {
        mine: rows.filter((row) => row.assignee?.key === MeKey).length,
        unassigned: rows.filter((row) => row.assignee === null).length,
        all: rows.length,
        awaitingReply: 0,
      },
    }) as never;
  });
}

const MeKey = "user:dana";

function wire(over: Partial<HandoffSummary> = {}): HandoffSummary {
  return {
    id: 1,
    callId: "call-1",
    status: "waiting",
    askedBy: "bot",
    reason: null,
    askedAt: "2026-09-12T12:38:00",
    assignee: null,
    claimedAt: null,
    email: null,
    doneAt: null,
    title: "Treadmill belt slips at 8 mph",
    firstLine: "I already did that twice.",
    position: 1,
    awaitingReply: false,
    unread: false,
    ...over,
  };
}

const Transcript = {
  headId: "call-1:0",
  messages: [
    {
      parentId: null,
      message: {
        id: "call-1:0",
        role: "user",
        content: [{ type: "text", text: "I already did that twice." }],
        attachments: [],
        createdAt: "2026-09-12T12:38:00",
        metadata: { custom: {} },
      },
    },
  ],
};

function screenWithTranscript() {
  const { client, wrapper } = queryWrapper();
  render(
    <InboxScreen
      meKey={MeKey}
      transcript={{
        history: reviveHistory(Transcript as never),
        loading: false,
        error: null,
        reload: () => {},
      }}
      onSelectionChange={() => {}}
    />,
    { wrapper },
  );
  return client;
}

afterEach(() => {
  cleanup();
  vi.useRealTimers();
  vi.mocked(useIsMobile).mockReturnValue(false);
});

describe("InboxScreen", () => {
  it("shows the picked row's chat once its title is clicked", async () => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date("2026-09-12T12:53:00"));

    hostWith([wire()]);

    screenWithTranscript();

    await act(() => vi.advanceTimersByTimeAsync(0));

    fireEvent.click(screen.getByText("Treadmill belt slips at 8 mph"));

    await act(() => vi.advanceTimersByTimeAsync(0));

    expect(screen.getByText("Started 15 min ago")).toBeTruthy();
  });

  it("takes the dot off a picked row and tells the host, and puts it back when the visitor speaks", async () => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date("2026-09-12T12:53:00"));

    hostWith([wire({ unread: true })]);
    vi.mocked(markHandoffSeen).mockResolvedValue({ data: undefined } as never);

    const client = screenWithTranscript();

    await act(() => vi.advanceTimersByTimeAsync(0));

    expect(screen.getByRole("img", { name: "Unread" })).toBeTruthy();

    fireEvent.click(screen.getByText("Treadmill belt slips at 8 mph"));

    await act(() => vi.advanceTimersByTimeAsync(0));

    expect(vi.mocked(markHandoffSeen)).toHaveBeenCalledTimes(1);
    expect(screen.queryByRole("img", { name: "Unread" })).toBeNull();

    // The visitor speaks while the chat is on screen: the push is applied the way
    // `useHandoffPushes` would, and the screen marks the chat seen again.
    act(() =>
      applyMessage(
        client,
        {
          callId: "call-1",
          messageId: "call-1:1",
          role: "user",
          text: "hello?",
          speaker: null,
          at: "2026-09-12T12:54:00",
        },
        MeKey,
      ),
    );

    await act(() => vi.advanceTimersByTimeAsync(0));

    // The second mark is made from an effect the push's render ran, so its answer is one more
    // timer away than the push's.
    await act(() => vi.runOnlyPendingTimersAsync());

    expect(screen.queryByRole("img", { name: "Unread" })).toBeNull();
    expect(vi.mocked(markHandoffSeen)).toHaveBeenCalledTimes(2);
  });

  it("takes a waiting handoff, and the Mine count moves once, however often the claim is heard", async () => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date("2026-09-12T12:53:00"));

    const claimed = wire({ status: "human", assignee: { key: MeKey, name: "Dana" } });
    hostWith([wire()]);
    vi.mocked(claimHandoff).mockResolvedValue({ data: claimed } as never);

    const client = screenWithTranscript();

    await act(() => vi.advanceTimersByTimeAsync(0));

    fireEvent.click(screen.getByText("Treadmill belt slips at 8 mph"));

    await act(() => vi.advanceTimersByTimeAsync(0));

    await act(async () => {
      fireEvent.click(screen.getByRole("button", { name: "Take" }));
    });

    expect(screen.getByText("You have this chat")).toBeTruthy();
    expect(screen.getByRole("tab", { name: /Mine/ }).textContent).toContain("1");

    // The host tells every member of staff, the claimant included, over the socket; the screen
    // has no socket here, so the push is applied the way `useHandoffPushes` would.
    act(() => applyClaimed(client, { callId: "call-1", assignee: claimed.assignee! }, MeKey));

    expect(screen.getByRole("tab", { name: /Mine/ }).textContent).toContain("1");
    expect(screen.getByRole("tab", { name: /Unassigned/ }).textContent).toContain("0");
  });

  it("clears the pick when the view changes underneath it", async () => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date("2026-09-12T12:53:00"));

    hostWith([wire()]);

    screenWithTranscript();

    await act(() => vi.advanceTimersByTimeAsync(0));

    fireEvent.click(screen.getByText("Treadmill belt slips at 8 mph"));

    await act(() => vi.advanceTimersByTimeAsync(0));

    expect(screen.getByText("Started 15 min ago")).toBeTruthy();

    // Radix opens a dropdown on a key press as readily as on a pointer, and happy-dom has no
    // pointer.
    fireEvent.keyDown(screen.getByRole("button", { name: "Open" }), { key: "Enter" });
    await act(() => vi.advanceTimersByTimeAsync(0));
    fireEvent.click(screen.getByRole("menuitem", { name: "Done" }));

    expect(screen.getByText("Pick a conversation.")).toBeTruthy();
  });

  it("orders the done view newest first until the sort is toggled", async () => {
    const older = wire({
      id: 1,
      status: "done",
      doneAt: "2026-09-12T11:00:00",
      email: null,
      title: "Older, done first ended",
    });
    const newer = wire({
      id: 2,
      status: "done",
      doneAt: "2026-09-12T12:00:00",
      email: null,
      title: "Newer, done last ended",
    });

    hostWith([], [older, newer]);

    const { container } = render(
      <InboxScreen
        meKey={MeKey}
        transcript={{ history: null, loading: false, error: null, reload: () => {} }}
        onSelectionChange={() => {}}
      />,
      { wrapper: queryWrapper().wrapper },
    );

    await screen.findByText("No conversations.");

    // Radix opens a dropdown on a key press as readily as on a pointer, and happy-dom has no
    // pointer.
    fireEvent.keyDown(screen.getByRole("button", { name: "Open" }), { key: "Enter" });
    fireEvent.click(await screen.findByText("Done"));

    await screen.findByText("Newer, done last ended");

    const rowTitles = () =>
      Array.from(container.querySelectorAll("li")).map((li) => li.textContent);

    expect(rowTitles()[0]).toContain("Newer, done last ended");
    expect(rowTitles()[1]).toContain("Older, done first ended");
    expect(screen.getByRole("button", { name: "Newest first" })).toBeTruthy();

    fireEvent.click(screen.getByRole("button", { name: "Newest first" }));

    await screen.findByRole("button", { name: "Oldest first" });
    await waitFor(() => expect(rowTitles()[0]).toContain("Older, done first ended"));
    expect(rowTitles()[1]).toContain("Newer, done last ended");
    expect(screen.getByRole("button", { name: "Oldest first" })).toBeTruthy();
  });

  it("on mobile, a row click fills the width with the chat, and Back returns to the list", async () => {
    vi.mocked(useIsMobile).mockReturnValue(true);
    hostWith([wire()]);

    screenWithTranscript();

    await screen.findByText("Treadmill belt slips at 8 mph");

    fireEvent.click(screen.getByText("Treadmill belt slips at 8 mph"));

    await screen.findByRole("button", { name: "Back" });

    expect(screen.queryByText("Conversations")).toBeNull();

    fireEvent.click(screen.getByRole("button", { name: "Back" }));

    expect(await screen.findByText("Treadmill belt slips at 8 mph")).toBeTruthy();
  });
});
