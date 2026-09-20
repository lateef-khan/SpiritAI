import type { ExportedMessageRepository } from "@assistant-ui/react";

/**
 * Splices an older page of history in front of what is already loaded.
 *
 * `aui.thread().import()` takes one flat list with no notion of "before" or "after" — a message's
 * place comes only from `parentId`. So prepending a page is a graph edit: the current root (the
 * one item whose `parentId` is `null`) stops being a root and hangs off the older page's last
 * message instead. The older page's own first item is left with `parentId: null` — it is the new
 * root now that it exists.
 *
 * A repository with no root (every item already has a parent, because a second call raced ahead
 * of this one) is returned unchanged: there is nothing here for the older page to attach to.
 *
 * @param current The repository as it stands in the runtime.
 * @param olderPage The page fetched for `before` the oldest message `current` holds.
 * @returns The merged repository, or `current` unchanged when the page is empty or there is
 *   nothing to relink.
 */
export function mergeOlderPage(
  current: ExportedMessageRepository,
  olderPage: ExportedMessageRepository,
): ExportedMessageRepository {
  const lastOlderMessageId = olderPage.messages.at(-1)?.message.id;
  if (lastOlderMessageId === undefined) return current;

  const rootIndex = current.messages.findIndex((item) => item.parentId === null);
  if (rootIndex === -1) return current;

  const relinked = current.messages.slice();
  relinked[rootIndex] = { ...relinked[rootIndex]!, parentId: lastOlderMessageId };

  return {
    ...(current.headId !== undefined ? { headId: current.headId } : {}),
    messages: [...olderPage.messages, ...relinked],
  };
}
