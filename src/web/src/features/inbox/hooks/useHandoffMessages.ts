import type { ExportedMessageRepository } from "@assistant-ui/react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { useMemo } from "react";

import type { HistoryPage, OlderMessagesSource } from "@/lib/history";
import { createHandoffsApi, type HandoffsApi } from "../api/handoffsApi";
import { handoffKeys } from "../cache/handoffKeys";
import { prependOlderPage, reloadNewestPage } from "../cache/transcriptPages";

/** See `useHandoffs.ts` for why the default api is built once, here, and not per render. */
const defaultApi = createHandoffsApi();

/**
 * Loads one handoff's transcript.
 *
 * `callId` of `null` means no row is selected yet, so it loads nothing and answers `history:
 * null`. The transcript is the cache's, under `handoffKeys.messages(callId)`: a message push
 * marks it stale and it refetches while on screen, without clearing what is already drawn, so a
 * reply does not flash a skeleton over the transcript it is about to replace.
 *
 * The host answers the newest page of it. The pages before that arrive through `older`, as the
 * reader scrolls up for them, and go into the same cached entry — so a refetch puts the newest
 * page over what is held rather than in place of it, and the older pages stay on screen.
 *
 * @param callId The handoff to load, or `null` when none is selected.
 * @param api The handoffs api to load from. Defaults to the signed-in one.
 * @returns The loaded transcript, or `null` before one is picked or while it loads, plus a way
 * to ask again and where its older pages come from.
 */
export function useHandoffMessages(
  callId: string | null,
  api: HandoffsApi = defaultApi,
): {
  history: ExportedMessageRepository | null;
  loading: boolean;
  error: string | null;
  reload: () => void;
  older: OlderMessagesSource | undefined;
} {
  const cache = useQueryClient();
  const key = handoffKeys.messages(callId ?? "");

  const { data, isPending, error, refetch } = useQuery({
    queryKey: key,
    queryFn: async () => {
      const fresh = await api.history(callId ?? "");

      // Read after the fetch, not before: an older page that lands while the newest one is in
      // flight is held by then, and must not be lost under the answer.
      const held = cache.getQueryData<HistoryPage>(key);

      return held ? reloadNewestPage(held, fresh) : fresh;
    },
    enabled: callId !== null,
  });

  const initialCursor = data?.nextCursor;
  
  const older = useMemo<OlderMessagesSource | undefined>(
    () =>
      callId === null
        ? undefined
        : {
          id: callId,
          initialCursor,
          fetchPage: (before) => api.history(callId, before),
          merge: (page) =>
            cache.setQueryData<HistoryPage>(handoffKeys.messages(callId), (held) =>
              held === undefined ? held : prependOlderPage(held, page),
            ),
        },
    [api, cache, callId, initialCursor],
  );

  return {
    history: callId === null ? null : (data?.repository ?? null),
    loading: callId !== null && isPending,
    error: callId === null ? null : (error?.message ?? null),
    reload: () => void refetch(),
    older,
  };
}
