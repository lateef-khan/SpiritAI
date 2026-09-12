import { useCallback, useEffect, useState } from "react";

import { apiClient } from "@/apiClient";

import { createHandoffsApi, type Handoff, type HandoffsApi } from "../api/handoffsApi";

/**
 * The inbox's two tabs of dates: rows still open for a human, and rows already closed.
 */
export type InboxView = "open" | "done";

/** How the current view's rows are split by who is on them. */
export type InboxTab = "mine" | "unassigned" | "all";

/** How many rows each tab would show, for the tab strip's badges. */
export type InboxCounts = Record<InboxTab, number>;

/**
 * The signed-in api, built once.
 *
 * A default parameter is re-evaluated on every render, so `api: HandoffsApi =
 * createHandoffsApi(apiClient)` would hand the hook a new object each time — and since `api`
 * sits in the load effect's dependency array, that new object would refire the effect on every
 * render, forever. Building it once at module scope keeps the default stable across renders.
 */
const defaultApi = createHandoffsApi(apiClient);

/**
 * Narrows a view's rows to one tab.
 *
 * @param rows The rows a view has loaded.
 * @param tab Which slice of them to keep.
 * @param meKey The signed-in caller's key, as `callerKeyOf` builds it.
 * @returns The rows that belong on `tab`.
 */
export function filterHandoffs(rows: Handoff[], tab: InboxTab, meKey: string): Handoff[] {
  switch (tab) {
    case "mine":
      return rows.filter((row) => row.assignee?.key === meKey);
    case "unassigned":
      return rows.filter((row) => row.assignee == null);
    case "all":
      return rows;
  }
}

/**
 * Counts a view's rows for each tab, for the tab strip's badges.
 *
 * @param rows The rows a view has loaded.
 * @param meKey The signed-in caller's key, as `callerKeyOf` builds it.
 * @returns How many rows each tab would show.
 */
export function countHandoffs(rows: Handoff[], meKey: string): InboxCounts {
  return {
    mine: filterHandoffs(rows, "mine", meKey).length,
    unassigned: filterHandoffs(rows, "unassigned", meKey).length,
    all: rows.length,
  };
}

/**
 * Loads one view's worth of handoffs, and counts them per tab.
 *
 * `open` reads `waiting` and `human` together, because both still want a human's attention and
 * the inbox draws them in one list. `done` reads on its own, newest first, since a closed
 * handoff is read newest-first the way a sent-mail folder is.
 *
 * @param view Which half of the inbox to load.
 * @param meKey The signed-in caller's key, as `callerKeyOf` builds it.
 * @param api The handoffs api to load from. Defaults to the signed-in one.
 * @returns The loaded rows, their per-tab counts, and a way to ask again.
 */
export function useHandoffs(
  view: InboxView,
  meKey: string,
  api: HandoffsApi = defaultApi,
): {
  rows: Handoff[];
  counts: InboxCounts;
  loading: boolean;
  error: Error | null;
  reload: () => void;
} {
  const [attempt, setAttempt] = useState(0);
  const [result, setResult] = useState<
    { to: string; rows: Handoff[] } | { to: string; error: Error } | null
  >(null);

  const asked = `${view}:${attempt}`;
  const reload = useCallback(() => setAttempt((n) => n + 1), []);

  useEffect(() => {
    // Guards which answer lands. A `reload` mid-flight can leave the earlier request finishing
    // after the newer one, and it must not overwrite what the newer one already set.
    let current = true;

    void (async () => {
      try {
        const loaded =
          view === "open"
            ? (await Promise.all([api.list("waiting"), api.list("human")]))
                .flat()
                .sort((a, b) => a.askedAt.getTime() - b.askedAt.getTime())
            : (await api.list("done")).sort(
                (a, b) => (b.doneAt?.getTime() ?? 0) - (a.doneAt?.getTime() ?? 0),
              );

        if (current) setResult({ to: asked, rows: loaded });
      } catch (failure) {
        if (current) {
          setResult({
            to: asked,
            error: failure instanceof Error ? failure : new Error(String(failure)),
          });
        }
      }
    })();

    return () => {
      current = false;
    };
  }, [view, api, asked]);

  const current = result?.to === asked ? result : null;
  const rows = current && "rows" in current ? current.rows : [];
  const error = current && "error" in current ? current.error : null;
  const loading = current === null;

  return { rows, counts: countHandoffs(rows, meKey), loading, error, reload };
}
