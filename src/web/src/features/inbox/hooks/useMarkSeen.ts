import { useEffect } from "react";
import { useQueryClient } from "@tanstack/react-query";

import { createHandoffsApi, type Handoff, type HandoffsApi } from "../api/handoffsApi";
import { applySeen } from "../cache/handoffCache";

/** See `useHandoffs.ts` for why the default api is built once, here, and not per render. */
const defaultApi = createHandoffsApi();

/**
 * Takes the unread dot off the chat on screen, and tells the host.
 *
 * Fires when a chat with unread visitor lines is picked, and again each time a visitor line lands
 * in it while it is on screen: a push flips the row back to unread, and this flips it forward. The
 * cache is patched before the host answers, so the dot goes at once, and a line that arrives in
 * between marks the row unread again and is marked seen in its turn rather than lost.
 *
 * @param handoff The chat on screen, or `null` when none is.
 * @param api The handoffs api to tell. Defaults to the signed-in one.
 */
export function useMarkSeen(handoff: Handoff | null, api: HandoffsApi = defaultApi): void {
  const cache = useQueryClient();
  const callId = handoff?.callId ?? null;
  const unread = handoff?.unread ?? false;

  useEffect(() => {
    if (callId === null || !unread) return;
    applySeen(cache, callId);
    // A refused mark leaves the dot off until the next listing, which reads the host's truth.
    void api.seen(callId).catch(() => {});
  }, [api, cache, callId, unread]);
}
