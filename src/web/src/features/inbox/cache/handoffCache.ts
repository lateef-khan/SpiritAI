import type { InfiniteData, QueryClient } from "@tanstack/react-query";

import type { HandoffCounts } from "@/api/types.gen";
import type { ClaimedPush, DonePush, MessagePush } from "@/features/handoff/events";

import type { Handoff, HandoffFilter, HandoffOrder, HandoffPage } from "../api/handoffsApi";
import { filterOfKey, handoffKeys } from "./handoffKeys";

/**
 * What a socket push does to the cache: patch what it says outright, and refetch only what it
 * cannot say.
 *
 * A push names one row. Every listing that holds the row is edited in place — its status, its
 * assignee, its removal — and the counts move by exactly what the row was and is. Only an insert
 * whose place in a page is unknown, or a row the cache has never seen, falls back to marking the
 * affected answers stale; TanStack then refetches the ones on screen, keeping the old rows up
 * until the new ones land, and leaves the rest for their next mount.
 *
 * The socket is a hint, and this is the cache's best reading of it. A reconnect marks everything
 * stale, so a push lost while the socket was down is caught up.
 */

/** The shape `useInfiniteQuery` keeps one listing in. */
export type HandoffPages = InfiniteData<HandoffPage, string | null>;

/** Puts one new waiting row where the open listings would draw it. */
export function applyWaiting(cache: QueryClient, row: Handoff): void {
  if (findRow(cache, row.callId) !== null) return;

  // A waiting row is nobody's, so the caller's own listing never shows it.
  patchOpenLists(cache, (pages, filter) =>
    filter.owner === "me" ? pages : placeNewest(pages, row, filter.order),
  );

  // A listing that could not take the row in place is stale; the ones on screen refetch.
  void cache.invalidateQueries({
    queryKey: handoffKeys.listsOf("open"),
    predicate: (query) =>
      filterOfKey(query.queryKey)?.owner !== "me" &&
      !holds(query.state.data as HandoffPages | undefined, row.callId),
  });

  patchCounts(cache, "open", (counts) => ({
    ...counts,
    unassigned: counts.unassigned + 1,
    all: counts.all + 1,
  }));
}

/** Moves one row from the queue to its new holder. */
export function applyClaimed(cache: QueryClient, push: ClaimedPush, meKey: string): void {
  const before = findRow(cache, push.callId);

  if (before === null) {
    void cache.invalidateQueries({ queryKey: handoffKeys.listsOf("open") });
    void cache.invalidateQueries({ queryKey: handoffKeys.counts("open") });
    return;
  }

  if (before.status === "human" && before.assignee?.key === push.assignee.key) return;

  const after: Handoff = {
    ...before,
    status: "human",
    assignee: push.assignee,
    claimedAt: new Date(),
    position: null,
  };

  patchOpenLists(cache, (pages, filter) =>
    closeUp(
      filter.owner === "none" ? dropRow(pages, push.callId) : replaceRow(pages, after),
      before.position,
    ),
  );

  // The holder's own listing gains a row it never had; only a refetch knows where.
  if (push.assignee.key === meKey) {
    void cache.invalidateQueries({
      queryKey: handoffKeys.listsOf("open"),
      predicate: (query) =>
        filterOfKey(query.queryKey)?.owner === "me" &&
        !holds(query.state.data as HandoffPages | undefined, push.callId),
    });
  }

  const mine = push.assignee.key === meKey;
  patchCounts(cache, "open", (counts) => ({
    ...counts,
    unassigned: counts.unassigned - 1,
    mine: mine ? counts.mine + 1 : counts.mine,
    awaitingReply: mine && before.awaitingReply ? counts.awaitingReply + 1 : counts.awaitingReply,
  }));
}

/** Takes one row out of the open listings, and lets the done ones learn of it on their next read. */
export function applyDone(cache: QueryClient, push: DonePush, meKey: string): void {
  const before = findRow(cache, push.callId);

  void cache.invalidateQueries({ queryKey: handoffKeys.listsOf("done") });
  void cache.invalidateQueries({ queryKey: handoffKeys.counts("done") });

  if (before === null) {
    void cache.invalidateQueries({ queryKey: handoffKeys.counts("open") });
    return;
  }

  patchOpenLists(cache, (pages) => closeUp(dropRow(pages, push.callId), before.position));

  const mine = before.status === "human" && before.assignee?.key === meKey;
  patchCounts(cache, "open", (counts) => ({
    mine: mine ? counts.mine - 1 : counts.mine,
    unassigned: before.assignee === null ? counts.unassigned - 1 : counts.unassigned,
    all: counts.all - 1,
    awaitingReply: mine && before.awaitingReply ? counts.awaitingReply - 1 : counts.awaitingReply,
  }));
}

/**
 * Notes who spoke last in one chat, and marks its transcript stale.
 */
