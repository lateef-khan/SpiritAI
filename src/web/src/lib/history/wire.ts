import type { ExportedMessageRepository } from "@assistant-ui/react";
import type { ThreadHistory } from "@/api/types.gen";
import { fileContent, fileLinks, type ReplyFile } from "@/lib/files";
import { resolveSandboxLinks } from "@/lib/sandboxLinks";

/** The conversation as the host sends it, before its dates are dates. */
export type WireHistory = ThreadHistory;

/** One page of a conversation's history: the messages, and where the page before them starts. */
export type HistoryPage = {
  repository: ExportedMessageRepository;
  /** The cursor for the page before this one, or `null` when this is already the oldest. */
  nextCursor: string | null;
};

/**
 * Turns the wire's dates back into dates.
 *
 * assistant-ui refuses a message whose `createdAt` is a string, and JSON has no date type, so
 * something has to do this. Doing it here rather than in the adapter keeps every wire concern on
 * one side of the seam.
 *
 * @param raw The body the host sent.
 * @returns The same conversation, with real `Date`s on it.
 */
export function reviveHistory(raw: WireHistory): ExportedMessageRepository {
  return {
    ...(raw.headId != null ? { headId: raw.headId } : {}),
    messages: raw.messages.map((item) => ({
      parentId: item.parentId,
      message: {
        ...item.message,
        content: drawFiles(item.message.content),
        createdAt: new Date(item.message.createdAt),
      },
    })) as ExportedMessageRepository["messages"],
  };
}

/**
 * Reads one page as the host sent it, cursor and all.
 *
 * The three history routes — the signed-in thread, the widget's public thread, the staff desk's
 * handoff — page the same way and answer the same shape, so the reading lives once. A cursor the
 * host left out (the page reaches the conversation's start) reads as `null` rather than
 * `undefined`: "no older page" is an answer, and the loader treats `undefined` as "not asked yet".
 *
 * @param raw The body the host sent.
 * @returns The page, with real `Date`s on its messages.
 */
export function revivePage(raw: WireHistory): HistoryPage {
  return { repository: reviveHistory(raw), nextCursor: raw.nextCursor ?? null };
}

/**
 * Reads the newest page of one conversation's messages, or — with `before` — the page just older
 * than it.
 *
 * @param conversationId The conversation, under the name the route knows it by.
 * @param before A cursor from an earlier page's `nextCursor`. Omitted for the newest page.
 * @param limit How many turns to fetch. The host defaults to 30 and caps at 100.
 */
export type ReadHistory = (
  conversationId: string,
  before?: string,
  limit?: number,
) => Promise<HistoryPage>;

/** Spells one page on the query string, naming only what the caller asked for. */
export function pageQuery(before?: string, limit?: number): { before?: string; limit?: number } {
  return { ...(before ? { before } : {}), ...(limit ? { limit } : {}) };
}

/**
 * Turns the host's file parts into the parts assistant-ui draws, and points the words at them.
 *
 * The host sends the words as stored, `sandbox:` links and all, and each kept file as facts and a
 * link beside them. The link is minted per read, so the join happens here and is never stored.
 * Picture or download is decided by the same `fileContent` a live turn uses.
 */
function drawFiles(content: WireHistory["messages"][number]["message"]["content"]) {
  const files: ReplyFile[] = content.flatMap((part) => (part.type === "file" ? [part] : []));
  const links = fileLinks(files);

  return content.flatMap((part) => {
    if (part.type === "file") return fileContent(part) ?? [];
    if (part.type === "text") return { ...part, text: resolveSandboxLinks(part.text, links) };
    return part;
  });
}
