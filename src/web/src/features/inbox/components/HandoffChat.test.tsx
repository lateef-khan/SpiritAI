import { act, cleanup, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";

import type { Handoff } from "../api/handoffsApi";

/**
 * The chat pane, against a mocked wire.
 *
 * `@/api/sdk.gen` is mocked the same way `InboxPanel.test.tsx` mocks `listHandoffs`: what is
 * worth holding in place here is what one transcript reads as once `useHandoffMessages` has
 * revived it, not the network underneath.
 */
vi.mock("@/api/sdk.gen", () => ({ getHandoffMessages: vi.fn() }));

const { getHandoffMessages } = await import("@/api/sdk.gen");
const { HandoffChat } = await import("./HandoffChat");

const Transcript = {
  headId: "call-1:2",
  messages: [
    {
      parentId: null,
      message: {
        id: "call-1:0",
        role: "user",
        content: [
          { type: "text", text: "Hi, my CT800 belt slips when I go above 8 mph. It's about 2 years old." },
        ],
        attachments: [],
        createdAt: "2026-09-12T12:31:00",
        metadata: { custom: {} },
      },
    },
    {
      parentId: "call-1:0",
      message: {
        id: "call-1:1",
        role: "assistant",
        content: [{ type: "text", text: "A belt that slips at speed usually needs tension." }],
        status: { type: "complete", reason: "stop" },
        createdAt: "2026-09-12T12:31:00",
        metadata: {
          unstable_state: null,
          unstable_annotations: [],
          unstable_data: [],
          steps: [],
          custom: {},
        },
      },
    },
    {
      parentId: "call-1:1",
      message: {
        id: "call-1:2",
        role: "assistant",
        content: [{ type: "text", text: "Hi, I'm Dana from Spirit service." }],
        status: { type: "complete", reason: "stop" },
        createdAt: "2026-09-12T12:50:00",
        metadata: {
          unstable_state: null,
          unstable_annotations: [],
          unstable_data: [],
          steps: [],
          custom: { speaker: { kind: "staff", name: "Dana", detail: "Spirit service" } },
        },
      },
    },
  ],
};

function handoff(over: Partial<Handoff> = {}): Handoff {
  return {
    id: 1,
    callId: "call-1",
    status: "waiting",
    askedBy: "visitor",
    reason: null,
    askedAt: new Date("2026-09-12T12:38:00"),
    assignee: null,
    claimedAt: null,
    email: null,
    doneAt: null,
    title: "Treadmill belt slips at 8 mph",
    firstLine: "Hi, my CT800 belt slips when I go above 8 mph.",
    position: null,
    ...over,
  };
}

afterEach(() => {
  cleanup();
  vi.useRealTimers();
});

describe("HandoffChat", () => {
  it("shows the wait time, the transcript in order, and who joined, for a waiting handoff", async () => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date("2026-09-12T12:53:00"));
    vi.mocked(getHandoffMessages).mockResolvedValue({ data: Transcript } as never);

    render(<HandoffChat handoff={handoff()} />);

    // The load effect resolves through real promises even under fake timers; advancing the fake
    // clock by zero still pumps the microtask queue so that resolution reaches state.
    await act(() => vi.advanceTimersByTimeAsync(0));

    expect(screen.getByText("Hi, I'm Dana from Spirit service.")).toBeTruthy();
    expect(screen.getByText("Started 15 min ago")).toBeTruthy();
    expect(screen.getByText("15 min")).toBeTruthy();
    expect(screen.getByText("Dana")).toBeTruthy();

    const bodyText = document.body.textContent ?? "";
    const visitorAt = bodyText.indexOf("Hi, my CT800 belt slips");
    const botAt = bodyText.indexOf("A belt that slips at speed");
    const staffAt = bodyText.indexOf("Hi, I'm Dana from Spirit service.");

    expect(visitorAt).toBeGreaterThanOrEqual(0);
    expect(visitorAt).toBeLessThan(botAt);
    expect(botAt).toBeLessThan(staffAt);
  });

  it("shows who has the chat once it is claimed", async () => {
    vi.mocked(getHandoffMessages).mockResolvedValue({ data: Transcript } as never);

    render(
      <HandoffChat
        handoff={handoff({ status: "human", assignee: { key: "user:dana", name: "Dana" } })}
      />,
    );

    expect(await screen.findByText("Dana has this chat")).toBeTruthy();
  });

  it("shows the chat is back with Spirit once it is done", async () => {
    vi.mocked(getHandoffMessages).mockResolvedValue({ data: Transcript } as never);

    render(
      <HandoffChat
        handoff={handoff({ status: "done", assignee: { key: "user:dana", name: "Dana" } })}
      />,
    );

    expect(await screen.findByText("Back with Spirit")).toBeTruthy();
  });
});
