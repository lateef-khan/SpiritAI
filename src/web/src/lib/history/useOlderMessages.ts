import { useAui, type ExportedMessageRepository } from "@assistant-ui/react";
import { useInfiniteQuery } from "@tanstack/react-query";
import { useEffect, useRef } from "react";
import type { HistoryPage } from "./wire.ts";
import { mergeOlderPage } from "./mergeOlderPage.ts";

/**
 * How close to the top a row has to render before the page before it is worth fetching.
 *
 * Not 0: overscan already draws a few rows past what the reader can see, and waiting for the
 * very first row to mount would fetch one screen later than a reader scrolling up expects.
 */
export const NEAR_TOP_ROWS = 5;

/**
 * Where older pages of one conversation come from, and where they go.
 *
 * The list is drawn the same way for a signed-in thread, the widget's public thread and a
 * handoff on the staff desk, but each keeps its messages somewhere else — assistant-ui's own
 * repository, a store the widget owns, the inbox's query cache — and reaches the host by a
 * different route. The source is the seam: the loader decides *when* to ask for a page, the
 * source says *how*, and *where* the page lands.
 */
export type OlderMessagesSource = {
  /** Names the conversation. Pages fetched under one id are never spliced into another's list. */
  id: string;
  /**
   * The cursor the newest page answered with: where the page before it starts.
   *
   * `undefined` while the newest page has not loaded yet — nothing to scroll up from, so nothing
   * is asked for. `null` once it has loaded and there is no older page.
   */
  initialCursor: string | null | undefined;
  /** Fetches the page before `before`, a cursor this or an earlier page answered with. */
  fetchPage(before: string): Promise<HistoryPage>;
  /**
   * Splices a fetched page in ahead of what is loaded. Defaults to assistant-ui's own
   * `import()`, with {@link mergeOlderPage} relinking the root.
   *
   * Must be idempotent: the loader forgets what it merged when the list unmounts and hands the
   * cached pages over again on the next mount, and a page the store already holds must not be
   * spliced in twice.
   */
  merge?: ((page: ExportedMessageRepository) => void) | undefined;
};

export type UseOlderMessagesOptions = {
  source: OlderMessagesSource;
  /**
   * The lowest `index` the virtualizer is currently drawing, or `null` while it draws nothing.
   *
   * `null` is not 0: before the scroll box has been measured the virtualizer lays out no row at
   * all, whatever the messages and wherever the box is, and nobody is near the top of nothing.
   */
  firstRenderedIndex: number | null;
  /** How many messages the virtualizer laid `firstRenderedIndex` out against, this render. */
  messageCount: number;
  /** Whether the reader is at the end of the list, this render. */
  atEnd: boolean;
  /**
   * Whether `firstRenderedIndex` was computed against where the scroll box is right now.
   *
   * Asked at the moment of deciding, not at render: the virtualizer takes its scroll position
   * from scroll events, so right after it has scrolled the element itself — to pin a freshly
   * loaded thread to its end, say — the box has moved and the index has not.
   */
  isRangeCurrent: () => boolean;
};

export type UseOlderMessagesResult = {
  /** Whether a page from before the oldest loaded message is in flight. */
  isFetchingOlder: boolean;
};

/**
 * Fetches and merges in the page before the oldest message currently loaded, as the reader nears
 * the top of the list.
 *
 * There is no assistant-ui hook for "load one more page of history" — `ThreadHistoryAdapter.load`
 * answers once, for the whole thread. So paging is this app's own job: a TanStack Query infinite
 * query keyed by the conversation and the cursor its newest page came with, and every page it
 * fetches handed to the source's `merge` the moment it arrives.
 *
 * @returns Whether an older page is in flight, for a "loading older" indicator.
 */
export function useOlderMessages({
  source,
  firstRenderedIndex,
  messageCount,
  atEnd,
  isRangeCurrent,
}: UseOlderMessagesOptions): UseOlderMessagesResult {
  const aui = useAui();

  const { id, initialCursor, fetchPage, merge } = source;

  // A new conversation — or the same one re-read from a different newest page — starts this
  // bookkeeping over: the pages already merged belonged to the list that just left.
  const mergedPageCount = useRef(0);

  useEffect(() => {
    mergedPageCount.current = 0;
  }, [id, initialCursor]);

  // A list opens at its end, and a reader who has not been there yet has not scrolled up from
  // it: rows sitting at the top before that are rows nothing has pinned yet. Pinning is not
  // this hook's — the viewport does it a frame after the messages mount — so it is watched for
  // rather than assumed, and forgotten with the messages it was seen with.
  const shownEnd = useRef(false);

  // Ready once the newest page has told us where the page before it starts — `null` included,
  // which just means there is no older page and this query has nothing to do.
  const ready = initialCursor !== undefined;
  const hasOlderPage = ready && initialCursor !== null;

  const { data, hasNextPage, isFetchingNextPage, fetchNextPage } = useInfiniteQuery({
    queryKey: ["older-messages", id, initialCursor],
    initialPageParam: initialCursor ?? null,
    // Never `null` when asked: a query seeded with no cursor is never asked for a page.
    queryFn: ({ pageParam }): Promise<HistoryPage> => fetchPage(pageParam!),
    getNextPageParam: (last) => last.nextCursor,
    // Never on its own: an enabled infinite query fetches its first page the moment it mounts,
    // which would pull the page before the newest one for every thread opened, scrolled up or
    // not. Every page, the first included, is asked for by the near-top effect below.
    enabled: false,
  });

  useEffect(() => {
    const pages = data?.pages;

    if (!pages) return;

    // Merged in fetch order: a page fetched later is always older than one fetched earlier, so
    // each new page must go in ahead of everything already spliced in.
    for (let i = mergedPageCount.current; i < pages.length; i += 1) {
      const page = pages[i]!.repository;

      if (merge) merge(page);
      else aui.thread().import(mergeOlderPage(aui.thread().export(), page));
    }
    mergedPageCount.current = pages.length;
  }, [data, aui, merge]);

  // Before the first page arrives `hasNextPage` is false for want of a page to read a cursor
  // off, yet the cursor the newest page came with says there is one.
  const morePages = data === undefined || hasNextPage;

  // The pages `firstRenderedIndex` was last read against. On the render where a page arrives the
  // effect above splices it in, but the rows only move on the render after — so the index still
  // says "near the top" of the list as it was, and would ask for one more page than the reader
  // scrolled up for.
  const seenData = useRef(data);

  useEffect(() => {
    // Only a render that drew rows can say: with nothing laid out yet the virtualizer's total
    // size is 0, and 0 from the end reads as "at the end" of a list nobody has seen.
    if (messageCount === 0) shownEnd.current = false;
    else if (atEnd && firstRenderedIndex !== null) shownEnd.current = true;

    const pageArrived = seenData.current !== data;

    seenData.current = data;
    if (pageArrived) return;

    if (!hasOlderPage) return;

    // The cursor can land a render before the page it came with; there is nothing to scroll up
    // from yet.
    if (messageCount === 0 || !shownEnd.current) return;

    if (firstRenderedIndex === null || firstRenderedIndex > NEAR_TOP_ROWS) return;

    if (!isRangeCurrent()) return;

    if (!morePages || isFetchingNextPage) return;

    void fetchNextPage();
  }, [
    hasOlderPage,
    firstRenderedIndex,
    messageCount,
    atEnd,
    isRangeCurrent,
    data,
    morePages,
    isFetchingNextPage,
    fetchNextPage,
  ]);

  return { isFetchingOlder: isFetchingNextPage };
}
