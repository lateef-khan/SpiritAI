import { useCallback, useRef, useState } from "react";

import { HostRefusedError } from "@/lib/apiClient";

import { createHandoffsApi, type Handoff, type HandoffsApi } from "../api/handoffsApi";

/**
 * The signed-in api, built once.
 *
 * Matches the reasoning in `useHandoffs.ts`: a default parameter is a new object on every render,
 * and that object sitting in a hook's closure would make every render's `take`/`finish` a new
 * function identity for no reason. Building it once at module scope keeps it stable.
 */
const defaultApi = createHandoffsApi();

/**
 * Runs a `take` or a `done` on one handoff, tracking whether a call is in flight and what the
 * last one failed with.
 *
 * A row's Take and Done buttons both come through here rather than calling the api directly, so
 * that a double-click while a claim is already on the wire is a no-op instead of a second race
 * with whoever else is also reaching for that row, and so that a 409's "somebody already has this
 * chat" reaches the screen as the sentence the host wrote for it, not `HostRefusedError`'s own
 * "the host answered 409 for /v1/handoff/call-1/claim.".
 *
 * @param api The handoffs api to act through. Defaults to the signed-in one.
 * @returns `take` and `finish`, plus whether one is in flight and how the last one failed.
 */
export function useHandoffActions(api: HandoffsApi = defaultApi): {
  take(callId: string): Promise<Handoff | null>;
  finish(callId: string): Promise<boolean>;
  busy: boolean;
  error: string | null;
} {
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const inFlight = useRef(false);

  const run = useCallback(async <T>(action: () => Promise<T>, fallback: T): Promise<T> => {
    if (inFlight.current) return fallback;

    inFlight.current = true;
    setBusy(true);
    setError(null);

    try {
      return await action();
    } catch (failure) {
      setError(
        failure instanceof HostRefusedError
          ? (failure.title ?? failure.message)
          : failure instanceof Error
            ? failure.message
            : String(failure),
      );
      return fallback;
    } finally {
      inFlight.current = false;
      setBusy(false);
    }
  }, []);

  const take = useCallback((callId: string) => run(() => api.claim(callId), null), [api, run]);

  const finish = useCallback(
    (callId: string) => run(() => api.finish(callId).then(() => true), false),
    [api, run],
  );

  return { take, finish, busy, error };
}
