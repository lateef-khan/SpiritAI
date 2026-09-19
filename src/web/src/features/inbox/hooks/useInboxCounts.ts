import { useQuery } from "@tanstack/react-query";

import { createHandoffsApi, type HandoffsApi, type HandoffView } from "../api/handoffsApi";
import { handoffKeys } from "../cache/handoffKeys";
import type { InboxCounts } from "./useHandoffs";

/** What the counts read as before the host has answered. */
const None: InboxCounts = { mine: 0, unassigned: 0, all: 0, awaitingReply: 0 };

/** See `useHandoffs.ts` for why the default api is built once, here, and not per render. */
const defaultApi = createHandoffsApi();

/**
 * How many rows one view holds, split by who holds them.
 *
 * One count on the host, cached under `handoffKeys.counts(view)`, and moved by each socket push
 * through `handoffCache.ts`: the sidebar's badges and the tab strip's read the same answer and
 * never ask for the rows themselves.
 *
 * @param view Which rows to count.
 * @param api The handoffs api to count through. Defaults to the signed-in one.
 * @returns The counts, zero until the host answers.
 */
export function useInboxCounts(view: HandoffView, api: HandoffsApi = defaultApi): InboxCounts {
  const { data } = useQuery({
    queryKey: handoffKeys.counts(view),
    queryFn: () => api.counts(view),
  });

  return data ?? None;
}
