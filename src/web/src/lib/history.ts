import type { ExportedMessageRepository } from "@assistant-ui/react";
import type { ThreadHistory } from "@/api/types.gen";
import { fileContent, fileLinks, type ReplyFile } from "@/lib/files";
import { resolveSandboxLinks } from "@/lib/sandboxLinks";

/** The conversation as the host sends it, before its dates are dates. */
export type WireHistory = ThreadHistory;

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
