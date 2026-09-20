import type { Client } from "@/api/client";
import {
  claimHandoff,
  countHandoffs,
  finishHandoff,
  getHandoffMessages,
  listHandoffs,
  markHandoffSeen,
  replyToHandoff,
} from "@/api/sdk.gen";
import type { HandoffCounts, HandoffSummary } from "@/api/types.gen";
import { apiClient } from "@/lib/apiClient";
import { pageQuery, revivePage, type ReadHistory } from "@/lib/history";

/**
 * Everything the inbox asks the host about handoffs.
 *
 * The generated client already builds the request and parses the body; what is left for this
 * module to do is turn the wire's three date strings on a handoff back into `Date`s, and revive
 * a transcript's messages the same way `threadsApi.ts` does for a thread.
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

/** The two halves of the inbox: rows still open for a human, and rows already closed. */
export type HandoffView = "open" | "done";

/** Whose rows a listing keeps: the caller's, nobody's, or everyone's. */
export type HandoffOwner = "me" | "none" | "all";

/** Which end of a listing comes first. */
export type HandoffOrder = "oldest" | "newest";

/** What one listing walks. The same three fields name its cache entry. */
export type HandoffFilter = {
  readonly view: HandoffView;
  readonly owner: HandoffOwner;
  readonly order: HandoffOrder;
};

/** One page of a listing, and the cursor that fetches the page after it. */
export type HandoffPage = {
  readonly items: Handoff[];
  readonly nextCursor: string | null;
};

/** Every question the inbox asks about handoffs. */
export type HandoffsApi = {
  list(filter: HandoffFilter, cursor: string | null): Promise<HandoffPage>;
  counts(view: HandoffView): Promise<HandoffCounts>;
  history: ReadHistory;
  claim(callId: string): Promise<Handoff>;
  finish(callId: string): Promise<void>;
  reply(callId: string, text: string): Promise<void>;
  seen(callId: string): Promise<void>;
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
export function reviveHandoff(raw: WireHandoff): Handoff {
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
    list: async ({ view, owner, order }, cursor) => {
      const { data } = await listHandoffs({
        client,
        throwOnError: true,
        query: {
          view,
          ...(owner === "all" ? {} : { owner }),
          order,
          ...(cursor === null ? {} : { cursor }),
        },
      });
      return { items: data.items.map(reviveHandoff), nextCursor: data.nextCursor };
    },

    counts: async (view) =>
      (await countHandoffs({ client, throwOnError: true, query: { view } })).data,

    history: async (callId, before, limit) =>
      revivePage(
        (
          await getHandoffMessages({
            client,
            throwOnError: true,
            path: { conversationId: callId },
            query: pageQuery(before, limit),
          })
        ).data,
      ),

    claim: async (callId) =>
      reviveHandoff(
        (await claimHandoff({ client, throwOnError: true, path: { conversationId: callId } })).data,
      ),

    finish: async (callId) => {
      await finishHandoff({ client, throwOnError: true, path: { conversationId: callId } });
    },

    reply: async (callId, text) => {
      await replyToHandoff({
        client,
        throwOnError: true,
        path: { conversationId: callId },
        body: { text },
      });
    },

    seen: async (callId) => {
      await markHandoffSeen({ client, throwOnError: true, path: { conversationId: callId } });
    },
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
