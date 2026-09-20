/**
 * Where the newest page's cursor waits for `useOlderMessages` to pick it up.
 *
 * `ThreadHistoryAdapter.load()` and `useOlderMessages` are two different hooks with no props
 * between them — assistant-ui owns the wiring for the first, this app owns the second. `load()`
 * learns the cursor for "one page before what just loaded" a moment before `useOlderMessages`
 * needs it, and there is no render in between where a prop could carry it. A tiny per-thread
 * store, read through `useSyncExternalStore`, is the seam: `load()` resolves after
 * `useOlderMessages` has already mounted and rendered once, so a plain `Map` read at mount would
 * see nothing there yet and never look again.
 *
 * `undefined` means "no page has loaded for this thread yet" — `useOlderMessages` reads that as
 * "not ready", not as "there is no older page". `null` is the real "no older page" answer once a
 * page has actually loaded.
 */
const cursors = new Map<string, string | null>();
const listeners = new Set<() => void>();

/** Records the cursor for the page before the one `load()` just fetched. */
export function setInitialOlderCursor(remoteId: string, cursor: string | null): void {
  cursors.set(remoteId, cursor);
  for (const listener of listeners) listener();
}

/** The cursor `load()` recorded for this thread, or `undefined` if it has not run yet. */
export function getInitialOlderCursor(remoteId: string): string | null | undefined {
  return cursors.get(remoteId);
}

/** For `useSyncExternalStore`: notifies on every recorded cursor, for every thread. */
export function subscribeInitialOlderCursor(listener: () => void): () => void {
  listeners.add(listener);
  return () => listeners.delete(listener);
}