export function applyMessage(cache: QueryClient, message: MessagePush, meKey: string): void {
  void cache.invalidateQueries({ queryKey: handoffKeys.messages(message.callId) });

  const awaiting =
    message.role === "user" ? true : message.speaker?.kind === "human" ? false : null;
  if (awaiting === null) return;

  const before = findRow(cache, message.callId);

  if (before === null) {
    void cache.invalidateQueries({ queryKey: handoffKeys.counts("open") });
    return;
  }

  const unread = awaiting || before.unread;
  if (before.awaitingReply === awaiting && before.unread === unread) return;

  patchOpenLists(cache, (pages) =>
    replaceRow(pages, { ...before, awaitingReply: awaiting, unread }),
  );

  if (before.awaitingReply === awaiting) return;

  if (before.status === "human" && before.assignee?.key === meKey) {
    patchCounts(cache, "open", (counts) => ({
      ...counts,
      awaitingReply: counts.awaitingReply + (awaiting ? 1 : -1),
    }));
  }
}

/** Takes the unread dot off one chat, once the caller has seen it. */
export function applySeen(cache: QueryClient, callId: string): void {
  patchLists(cache, (pages) => {
    const row = rowIn(pages, callId);
    return row && row.unread ? replaceRow(pages, { ...row, unread: false }) : pages;
  });
}

/** Marks every handoff answer stale, for a reconnect. */
export function applyReconnect(cache: QueryClient): void {
  void cache.invalidateQueries({ queryKey: handoffKeys.all });
}

/** The row as any open listing last saw it, or `null` when none holds it. */
export function findRow(cache: QueryClient, callId: string): Handoff | null {
  for (const [, pages] of cache.getQueriesData<HandoffPages>({
    queryKey: handoffKeys.listsOf("open"),
  })) {
    for (const page of pages?.pages ?? []) {
      const row = page.items.find((item) => item.callId === callId);
      if (row) return row;
    }
  }
  return null;
}

/** Edits every cached listing, open and done alike. */
function patchLists(cache: QueryClient, patch: (pages: HandoffPages) => HandoffPages): void {
  for (const [key, pages] of cache.getQueriesData<HandoffPages>({
    queryKey: handoffKeys.lists(),
  })) {
    if (pages) cache.setQueryData<HandoffPages>(key, patch(pages));
  }
}

/** Edits every cached open listing, told which filter each one is. */
function patchOpenLists(
  cache: QueryClient,
  patch: (pages: HandoffPages, filter: HandoffFilter) => HandoffPages,
): void {
  for (const [key, pages] of cache.getQueriesData<HandoffPages>({
    queryKey: handoffKeys.listsOf("open"),
  })) {
    const filter = filterOfKey(key);
    if (pages && filter) cache.setQueryData<HandoffPages>(key, patch(pages, filter));
  }
}

function holds(pages: HandoffPages | undefined, callId: string): boolean {
  return rowIn(pages, callId) !== null;
}

function rowIn(pages: HandoffPages | undefined, callId: string): Handoff | null {
  for (const page of pages?.pages ?? []) {
    const row = page.items.find((item) => item.callId === callId);
    if (row) return row;
  }
  return null;
}

/**
 * Puts a row that is newer than every cached one at the listing's newest end: the front of a
 * newest-first listing, or the back of an oldest-first one whose last page is the last there is.
 * An oldest-first listing with pages still unread cannot take it, and is left alone for
 * `applyWaiting` to mark stale.
 */
function placeNewest(pages: HandoffPages, row: Handoff, order: HandoffOrder): HandoffPages {
  const first = pages.pages[0];
  const last = pages.pages[pages.pages.length - 1];
  if (!first || !last) return pages;

  if (order === "newest") {
    return {
      ...pages,
      pages: [{ ...first, items: [row, ...first.items] }, ...pages.pages.slice(1)],
    };
  }

  if (last.nextCursor === null) {
    return {
      ...pages,
      pages: [...pages.pages.slice(0, -1), { ...last, items: [...last.items, row] }],
    };
  }

  return pages;
}

function replaceRow(pages: HandoffPages, row: Handoff): HandoffPages {
  return {
    ...pages,
    pages: pages.pages.map((page) => ({
      ...page,
      items: page.items.map((item) => (item.callId === row.callId ? row : item)),
    })),
  };
}

function dropRow(pages: HandoffPages, callId: string): HandoffPages {
  return {
    ...pages,
    pages: pages.pages.map((page) => ({
      ...page,
      items: page.items.filter((item) => item.callId !== callId),
    })),
  };
}

/** Everyone who stood behind a waiting row that left the line moves up one. */
function closeUp(pages: HandoffPages, leftAt: number | null): HandoffPages {
  if (leftAt === null) return pages;
  return {
    ...pages,
    pages: pages.pages.map((page) => ({
      ...page,
      items: page.items.map((item) =>
        item.position !== null && item.position > leftAt
          ? { ...item, position: item.position - 1 }
          : item,
      ),
    })),
  };
}

function patchCounts(
  cache: QueryClient,
  view: "open" | "done",
  patch: (counts: HandoffCounts) => HandoffCounts,
): void {
  cache.setQueryData<HandoffCounts>(handoffKeys.counts(view), (counts) =>
    counts ? patch(counts) : counts,
  );
}
