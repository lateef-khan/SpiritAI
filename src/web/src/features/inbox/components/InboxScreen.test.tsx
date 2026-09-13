import { act, cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";

import type { HandoffSummary } from "@/api/types.gen";

/**
 * The screen, against a mocked wire.
 *
 * `@/api/sdk.gen` is mocked the same way `InboxPanel.test.tsx` and `HandoffChat.test.tsx` mock
 * it: what is worth holding in place here is that a row click carries the picked handoff from the
 * panel into the chat pane, not the network underneath. `@/hooks/use-mobile` is mocked separately
 * per test, since `InboxScreen` reads it to choose which of the two layouts to render.
 */
vi.mock("@/api/sdk.gen", () => ({ listHandoffs: vi.fn(), getHandoffMessages: vi.fn() }));
vi.mock("@/hooks/use-mobile", () => ({ useIsMobile: vi.fn(() => false) }));

const { listHandoffs, getHandoffMessages } = await import("@/api/sdk.gen");
const { useIsMobile } = await import("@/hooks/use-mobile");
const { InboxScreen } = await import("./InboxScreen");

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
    vi.mocked(getHandoffMessages).mockResolvedValue({ data: Transcript } as never);

    render(<InboxScreen meKey={MeKey} />);

    await act(() => vi.advanceTimersByTimeAsync(0));

    fireEvent.click(screen.getByText("Treadmill belt slips at 8 mph"));

    await act(() => vi.advanceTimersByTimeAsync(0));

    expect(screen.getByText("Started 15 min ago")).toBeTruthy();
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
    vi.mocked(getHandoffMessages).mockResolvedValue({ data: Transcript } as never);

    render(<InboxScreen meKey={MeKey} />);

    await act(() => vi.advanceTimersByTimeAsync(0));

    fireEvent.click(screen.getByText("Treadmill belt slips at 8 mph"));

    await act(() => vi.advanceTimersByTimeAsync(0));

    expect(screen.getByText("Started 15 min ago")).toBeTruthy();

    // Radix opens a dropdown on a key press as readily as on a pointer, and happy-dom has no
    // pointer.
    fireEvent.keyDown(screen.getByRole("button", { name: "Open" }), { key: "Enter" });
    await act(() => vi.advanceTimersByTimeAsync(0));
    fireEvent.click(screen.getByText("Done"));

    expect(screen.getByText("Pick a conversation.")).toBeTruthy();
  });

  it("on mobile, a row click fills the width with the chat, and Back returns to the list", async () => {
    vi.mocked(useIsMobile).mockReturnValue(true);
    vi.mocked(listHandoffs).mockImplementation(
      (options) =>
        Promise.resolve({
          data: { items: options?.query?.status === "waiting" ? [wire()] : [] },
        }) as never,
    );
    vi.mocked(getHandoffMessages).mockResolvedValue({ data: Transcript } as never);

    render(<InboxScreen meKey={MeKey} />);

    await screen.findByText("Treadmill belt slips at 8 mph");

    fireEvent.click(screen.getByText("Treadmill belt slips at 8 mph"));

    await screen.findByRole("button", { name: "Back" });

    expect(screen.queryByText("Conversations")).toBeNull();

    fireEvent.click(screen.getByRole("button", { name: "Back" }));

    expect(await screen.findByText("Treadmill belt slips at 8 mph")).toBeTruthy();
  });
});
