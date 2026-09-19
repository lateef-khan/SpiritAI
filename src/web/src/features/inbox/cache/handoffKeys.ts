import type { HandoffFilter, HandoffView } from "../api/handoffsApi";

/**
 * The names the inbox files its cached answers under.
 *
 * Each key is a prefix of the ones below it, so one `invalidateQueries` on `all` touches every
 * handoff answer, on `lists()` every listing, and on `list(filter)` one listing. The filter is
 * spelled out field by field rather than passed as an object so that a key never depends on the
 * order the caller wrote the fields in.
 */
export const handoffKeys = {
  all: ["handoffs"] as const,
  lists: () => [...handoffKeys.all, "list"] as const,
  listsOf: (view: HandoffView) => [...handoffKeys.lists(), view] as const,
  list: ({ view, owner, order }: HandoffFilter) =>
    [...handoffKeys.listsOf(view), owner, order] as const,
  counts: (view: HandoffView) => [...handoffKeys.all, "counts", view] as const,
  messages: (callId: string) => [...handoffKeys.all, "messages", callId] as const,
};

/** Reads the filter back out of a list key, for a `setQueriesData` over many lists at once. */
export function filterOfKey(key: readonly unknown[]): HandoffFilter | null {
  if (key.length !== 5 || key[0] !== "handoffs" || key[1] !== "list") return null;
  return {
    view: key[2] as HandoffFilter["view"],
    owner: key[3] as HandoffFilter["owner"],
    order: key[4] as HandoffFilter["order"],
  };
}
