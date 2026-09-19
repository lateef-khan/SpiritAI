import type { ExportedMessageRepository } from "@assistant-ui/react";
import { useQuery } from "@tanstack/react-query";

import { createHandoffsApi, type HandoffsApi } from "../api/handoffsApi";
import { handoffKeys } from "../cache/handoffKeys";

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
 * @param callId The handoff to load, or `null` when none is selected.
 * @param api The handoffs api to load from. Defaults to the signed-in one.
 * @returns The loaded transcript, or `null` before one is picked or while it loads, plus a way
 * to ask again.
 */
export function useHandoffMessages(
  callId: string | null,
  api: HandoffsApi = defaultApi,
): {
  history: ExportedMessageRepository | null;
  loading: boolean;
  error: string | null;
  reload: () => void;
} {
  const { data, isPending, error, refetch } = useQuery({
    queryKey: handoffKeys.messages(callId ?? ""),
    queryFn: () => api.messages(callId ?? ""),
    enabled: callId !== null,
  });

  return {
    history: callId === null ? null : (data ?? null),
    loading: callId !== null && isPending,
    error: callId === null ? null : (error?.message ?? null),
    reload: () => void refetch(),
  };
}
