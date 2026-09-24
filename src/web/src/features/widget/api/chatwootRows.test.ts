import { describe, expect, it } from "vitest";

import type { ChatwootMessage } from "@/lib/chatwoot";
import messagesBefore from "@/lib/chatwoot/payloads/messages_before.json";
import messagesNewest from "@/lib/chatwoot/payloads/messages_newest.json";
import { pageFromChatwoot } from "./chatwootRows";

/*
 * The pages were recorded from local Chatwoot 4.18.0: the newest 20 messages of conversation 27,
 * then the page before message 189, which is its oldest.
 */

function rows(messages: unknown) {
  return pageFromChatwoot(messages as ChatwootMessage[]).repository.messages.map(
    ({ parentId, message }) => ({
      parentId,
      id: message.id,
      role: message.role,
      text: message.content.map((part) => (part.type === "text" ? part.text : "")).join(""),
      speaker: (message.metadata.custom as { speaker?: { name: string } }).speaker?.name ?? null,
      hostMessageId: (message.metadata.custom as { hostMessageId: string }).hostMessageId,
    }),
  );
}

describe("pageFromChatwoot", () => {
  it("pages on from the oldest message of a full page", () => {
    const page = pageFromChatwoot(messagesNewest as ChatwootMessage[]);

    expect(page.repository.messages).toHaveLength(20);
    expect(page.nextCursor).toBe("189");
  });

  it("reads a short page as the oldest one, and draws each side as it spoke", () => {
    expect(pageFromChatwoot(messagesBefore as ChatwootMessage[]).nextCursor).toBeNull();

    expect(rows(messagesBefore).slice(0, 4)).toEqual([
      {
        parentId: null,
        id: "cw-180",
        role: "user",
        text: "Do you have the XT485 in stock?",
        speaker: null,
        hostMessageId: "180",
      },
      {
        parentId: "cw-180",
        id: "cw-181",
        role: "assistant",
        text: "Yes, the **XT485** is in stock.",
        speaker: null,
        hostMessageId: "181",
      },
      {
        parentId: "cw-181",
        id: "cw-184",
        role: "assistant",
        text: "Hi, this is Matthew. I can help.",
        speaker: "Matthew",
        hostMessageId: "184",
      },
      {
        parentId: "cw-184",
        id: "cw-186",
        role: "user",
        text: "One more question.",
        speaker: null,
        hostMessageId: "186",
      },
    ]);
  });
});
