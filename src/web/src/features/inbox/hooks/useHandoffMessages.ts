import type { ExportedMessageRepository } from "@assistant-ui/react";
import { useCallback, useEffect, useState } from "react";

import { createHandoffsApi, type HandoffsApi } from "../api/handoffsApi";

/**
 * The signed-in api, built once.
 *
 * A default parameter is re-evaluated on every render, so `api: HandoffsApi =
 * createHandoffsApi()` would hand the hook a new object each time — and since `api`
 * sits in the load effect's dependency array, that new object would refire the effect on every
 * render, forever. Building it once at module scope keeps the default stable across renders.
 */
const defaultApi = createHandoffsApi();

/**
 * Loads one handoff's transcript.
 *
 * `callId` of `null` means no row is selected yet, so it loads nothing and answers `history:
 * null`. A row picked while an earlier load is still in flight must not let that earlier answer
 * land after the newer one — the effect's cleanup guards against that with a cancelled flag.
 *
 * A `reload` re-runs the load for the same `callId` without clearing what is already on screen:
 * `loading` only reports true while there is no history yet for this `callId`, so a reply that
 * triggers a reload does not flash a skeleton over the transcript it is about to replace.
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
  const [attempt, setAttempt] = useState(0);
  const [result, setResult] = useState<
    { for: string; history: ExportedMessageRepository } | { for: string; error: string } | null
  >(null);

  const reload = useCallback(() => setAttempt((n) => n + 1), []);

  useEffect(() => {
    if (callId === null) return;

    let current = true;

    void (async () => {
      try {
        const history = await api.messages(callId);

        if (current) setResult({ for: callId, history });
      } catch (failure) {
        if (current) {
          setResult({
            for: callId,
            error: failure instanceof Error ? failure.message : String(failure),
          });
        }
      }
    })();

    return () => {
      current = false;
    };
  }, [callId, api, attempt]);

  const current = callId !== null && result?.for === callId ? result : null;
  const history = current && "history" in current ? current.history : null;
  const error = current && "error" in current ? current.error : null;
  const loading = callId !== null && current === null;

  return { history, loading, error, reload };
}
