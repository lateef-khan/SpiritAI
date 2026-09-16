import type { ExportedMessageRepository } from "@assistant-ui/react";
import type { ThreadHistory } from "@/api/types.gen";

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
        createdAt: new Date(item.message.createdAt),
      },
    })) as ExportedMessageRepository["messages"],
  };
}
