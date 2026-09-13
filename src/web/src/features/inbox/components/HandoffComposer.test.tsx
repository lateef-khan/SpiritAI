import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";

import { HostRefusedError } from "@/lib/apiClient";

import type { Handoff } from "../api/handoffsApi";

/**
 * The reply box, against a mocked wire.
 *
 * `@/api/sdk.gen` is mocked the same way `HandoffChat.test.tsx` mocks it; what is worth holding in
 * place here is what the box under one transcript looks like and sends in each handoff state, not
 * the network underneath.
 */
vi.mock("@/api/sdk.gen", () => ({ getHandoffMessages: vi.fn(), replyToHandoff: vi.fn() }));

const { getHandoffMessages, replyToHandoff } = await import("@/api/sdk.gen");
const { HandoffChat } = await import("./HandoffChat");

const Transcript = {
  headId: "call-1:0",
  messages: [
    {
      parentId: null,
      message: {
        id: "call-1:0",
        role: "user",
        content: [{ type: "text", text: "Hi, my CT800 belt slips when I go above 8 mph." }],
        attachments: [],
        createdAt: "2026-09-12T12:31:00",
        metadata: { custom: {} },
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

afterEach(() => cleanup());

describe("HandoffComposer", () => {
  it("sends what the owner types, and reloads the transcript", async () => {
    const TranscriptAfterReply = {
      headId: "call-1:1",
      messages: [
        ...Transcript.messages,
        {
          parentId: "call-1:0",
          message: {
            id: "call-1:1",
            role: "assistant",
            content: [{ type: "text", text: "Sure, one sec." }],
            status: { type: "complete", reason: "stop" },
            createdAt: "2026-09-12T12:39:00",
            metadata: {
              unstable_state: null,
              unstable_annotations: [],
              unstable_data: [],
              steps: [],
              custom: {},
            },
          },
        },
      ],
    };
    vi.mocked(getHandoffMessages)
      .mockResolvedValueOnce({ data: Transcript } as never)
      .mockResolvedValueOnce({ data: TranscriptAfterReply } as never);
    vi.mocked(replyToHandoff).mockResolvedValue({ data: undefined } as never);

    render(
      <HandoffChat
        handoff={handoff({
          status: "human",
          assignee: { key: "user:dana", name: "Dana" },
          email: "lorrie@northwind.example",
        })}
        meKey="user:dana"
        onChanged={() => {}}
      />,
    );

    const input = await screen.findByLabelText("Reply");
    fireEvent.change(input, { target: { value: "Sure, one sec." } });
    fireEvent.keyDown(input, { key: "Enter" });

    screen.getByText("This reply also goes to lorrie@northwind.example");
    // The reply box shares the thread composer's send button, and the status line sits under the
    // shell rather than beside the button.
    expect(await screen.findByRole("button", { name: "Send message" })).toBeTruthy();

    await waitFor(() => expect(getHandoffMessages).toHaveBeenCalledTimes(2));
    expect(replyToHandoff).toHaveBeenCalledWith(
      expect.objectContaining({
        path: { callId: "call-1" },
        body: { text: "Sure, one sec." },
      }),
    );

    // The composer's own textarea still holds "Sure, one sec." until it clears, so scope the
    // assertion to the rendered message group rather than matching on text alone.
    const messageGroup = (await screen.findByText("Sure, one sec.")).closest(
      '[data-role="assistant"]',
    );
    expect(messageGroup).not.toBeNull();
  });

  it("disables the box and explains itself while the chat is still waiting", async () => {
    vi.mocked(getHandoffMessages).mockResolvedValue({ data: Transcript } as never);

    render(
      <HandoffChat
        handoff={handoff({ status: "waiting" })}
        meKey="user:dana"
        onChanged={() => {}}
      />,
    );

    const input = await screen.findByLabelText("Reply");
    expect(input).toHaveProperty("disabled", true);
    expect(screen.getByText("Only the person who takes the chat can reply.")).toBeTruthy();
  });

  it("disables the box for someone who does not have the chat", async () => {
    vi.mocked(getHandoffMessages).mockResolvedValue({ data: Transcript } as never);

    render(
      <HandoffChat
        handoff={handoff({ status: "human", assignee: { key: "user:dana", name: "Dana" } })}
        meKey="user:other"
        onChanged={() => {}}
      />,
    );

    const input = await screen.findByLabelText("Reply");
    expect(input).toHaveProperty("disabled", true);
    expect(screen.getByText("Dana has this chat.")).toBeTruthy();
    // The "who has it" line sits under the shell, not crowded beside the send button.
    const shell = document.querySelector('[data-slot="aui_composer-shell"]');
    expect(shell?.textContent ?? "").not.toContain("Dana has this chat.");
  });

  it("shows no box once the chat is back with Spirit", async () => {
    vi.mocked(getHandoffMessages).mockResolvedValue({ data: Transcript } as never);

    render(
      <HandoffChat
        handoff={handoff({ status: "done", assignee: { key: "user:dana", name: "Dana" } })}
        meKey="user:dana"
        onChanged={() => {}}
      />,
    );

    await screen.findByText(
      "This chat is back with Spirit. The next message gets a normal bot answer.",
    );
  });

  it("shows the host's refusal under the box when nobody has the chat any more", async () => {
    vi.mocked(getHandoffMessages).mockResolvedValue({ data: Transcript } as never);
    vi.mocked(replyToHandoff).mockRejectedValue(
      new HostRefusedError(409, "/v1/handoff/call-1/reply", "Nobody has this chat."),
    );

    render(
      <HandoffChat
        handoff={handoff({ status: "human", assignee: { key: "user:dana", name: "Dana" } })}
        meKey="user:dana"
        onChanged={() => {}}
      />,
    );

    const input = await screen.findByLabelText("Reply");
    fireEvent.change(input, { target: { value: "Sure, one sec." } });
    fireEvent.keyDown(input, { key: "Enter" });

    await screen.findByText("Nobody has this chat.");
    expect(input).toHaveProperty("value", "Sure, one sec.");
  });
});
