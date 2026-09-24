import type { ExportedMessageRepository } from "@assistant-ui/react";

import type { HistoryPage } from "@/lib/history";
import { ChatwootPageSize, type ChatwootMessage } from "@/lib/chatwoot";
import type { Held } from "../hooks/held";

/**
 * Chatwoot's messages as the widget draws them.
 *
 * Every row is Markdown: a card the page streamed live comes back as its Markdown copy. A row
 * holds its Chatwoot id as `hostMessageId`, which is how a reload and a push find it again.
 */

/** Chatwoot's `message_type` for the visitor's words and for words to the visitor. */
const Incoming = 0;
const Outgoing = 1;

/**
 * One Chatwoot message as a row, or `null` for one the visitor is not shown.
 *
 * A member of staff speaks under their name. The agent bot is the AI and speaks as the widget's
 * own replies do.
 */
export function heldFromChatwoot(message: ChatwootMessage): Held | null {
  const text = message.content ?? "";
  const kind = message.message_type;
  if (text.length === 0 || (kind !== Incoming && kind !== Outgoing)) return null;

  const staff = message.sender?.type === "user" ? message.sender.name : null;

  return {
    id: `cw-${message.id}`,
    role: kind === Incoming ? "user" : "assistant",
    content: [{ type: "text", text }],
    createdAt: new Date(message.created_at * 1000),
    ...(kind === Outgoing ? { status: { type: "complete", reason: "stop" } as const } : {}),
    metadata: {
      custom: {
        hostMessageId: String(message.id),
        ...(staff === null ? {} : { speaker: { kind: "human", name: staff } }),
      },
    },
  };
}

/** A line the widget draws itself, such as "Dana joined the chat.", centred as an event. */
export function systemNote(text: string): Held {
  return {
    id: crypto.randomUUID(),
    role: "assistant",
    content: [{ type: "text", text }],
    createdAt: new Date(),
    status: { type: "complete", reason: "stop" },
    metadata: { custom: { speaker: { kind: "system", name: "" } } },
  };
}

/**
 * One Chatwoot page as a page of history. Its cursor is the oldest message's id, or `null` when
 * the page is short, which makes it the oldest one.
 *
 * @param messages The page, oldest first.
 */
export function pageFromChatwoot(messages: readonly ChatwootMessage[]): HistoryPage {
  const rows = messages.flatMap((message) => heldFromChatwoot(message) ?? []);
  const oldest = messages[0];

  return {
    repository: {
      messages: rows.map((row, index) => ({
        parentId: index === 0 ? null : rows[index - 1]!.id,
        message: row,
      })),
    } as ExportedMessageRepository,
    nextCursor:
      messages.length < ChatwootPageSize || oldest === undefined ? null : String(oldest.id),
  };
}
