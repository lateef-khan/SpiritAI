import { describe, expect, it } from "vitest";

import type { ChatwootConversationEvent, ChatwootPresence } from "@/lib/chatwoot";
import presenceFrames from "@/lib/chatwoot/payloads/presence_frames.json";
import socketFrames from "@/lib/chatwoot/payloads/socket_frames.json";
import { moved, NoChat, staffOnline, type Desk } from "./desk";

/*
 * The frames were recorded from local Chatwoot 4.18.0 while the bot answered, staff took the
 * chat and replied, resolved it, and the visitor wrote again.
 */

type Frame = { message?: { event: string; data: unknown } };

function pushes(event: string): unknown[] {
  return (socketFrames as Frame[]).flatMap((frame) =>
    frame.message?.event === event ? [frame.message.data] : [],
  );
}

const conversationEvents = (socketFrames as Frame[]).flatMap((frame) =>
  frame.message?.event === "conversation.updated" ||
  frame.message?.event === "conversation.status_changed"
    ? [frame.message.data as ChatwootConversationEvent]
    : [],
);

describe("moved", () => {
  it("draws one join and one close for the recorded chat, and ends back with the AI", () => {
    let desk: Desk = { ...NoChat, chatwoot: "pending" };
    const notes: string[] = [];

    for (const event of conversationEvents) {
      const next = moved(desk, event);
      desk = next.desk;
      if (next.note !== null) notes.push(next.note);
    }

    expect(notes).toEqual(["Matthew Hsu joined the chat.", "The chat was closed."]);
    expect(desk.status).toBe("bot");
    expect(desk.chatwoot).toBe("pending");
  });

  it("never names the agent bot as the person on the chat", () => {
    const [pending] = conversationEvents;

    const { desk } = moved({ ...NoChat, chatwoot: "pending" }, pending!);

    expect(pending!.meta.assignee?.name).toBe("Spirit AI");
    expect(desk.assigneeName).toBeNull();
  });

  it("says a person has the chat once staff take it", () => {
    const open = conversationEvents.find((event) => event.status === "open")!;

    const { desk } = moved({ ...NoChat, chatwoot: "pending" }, open);

    expect(desk).toMatchObject({ status: "human", assigneeName: "Matthew Hsu" });
  });
});

describe("staffOnline", () => {
  it("reads nobody from an empty presence, and someone once a member of staff is online", () => {
    expect(staffOnline(pushes("presence.update")[0] as ChatwootPresence)).toBe(false);

    const online = (presenceFrames as Frame[]).find(
      (frame) => frame.message?.event === "presence.update",
    )!.message!.data as ChatwootPresence;

    expect(staffOnline(online)).toBe(true);
  });
});
