import type { ExportedMessageRepository } from "@assistant-ui/react";

import {
  createPublicThread,
  getHandoffState,
  getPublicThreadMessages,
  leaveEmail,
  sendVisitorMessage,
} from "@/api/sdk.gen";
import type { HandoffMessage, HandoffState as WireHandoffState } from "@/api/types.gen";
import { createApiClient, type FetchLike } from "@/lib/apiClient";
import { reviveHistory } from "@/lib/history";

/**
 * Everything the widget asks the host about its own thread, with no assistant-ui in sight.
 *
 * The requests are generated from the host's OpenAPI document; what is left here is turning the
 * wire's dates back into dates, and naming the two calls the widget makes in words the hook can
 * read. A refusal throws `HostRefusedError` with the status on it, which is how the hook tells
 * "the host forgot this call" (404) from "the host is down".
 */

/** Where a chat stands, as the visitor is told: with the bot, waiting, with a person, or done. */
export type HandoffStatus = "bot" | "waiting" | "human" | "done";

/** Where one chat stands, with its status narrowed. */
export type HandoffState = Omit<WireHandoffState, "status"> & { status: HandoffStatus };

/** One message of the human phase, as the host answers a visitor's send with it. */
export type WireHandoffMessage = HandoffMessage;

/** Every question the widget asks about its thread. */
export type WidgetApi = {
  /** Makes the visitor's thread on the host, and answers the call id it was filed under. */
  createThread(): Promise<string>;
  /** One thread's whole conversation, in the shape the widget restores it from. */
  history(callId: string): Promise<ExportedMessageRepository>;
  /** Where the chat stands: the truth after a reload or a reconnect. */
  handoffState(callId: string): Promise<HandoffState>;
  /** Leaves an email for a reply the visitor is not there to read. */
  leaveEmail(callId: string, email: string): Promise<void>;
  /** Puts the visitor's words in a chat that is waiting for, or with, a person. */
  say(callId: string, text: string): Promise<WireHandoffMessage>;
};

/**
 * Narrows the wire's status to the four the host writes. Anything else is read as `bot`: a chat
 * with no row is the safe reading, and the next state fetch corrects it.
 */
function statusOf(value: string): HandoffStatus {
  return value === "waiting" || value === "human" || value === "done" ? value : "bot";
}

/**
 * Binds the widget's routes to one way of sending a request.
 *
 * @param send How a request reaches the host. Must carry the visitor's key.
 * @returns The api the widget's runtime runs on.
 */
export function createWidgetApi(send: FetchLike): WidgetApi {
  const client = createApiClient(send);

  return {
    createThread: async () =>
      (await createPublicThread({ client, throwOnError: true })).data.remoteId,

    history: async (callId) =>
      reviveHistory(
        (await getPublicThreadMessages({ client, throwOnError: true, path: { conversationId: callId } })).data,
      ),

    handoffState: async (callId) => {
      const { data } = await getHandoffState({ client, throwOnError: true, path: { conversationId: callId } });
      return { ...data, status: statusOf(data.status) };
    },

    leaveEmail: async (callId, email) => {
      await leaveEmail({ client, throwOnError: true, path: { conversationId: callId }, body: { email } });
    },

    say: async (callId, text) =>
      (await sendVisitorMessage({ client, throwOnError: true, path: { conversationId: callId }, body: { text } }))
        .data,
  };
}
