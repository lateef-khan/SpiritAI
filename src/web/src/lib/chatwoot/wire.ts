/**
 * What Chatwoot sends the widget, spelled the way Chatwoot spells it. Only the fields the widget
 * reads are named; the payloads carry many more.
 *
 * Recorded examples of every shape are in `payloads/`, taken from Chatwoot 4.18.0.
 */

/** Where a conversation stands. `pending` means the AI has it; `open` means a person has it. */
export type ChatwootStatus = "pending" | "open" | "resolved" | "snoozed";

/** Who wrote a message. The agent bot is the AI; a `user` is staff. */
export type ChatwootSenderType = "contact" | "user" | "agent_bot";

/** The visitor's contact, as `POST …/contacts` answers it. */
export type ChatwootContact = {
  readonly id: number;
  readonly source_id: string;
  /** What the socket subscribes with. */
  readonly pubsub_token: string;
  readonly name: string;
};

/** A conversation, as the Client API answers it. The socket's copy has no `uuid`. */
export type ChatwootConversation = {
  /** The display id, which every Client API path names. */
  readonly id: number;
  /** What Spirit's conversation id is made from: `cw_{uuid}`. */
  readonly uuid: string;
  readonly status: ChatwootStatus;
};

/** A member of staff or the agent bot, as a conversation's assignee or a typing event names them. */
export type ChatwootAgent = {
  readonly id: number;
  readonly name: string;
  readonly available_name?: string;
  readonly type: "user" | "agent_bot";
  readonly availability_status?: "online" | "busy" | "offline";
};

/** One message, from the Client API or from the socket. */
export type ChatwootMessage = {
  readonly id: number;
  readonly content: string | null;
  /** 0 from the visitor, 1 to the visitor, 2 activity, 3 template. */
  readonly message_type: number;
  readonly content_type: string;
  /** Seconds since the epoch. */
  readonly created_at: number;
  readonly conversation_id: number;
  readonly sender?: {
    readonly id: number;
    readonly name: string;
    readonly available_name?: string;
    readonly type: ChatwootSenderType;
  };
};

/** A conversation as the socket pushes it on `conversation.updated` and `conversation.status_changed`. */
export type ChatwootConversationEvent = {
  readonly id: number;
  readonly status: ChatwootStatus;
  /** The agent bot while the AI has it, a `user` once staff take it, `null` when no one does. */
  readonly meta: { readonly assignee: ChatwootAgent | null };
};

/** A member of staff starting or stopping typing. */
export type ChatwootTyping = {
  readonly conversation: { readonly id: number };
  readonly user: ChatwootAgent;
  readonly is_private: boolean;
};

/** Who among staff is reachable, by user id. A user who is not listed is offline. */
export type ChatwootPresence = {
  readonly account_id: number;
  readonly users: Readonly<Record<string, "online" | "busy" | "offline">>;
};

/** Every socket event the widget hears, and what each carries. */
export type ChatwootEvents = {
  "message.created": ChatwootMessage;
  "conversation.updated": ChatwootConversationEvent;
  "conversation.status_changed": ChatwootConversationEvent;
  "conversation.typing_on": ChatwootTyping;
  "conversation.typing_off": ChatwootTyping;
  "presence.update": ChatwootPresence;
};
