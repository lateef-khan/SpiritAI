import { useCallback, useMemo } from "react";
import { useInfiniteQuery } from "@tanstack/react-query";

import type { HandoffCounts } from "@/api/types.gen";

import {
  createHandoffsApi,
  type Handoff,
  type HandoffFilter,
  type HandoffsApi,
  type HandoffView,
} from "../api/handoffsApi";
import { handoffKeys } from "../cache/handoffKeys";

/** The inbox's two halves: rows still open for a human, and rows already closed. */
export type InboxView = HandoffView;

/** How the current view's rows are split by who is on them. */
export type InboxTab = "mine" | "unassigned" | "all";

/** How many rows each tab would show, for the tab strip's badges. */
export type InboxCounts = HandoffCounts;

/**
 * The signed-in api, built once.
 *
 * A default parameter is re-evaluated on every render, so `api: HandoffsApi =
 * createHandoffsApi()` would hand the hook a new object each time, and a query function that
 * closes over a new object each render is a new function each render. Building it once at
 * module scope keeps the default stable.
 */
const defaultApi = createHandoffsApi();

/**
 * One listing of handoffs, a page at a time.
 *
 * The listing is the cache's, under `handoffKeys.list(filter)`: two callers asking for the same
 * filter share one answer, and a socket push edits that answer in place through
 * `handoffCache.ts` rather than asking the host again. Each page is fetched with the cursor the
 * page before it handed back, so a row that joins or leaves between two reads shifts nothing.
 *
 * @param filter Which rows, whose, and from which end.
 * @param api The handoffs api to load from. Defaults to the signed-in one.
 * @returns The rows read so far, whether there are more, and a way to read the next page.
 */
export function useHandoffs(
  filter: HandoffFilter,
  api: HandoffsApi = defaultApi,
): {
  rows: Handoff[];
  loading: boolean;
  error: Error | null;
  hasMore: boolean;
  loadingMore: boolean;
  loadMore: () => void;
} {
  const query = useInfiniteQuery({
    queryKey: handoffKeys.list(filter),
    queryFn: ({ pageParam }) => api.list(filter, pageParam),
    initialPageParam: null as string | null,
    getNextPageParam: (last) => last.nextCursor,
  });

  const rows = useMemo(() => query.data?.pages.flatMap((page) => page.items) ?? [], [query.data]);

  const { hasNextPage, isFetchingNextPage, fetchNextPage } = query;
  const loadMore = useCallback(() => {
    if (hasNextPage && !isFetchingNextPage) void fetchNextPage();
  }, [hasNextPage, isFetchingNextPage, fetchNextPage]);

  return {
    rows,
    loading: query.isPending,
    error: query.error,
    hasMore: hasNextPage,
    loadingMore: isFetchingNextPage,
    loadMore,
  };
}
