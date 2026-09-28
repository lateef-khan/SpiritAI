import { getWidgetSettings } from "@/api/sdk.gen";
import type { WidgetSettings } from "@/api/types.gen";
import { createApiClient, type FetchLike } from "@/lib/apiClient";

/** Where Chatwoot is and which inbox the widget speaks to. Spirit sends them; nothing here is secret. */
export type ChatwootSettings = WidgetSettings;

/**
 * Asks Spirit where Chatwoot is. Spirit answers 503 when it has no Chatwoot set up, and that
 * throws `HostRefusedError`.
 *
 * @param send How a request reaches Spirit. The widget's `visitorFetch`, never `authFetch`.
 * @returns The settings every other call in this module needs.
 */
export async function loadChatwootSettings(send: FetchLike): Promise<ChatwootSettings> {
  const client = createApiClient(send);
  return (await getWidgetSettings({ client, throwOnError: true })).data;
}
