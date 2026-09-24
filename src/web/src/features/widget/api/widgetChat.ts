import type { FetchLike } from "@/lib/apiClient";
import {
  createChatwootClient,
  loadChatwootSettings,
  type ChatwootClient,
  type ChatwootContact,
  type ChatwootSettings,
} from "@/lib/chatwoot";

/**
 * The visitor, as Chatwoot knows them: where Chatwoot is, the Client API bound to the visitor's
 * key, and the contact that key finds or makes (spec section 3).
 */
export type WidgetChat = {
  readonly settings: ChatwootSettings;
  readonly client: ChatwootClient;
  readonly contact: ChatwootContact;
};

/**
 * Starts the visitor's Chatwoot side once, on the first ask. A start that fails is tried again on
 * the next ask.
 *
 * @param send How a request reaches Spirit, for the settings. The widget's `visitorFetch`.
 * @param visitorKey The visitor's key, which is the contact's `source_id`.
 * @returns How to get the started chat.
 */
export function connectWidgetChat(send: FetchLike, visitorKey: string): () => Promise<WidgetChat> {
  let started: Promise<WidgetChat> | null = null;

  const start = async (): Promise<WidgetChat> => {
    const settings = await loadChatwootSettings(send);
    const client = createChatwootClient(settings, visitorKey);
    return { settings, client, contact: await client.contact() };
  };

  return () => {
    started ??= start().catch((error: unknown) => {
      started = null;
      throw error;
    });
    return started;
  };
}
