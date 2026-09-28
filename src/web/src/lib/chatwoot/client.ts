import type { FetchLike } from "@/lib/apiClient";
import type { ChatwootSettings } from "./settings.ts";
import type { ChatwootContact, ChatwootConversation, ChatwootMessage } from "./wire.ts";

/**
 * The widget's calls to Chatwoot's Client API, as the visitor.
 *
 * The visitor key is the contact's `source_id` (spec section 3), so every path names it. The
 * Client API needs no token: Chatwoot finds the contact by the key, and lists only the
 * conversations this key made. Chatwoot allows CORS from any origin on these routes.
 */

/** How many messages Chatwoot answers per page. A shorter page is the oldest one. */
export const ChatwootPageSize = 20;

/** A request Chatwoot refused, with the status it refused it with. */
export class ChatwootRefusedError extends Error {
  /**
   * @param status The status Chatwoot answered with.
   * @param path What was asked for.
   */
  constructor(
    readonly status: number,
    path: string,
  ) {
    super(`Chatwoot answered ${status} for ${path}.`);
    this.name = "ChatwootRefusedError";
  }
}

/** Every call the widget makes to the Client API, for one visitor. */
export type ChatwootClient = {
  /** Makes the visitor's contact, or finds it when the key already has one. */
  contact(): Promise<ChatwootContact>;
  /** Every conversation this visitor's key made. */
  conversations(): Promise<ChatwootConversation[]>;
  /** Makes a conversation. With the agent bot on the inbox, it starts `pending`. */
  createConversation(): Promise<ChatwootConversation>;
  /** Posts the visitor's words, and answers the message Chatwoot made of them. */
  post(conversationId: number, content: string): Promise<ChatwootMessage>;
  /**
   * The newest {@link ChatwootPageSize} messages, oldest first. With `before`, the page just
   * older than that message id.
   */
  messages(conversationId: number, before?: number): Promise<ChatwootMessage[]>;
  /** Tells staff the visitor started or stopped typing. */
  typing(conversationId: number, on: boolean): Promise<void>;
};

/**
 * Binds the Client API to one visitor.
 *
 * @param settings Where Chatwoot is and which inbox to speak to.
 * @param visitorKey The visitor's key, which is the contact's `source_id`.
 * @param send How a request reaches Chatwoot. Defaults to the page's `fetch`.
 * @returns The calls.
 */
export function createChatwootClient(
  settings: ChatwootSettings,
  visitorKey: string,
  send: FetchLike = (input, init) => fetch(input, init),
): ChatwootClient {
  const inbox = `${settings.chatwootBaseUrl.replace(/\/+$/, "")}/public/api/v1/inboxes/${encodeURIComponent(settings.chatwootInboxIdentifier)}`;
  const contact = `${inbox}/contacts/${encodeURIComponent(visitorKey)}`;
  const conversation = (id: number) => `${contact}/conversations/${id}`;

  async function call<T>(url: string, method: "GET" | "POST", body?: object): Promise<T> {
    const response = await send(url, {
      method,
      ...(body
        ? { headers: { "Content-Type": "application/json" }, body: JSON.stringify(body) }
        : {}),
    });

    if (!response.ok) throw new ChatwootRefusedError(response.status, new URL(url).pathname);

    // toggle_typing answers 200 with an empty body.
    const text = await response.text();
    return (text ? JSON.parse(text) : undefined) as T;
  }

  return {
    contact: () => call(`${inbox}/contacts`, "POST", { source_id: visitorKey }),
    conversations: () => call(`${contact}/conversations`, "GET"),
    createConversation: () => call(`${contact}/conversations`, "POST", {}),
    post: (conversationId, content) =>
      call(`${conversation(conversationId)}/messages`, "POST", { content }),
    messages: (conversationId, before) =>
      call(
        `${conversation(conversationId)}/messages${before === undefined ? "" : `?before=${before}`}`,
        "GET",
      ),
    typing: (conversationId, on) =>
      call(`${conversation(conversationId)}/toggle_typing`, "POST", {
        typing_status: on ? "on" : "off",
      }),
  };
}
