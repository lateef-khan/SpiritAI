import type { ExportedMessageRepository } from "@assistant-ui/react";

import { createPublicThread, getPublicThreadMessages } from "@/api/sdk.gen";
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

/** Every question the widget asks about its thread. */
export type WidgetApi = {
  /** Makes the visitor's thread on the host, and answers the call id it was filed under. */
  createThread(): Promise<string>;
  /** One thread's whole conversation, in the shape the widget restores it from. */
  history(callId: string): Promise<ExportedMessageRepository>;
};

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
        (await getPublicThreadMessages({ client, throwOnError: true, path: { callId } })).data,
      ),
  };
}
