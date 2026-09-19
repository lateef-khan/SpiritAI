import { act, cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";

import type { HandoffSummary } from "@/api/types.gen";
import { reviveHistory } from "@/lib/history";

import { InboxScreen } from "./InboxScreen";

/**
 * The screen, against a mocked wire.
 *
 * `listHandoffs` is mocked the same way `InboxPanel.test.tsx` mocks it, and the picked chat's
 * transcript arrives revived rather than over the wire: what is worth holding in place here is
 * that a row click carries the picked handoff from the panel into the chat pane, not the
 * network underneath. `@/hooks/use-mobile` is mocked separately per test, since `InboxScreen`
 * reads it to choose which of the two layouts to render.
 */
vi.mock("@/api/sdk.gen", () => ({
  listHandoffs: vi.fn(),
  claimHandoff: vi.fn(),
}));
vi.mock("@/hooks/use-mobile", () => ({ useIsMobile: vi.fn(() => false) }));
// No socket here: the screen's list and pick are what is under test, and the real hub would try
// to reach a host that does not exist.
vi.mock("@/lib/realtime/SocketProvider", () => ({
  useSocketEvents: () => ({ signal: async () => {} }),
}));

const { listHandoffs, claimHandoff } = await import("@/api/sdk.gen");
const { useIsMobile } = await import("@/hooks/use-mobile");

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
  );
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

    vi.mocked(listHandoffs).mockImplementation(
      (options) =>
        Promise.resolve({
          data: { items: options?.query?.status === "waiting" ? [wire()] : [] },
        }) as never,
    );

    screenWithTranscript();

    await act(() => vi.advanceTimersByTimeAsync(0));

    fireEvent.click(screen.getByText("Treadmill belt slips at 8 mph"));

    await act(() => vi.advanceTimersByTimeAsync(0));

    expect(screen.getByText("Started 15 min ago")).toBeTruthy();
  });

  it("takes a waiting handoff and reflects the claim in the badge and the Mine count", async () => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date("2026-09-12T12:53:00"));

    const claimed = wire({ status: "human", assignee: { key: MeKey, name: "Dana" } });
    let claimedFlag = false;
    vi.mocked(listHandoffs).mockImplementation((options) => {
      if (options?.query?.status === "waiting") {
        return Promise.resolve({ data: { items: claimedFlag ? [] : [wire()] } }) as never;
      }
      return Promise.resolve({
        data: { items: claimedFlag ? [claimed] : [] },
      }) as never;
    });
    vi.mocked(claimHandoff).mockImplementation(() => {
      claimedFlag = true;
      return Promise.resolve({ data: claimed }) as never;
    });

    screenWithTranscript();

    await act(() => vi.advanceTimersByTimeAsync(0));

    fireEvent.click(screen.getByText("Treadmill belt slips at 8 mph"));

    await act(() => vi.advanceTimersByTimeAsync(0));

    await act(async () => {
      fireEvent.click(screen.getByRole("button", { name: "Take" }));
    });

    expect(screen.getByText("You have this chat")).toBeTruthy();

    await act(() => vi.advanceTimersByTimeAsync(0));

    expect(screen.getByRole("tab", { name: /Mine/ }).textContent).toContain("1");
  });

  it("clears the pick when the view changes underneath it", async () => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date("2026-09-12T12:53:00"));

    vi.mocked(listHandoffs).mockImplementation(
      (options) =>
        Promise.resolve({
          data: { items: options?.query?.status === "waiting" ? [wire()] : [] },
        }) as never,
    );

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

    vi.mocked(listHandoffs).mockImplementation(
      (options) =>
        Promise.resolve({
          data: { items: options?.query?.status === "done" ? [older, newer] : [] },
        }) as never,
    );

    const { container } = render(
      <InboxScreen
        meKey={MeKey}
        transcript={{ history: null, loading: false, error: null, reload: () => {} }}
        onSelectionChange={() => {}}
      />,
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

    expect(rowTitles()[0]).toContain("Older, done first ended");
    expect(rowTitles()[1]).toContain("Newer, done last ended");
    expect(screen.getByRole("button", { name: "Oldest first" })).toBeTruthy();
  });

  it("on mobile, a row click fills the width with the chat, and Back returns to the list", async () => {
    vi.mocked(useIsMobile).mockReturnValue(true);
    vi.mocked(listHandoffs).mockImplementation(
      (options) =>
        Promise.resolve({
          data: { items: options?.query?.status === "waiting" ? [wire()] : [] },
        }) as never,
    );

    screenWithTranscript();

    await screen.findByText("Treadmill belt slips at 8 mph");

    fireEvent.click(screen.getByText("Treadmill belt slips at 8 mph"));

    await screen.findByRole("button", { name: "Back" });

    expect(screen.queryByText("Conversations")).toBeNull();

    fireEvent.click(screen.getByRole("button", { name: "Back" }));

    expect(await screen.findByText("Treadmill belt slips at 8 mph")).toBeTruthy();
  });
});
