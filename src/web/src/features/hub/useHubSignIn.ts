/**
 * The Hub's one-time sign-in links for Desk and CRM, and the loop guard: a second
 * `hub:needs-sign-in` for the same app soon after a link loaded means the link did not work, not
 * that another one is worth fetching.
 */
import { useCallback, useRef, useState } from "react";

import { openHubApp } from "@/api/sdk.gen";

import type { HubAppId } from "./hubMessages";

/** How soon a second request for the same app, after a link was requested, counts as a loop. */
const SIGN_IN_LOOP_WINDOW_MS = 10_000;

type FrameSrcUpdater = (updater: (prev: Record<string, string>) => Record<string, string>) => void;

export interface UseHubSignIn {
  readonly failed: ReadonlySet<HubAppId>;
  requestSignIn: (app: HubAppId) => void;
  retry: (app: HubAppId) => void;
}

export function useHubSignIn(setFrameSrc: FrameSrcUpdater): UseHubSignIn {
  const [failed, setFailed] = useState<Set<HubAppId>>(new Set());

  // Set the moment a request starts, not the moment it resolves: `LoginPage`'s effect can re-fire
  // (StrictMode's double-mount, a session identity change) before the first fetch is back, and a
  // guard keyed off "loaded" alone would let each of those through as its own link — signing the
  // Person out of Desk and back in, and spending its 5-per-5-minute limit, every time.
  const requestedAt = useRef<Partial<Record<HubAppId, number>>>({});
  const inFlight = useRef<Set<HubAppId>>(new Set());

  const loadLink = useCallback(
    async (app: HubAppId) => {
      inFlight.current.add(app);
      requestedAt.current[app] = Date.now();

      try {
        const { data } = await openHubApp({ throwOnError: true, path: { app } });
        setFrameSrc((prev) => ({ ...prev, [app]: data.url }));
        setFailed((prev) => {
          if (!prev.has(app)) return prev;
          const next = new Set(prev);
          next.delete(app);
          return next;
        });
      } catch {
        // No ready link, or the request failed outright — either way this attempt did not sign the
        // Person in, and the same "could not sign in" place-holder covers both.
        setFailed((prev) => (prev.has(app) ? prev : new Set(prev).add(app)));
      } finally {
        inFlight.current.delete(app);
      }
    },
    [setFrameSrc],
  );

  const requestSignIn = useCallback(
    (app: HubAppId) => {
      // Already asked, and no answer yet: this is the same request arriving twice, not a loop —
      // ignore it rather than firing a second Chatwoot sign-in for a link already on its way.
      if (inFlight.current.has(app)) return;

      const last = requestedAt.current[app];
      if (last !== undefined && Date.now() - last < SIGN_IN_LOOP_WINDOW_MS) {
        setFailed((prev) => (prev.has(app) ? prev : new Set(prev).add(app)));
        return;
      }
      void loadLink(app);
    },
    [loadLink],
  );

  const retry = useCallback(
    (app: HubAppId) => {
      delete requestedAt.current[app];
      setFailed((prev) => {
        if (!prev.has(app)) return prev;
        const next = new Set(prev);
        next.delete(app);
        return next;
      });
      void loadLink(app);
    },
    [loadLink],
  );

  return { failed, requestSignIn, retry };
}
