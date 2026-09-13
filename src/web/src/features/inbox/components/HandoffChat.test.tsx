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
          custom: { speaker: { kind: "human", name: "Dana", detail: "Spirit service" } },
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

/**
 * A claimed chat after the backend stopped merging one turn's rows: the host's joined/left lines
 * and the staff reply arrive as three assistant messages with three speakers, not one jammed
 * "Dana joinedHi, I'm Dana.Dana left" under the wrong name.
 */
const ClaimedTranscript = {
  headId: "call-9:4",
  messages: [
    {
      parentId: null,
      message: {
        id: "call-9:0",
        role: "user",
        content: [{ type: "text", text: "Hello? Is anyone there?" }],
        attachments: [],
        createdAt: "2026-09-12T12:51:00",
        metadata: { custom: {} },
      },
    },
    {
      parentId: "call-9:0",
      message: {
        id: "call-9:1",
        role: "assistant",
        content: [{ type: "text", text: "Let me find someone." }],
        status: { type: "complete", reason: "stop" },
        createdAt: "2026-09-12T12:51:00",
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
      parentId: "call-9:1",
      message: {
        id: "call-9:2",
        role: "assistant",
        content: [{ type: "text", text: "Dana joined" }],
        status: { type: "complete", reason: "stop" },
        createdAt: "2026-09-12T12:52:00",
        metadata: {
          unstable_state: null,
          unstable_annotations: [],
          unstable_data: [],
          steps: [],
          custom: { speaker: { kind: "system", name: "Spirit" } },
        },
      },
    },
    {
      parentId: "call-9:2",
      message: {
        id: "call-9:3",
        role: "assistant",
        content: [{ type: "text", text: "Hi, I'm Dana." }],
        status: { type: "complete", reason: "stop" },
        createdAt: "2026-09-12T12:52:00",
        metadata: {
          unstable_state: null,
          unstable_annotations: [],
          unstable_data: [],
          steps: [],
          custom: { speaker: { kind: "human", name: "Dana", detail: "Support" } },
        },
      },
    },
  ],
};

describe("HandoffChat", () => {
  it("shows the wait time, the transcript in order, and who joined, for a waiting handoff", async () => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date("2026-09-12T12:53:00"));
    vi.mocked(getHandoffMessages).mockResolvedValue({ data: Transcript } as never);

    render(<HandoffChat handoff={handoff()} meKey="user:dana" onChanged={() => {}} />);

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
        meKey="user:other"
        onChanged={() => {}}
      />,
    );

    expect(await screen.findByText("Dana has this chat")).toBeTruthy();
  });

  it("shows the chat is back with Spirit once it is done", async () => {
    vi.mocked(getHandoffMessages).mockResolvedValue({ data: Transcript } as never);

    render(
      <HandoffChat
        handoff={handoff({ status: "done", assignee: { key: "user:dana", name: "Dana" } })}
        meKey="user:dana"
        onChanged={() => {}}
      />,
    );

    expect(await screen.findByText("Back with Spirit")).toBeTruthy();
  });

  it("names the visitor, the model, and staff on their own messages", async () => {
    vi.mocked(getHandoffMessages).mockResolvedValue({ data: ClaimedTranscript } as never);

    render(
      <HandoffChat
        handoff={handoff({ status: "human", assignee: { key: "user:dana", name: "Dana" } })}
        meKey="user:dana"
        onChanged={() => {}}
      />,
    );

    expect(await screen.findByText("Visitor")).toBeTruthy();
    expect(screen.getByText("Spirit")).toBeTruthy();
    expect(screen.getAllByText("Dana").length).toBeGreaterThan(0);
  });

  it("draws joined lines as notes with no message actions", async () => {
    vi.mocked(getHandoffMessages).mockResolvedValue({ data: ClaimedTranscript } as never);

    render(
      <HandoffChat
        handoff={handoff({ status: "human", assignee: { key: "user:dana", name: "Dana" } })}
        meKey="user:dana"
        onChanged={() => {}}
      />,
    );


    const note = await screen.findByText("Dana joined");
    expect(note.closest('[data-slot="aui_system-note"]')).not.toBeNull();
    // The staff reply is a message of its own, not jammed onto the joined line.
    expect(screen.getByText("Hi, I'm Dana.").closest('[data-slot="aui_system-note"]')).toBeNull();
  });

  it("draws staff replies as bubbles on the support side", async () => {
    vi.mocked(getHandoffMessages).mockResolvedValue({ data: ClaimedTranscript } as never);

    render(
      <HandoffChat
        handoff={handoff({ status: "human", assignee: { key: "user:dana", name: "Dana" } })}
        meKey="user:dana"
        onChanged={() => {}}
      />,
    );

    // The staff reply is a bubble of its own, not bare text and not a system note.
    const reply = await screen.findByText("Hi, I'm Dana.");
    expect(reply.closest('[data-slot="aui_staff-message-root"]')).not.toBeNull();
    expect(reply.closest('[data-slot="aui_system-note"]')).toBeNull();
    // The visitor's bubble stays on the other side, under its own name.
    const visitor = screen.getByText("Hello? Is anyone there?");
    expect(visitor.closest('[data-slot="aui_user-message-root"]')).not.toBeNull();
    expect(visitor.closest('[data-slot="aui_staff-message-root"]')).toBeNull();
  });

  it("offers no retry on a staff reply", async () => {
    // The staff reply is the last message, so its action bar is the one drawn.
    vi.mocked(getHandoffMessages).mockResolvedValue({ data: ClaimedTranscript } as never);

    render(
      <HandoffChat
        handoff={handoff({ status: "human", assignee: { key: "user:dana", name: "Dana" } })}
        meKey="user:dana"
        onChanged={() => {}}
      />,
    );

    await screen.findByText("Hi, I'm Dana.");
    expect(screen.queryByLabelText("Regenerate with a different model")).toBeNull();
  });

  it("keeps retry on the model's own answers", async () => {
    const waiting = {
      headId: "call-9:1",
      messages: ClaimedTranscript.messages.slice(0, 2),
    };
    vi.mocked(getHandoffMessages).mockResolvedValue({ data: waiting } as never);

    render(<HandoffChat handoff={handoff()} meKey="user:dana" onChanged={() => {}} />);

    // The model's answer is last, so its action bar is the one drawn — with retry.
    await screen.findByText("Let me find someone.");
    expect(screen.getByLabelText("Regenerate with a different model")).toBeTruthy();
});
});
