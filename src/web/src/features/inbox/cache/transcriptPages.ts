import type { ExportedMessageRepository } from "@assistant-ui/react";

import { alreadyHolds, mergeOlderPage, reloadOver, type HistoryPage } from "@/lib/history";

/**
 * How pages of a handoff's transcript go in among what the cache already holds.
 *
 * The transcript is the query cache's, and it is read two ways: the newest page whenever the
 * host says it changed, and an older page whenever the reader scrolls up for one. Both land on
 * one cached entry, so both have to leave the other's rows in place — and the cursor the
 * loader pages from has to stay the one the rows were paged from.
 */

type Item = ExportedMessageRepository["messages"][number];

const idOf = (item: Item): string => item.message.id;

/** Puts an older page ahead of what is held, unless it is already there. */
export function prependOlderPage(held: HistoryPage, page: ExportedMessageRepository): HistoryPage {
  if (alreadyHolds(held.repository.messages, page.messages, idOf)) return held;

  return { repository: mergeOlderPage(held.repository, page), nextCursor: held.nextCursor };
}

/**
 * Puts a freshly read newest page over what is held.
 *
 * The rows go in as {@link reloadOver} says. On top of that the page's root is hung off the last
 * kept row, the way {@link mergeOlderPage} hangs a root off the page before it, and the cursor
 * stays the held one: the kept rows were paged from it, and the page's own names a start
 * already loaded. A page that kept nothing replaces the transcript outright, cursor and all.
 */
export function reloadNewestPage(held: HistoryPage, fresh: HistoryPage): HistoryPage {
  const { rows, kept } = reloadOver(held.repository.messages, fresh.repository.messages, idOf);

  if (kept === 0) return fresh;

  const relinked = rows.slice();
  relinked[kept] = { ...relinked[kept]!, parentId: idOf(relinked[kept - 1]!) };

  return {
    repository: {
      ...(fresh.repository.headId !== undefined ? { headId: fresh.repository.headId } : {}),
      messages: relinked,
    },
    nextCursor: held.nextCursor,
  };
}
