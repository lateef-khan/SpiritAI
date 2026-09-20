import { useAuiState } from "@assistant-ui/react";
import { useMemo, useSyncExternalStore } from "react";
import { createThreadsApi, type ThreadsApi } from "./api/threadsApi.ts";
import { getInitialOlderCursor, subscribeInitialOlderCursor } from "./historyCursors.ts";
import type { OlderMessagesSource } from "@/lib/history";

let sharedApi: ThreadsApi | undefined;

/**
 * The older pages of the open thread, for the signed-in app.
 *
 * The thread list names the open thread, `useServerHistory`'s `load()` records where the page
 * before the newest one starts, and the signed-in route answers the pages; nothing here is more
 * than those three bound together. Merging is left to the loader's default, assistant-ui's own
 * `import()`, because that is where a thread list runtime keeps its messages.
 *
 * @param api How the host is asked. Defaults to the signed-in path.
 * @returns The source to hand to `Thread`, or `undefined` before a thread is open.
 */
export function useThreadListOlderMessages(api?: ThreadsApi): OlderMessagesSource | undefined {
  const remoteId = useAuiState((s) => s.threadListItem.remoteId);
  const resolvedApi = api ?? (sharedApi ??= createThreadsApi());

  const initialCursor = useSyncExternalStore(subscribeInitialOlderCursor, () =>
    remoteId ? getInitialOlderCursor(remoteId) : undefined,
  );

  return useMemo(
    () =>
      remoteId
        ? {
            id: remoteId,
            initialCursor,
            fetchPage: (before) => resolvedApi.history(remoteId, before),
          }
        : undefined,
    [remoteId, initialCursor, resolvedApi],
  );
}
