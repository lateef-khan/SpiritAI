import type { HandoffMessage } from "@/api/types.gen";

/**
 * The handoff pushes the widget hears, named the way the host names them (section 6.2 of the
 * handoff spec), and the shape each one carries. Staff work in Chatwoot; the host relays what
 * they do to the chat's visitor.
 */

/** A member of staff took a chat. To the chat's visitor. */
export const Claimed = "handoff.claimed";

/** A chat went back to the bot. To the chat's visitor. */
export const Done = "handoff.done";

/** A message landed in a chat of the human phase. To the chat's visitor. */
export const MessageCreated = "message.created";

export type ClaimedPush = {
  readonly callId: string;
  readonly assignee: { readonly key: string; readonly name: string };
};

export type DonePush = { readonly callId: string };

export type MessagePush = HandoffMessage;

/** The kind the hub counts staff under in a `presence` push. */
export const StaffKind = "staff";

/** The group every member of staff on a socket is in. A visitor signals it. */
export const StaffGroup = "handoff:staff";

/** The one signal either side sends: whether they are typing in a chat. Nothing is stored. */
export const Typing = "typing";

export type TypingSignal = { readonly callId: string; readonly on: boolean };
