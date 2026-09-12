import { listHandoffs } from "@/api/sdk.gen";
import type { HandoffSummary } from "@/api/types.gen";
import { apiClient } from "@/apiClient";
import type { Client } from "@/api/client";

/**
 * Everything the inbox asks the host about handoffs.
 *
 * The generated client already builds the request and parses the body; the one thing left for
 * this module to do is turn the wire's three date strings back into `Date`s, the same seam
 * `threadsApi.ts` keeps for threads.
 */

/** One handoff, exactly as the host writes it. */
export type WireHandoff = HandoffSummary;

/** The three states a handoff moves through. */
export type HandoffStatus = "waiting" | "human" | "done";

/** Who raised the handoff. */
export type HandoffAskedBy = "bot" | "visitor";

/** One handoff, with its dates as dates and its status and askedBy narrowed. */
export type Handoff = Omit<
  WireHandoff,
  "askedAt" | "claimedAt" | "doneAt" | "status" | "askedBy"
> & {
  status: HandoffStatus;
  askedBy: HandoffAskedBy;
  askedAt: Date;
  claimedAt: Date | null;
  doneAt: Date | null;
};

/** Every question the inbox asks about handoffs. */
export type HandoffsApi = {
  list(status: HandoffStatus): Promise<Handoff[]>;
};

/**
 * Turns a wire date into a `Date`, leaving `null` as `null`.
 *
 * @param value The wire's string, or `null` when the host never set it.
 * @returns The same instant as a `Date`, or `null`.
 */
function dateOrNull(value: string | null): Date | null {
  return value === null ? null : new Date(value);
}

/**
 * Turns one wire handoff into a `Handoff`.
 *
 * @param raw The row the host sent.
 * @returns The same row, with real `Date`s and a narrowed `status`.
 */
function reviveHandoff(raw: WireHandoff): Handoff {
  return {
    ...raw,
    // The generated type widens the wire's enum to `string`; the OpenAPI document is the source
    // of truth for its actual members, so this trusts it rather than re-validating at runtime.
    status: raw.status as HandoffStatus,
    askedBy: raw.askedBy as HandoffAskedBy,
    askedAt: new Date(raw.askedAt),
    claimedAt: dateOrNull(raw.claimedAt),
    doneAt: dateOrNull(raw.doneAt),
  };
}

/**
 * Binds the handoff routes to one client.
 *
 * @param client How a request reaches the host. Defaults to the signed-in client.
 * @returns The api the inbox runs on.
 */
export function createHandoffsApi(client: Client = apiClient): HandoffsApi {
  return {
    list: async (status) =>
      (await listHandoffs({ client, throwOnError: true, query: { status } })).data.items.map(
        reviveHandoff,
      ),
  };
}

/**
 * The key a signed-in caller's handoffs are filed under.
 *
 * Matches `SpiritAI.Threads.CallerPrincipal.KeyOf` on the server, which prefixes the same way from
 * the Neon session's `NameIdentifier` claim: `assignee.key` on a handoff is that exact string.
 *
 * @param userId The signed-in user's id, from `useSession().data.user.id`.
 * @returns The key to compare against a handoff's `assignee.key`.
 */
export function callerKeyOf(userId: string): string {
  return `user:${userId}`;
}
