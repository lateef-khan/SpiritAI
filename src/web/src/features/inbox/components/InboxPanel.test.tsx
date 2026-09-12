import { act, cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import type { HandoffSummary } from "@/api/types.gen";

/**
 * The panel, against a mocked wire.
 *
 * `@/api/sdk.gen` is mocked the same way `UnitPanel.test.tsx` and `handoffsApi.test.ts` mock it:
 * what is worth holding in place here is what one waiting row and one claimed row read as once
 * `createHandoffsApi` and `useHandoffs` have turned them into props, not the network underneath.
 */
vi.mock("@/api/sdk.gen", () => ({ listHandoffs: vi.fn() }));

const { listHandoffs } = await import("@/api/sdk.gen");
const { InboxPanel } = await import("./InboxPanel");

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
    email: "lorrie@northwind.example",
    doneAt: null,
    title: "Treadmill belt slips at 8 mph",
    firstLine: "I already did that twice.",
    position: 1,
    ...over,
  };
}

afterEach(() => {
  cleanup();
  vi.useRealTimers();
});

beforeEach(() => vi.resetAllMocks());

describe("InboxPanel", () => {
  it("shows a waiting row's wait time and position, a claimed row's assignee, and tab counts", async () => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date("2026-09-12T12:52:00"));

    const waiting = wire();
    const human = wire({
      id: 2,
      status: "human",
      assignee: { key: MeKey, name: "Dana" },
      email: "priya.n@example.com",
      title: "Bluetooth will not pair on XBR95",
      firstLine: "Try holding the button for 5 seconds.",
      position: null,
    });

    vi.mocked(listHandoffs).mockImplementation(
      (options) =>
        Promise.resolve({
          data: { items: options?.query?.status === "waiting" ? [waiting] : [human] },
        }) as never,
    );

    render(<InboxPanel meKey={MeKey} />);

    // The load effect resolves through real promises even under fake timers; advancing the fake
    // clock by zero still pumps the microtask queue so that resolution reaches state.
    await act(() => vi.advanceTimersByTimeAsync(0));

    expect(screen.getByText("Waiting 14 min")).toBeTruthy();
    expect(screen.getByText("#1 in line")).toBeTruthy();
    expect(screen.getByText("Dana")).toBeTruthy();

    expect(screen.getByRole("tab", { name: "Mine 1" })).toBeTruthy();
    expect(screen.getByRole("tab", { name: "Unassigned 1" })).toBeTruthy();
    expect(screen.getByRole("tab", { name: "All 2" })).toBeTruthy();
  });

  it("says so when there are no conversations", async () => {
    vi.mocked(listHandoffs).mockResolvedValue({ data: { items: [] } } as never);

    render(<InboxPanel meKey={MeKey} />);

    expect(await screen.findByText("No conversations.")).toBeTruthy();
  });

  it("reports the error from a refused request", async () => {
    vi.mocked(listHandoffs).mockRejectedValue(new Error("host refused"));

    render(<InboxPanel meKey={MeKey} />);

    expect(await screen.findByText("host refused")).toBeTruthy();
  });

  it("orders the done view newest first until the sort is toggled", async () => {
    const older = wire({
      id: 1,
      status: "done",
      doneAt: "2026-09-12T11:00:00",
      title: "Older, done first ended",
    });
    const newer = wire({
      id: 2,
      status: "done",
      doneAt: "2026-09-12T12:00:00",
      title: "Newer, done last ended",
    });

    vi.mocked(listHandoffs).mockImplementation(
      (options) =>
        Promise.resolve({
          data: { items: options?.query?.status === "done" ? [older, newer] : [] },
        }) as never,
    );

    const { container } = render(<InboxPanel meKey={MeKey} />);

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
});
