import type { HandoffMessage, HandoffSummary } from "@/api/types.gen";

/**
 * The handoff pushes, named the way the host names them (section 6.2 of the handoff spec), and
 * the shape each one carries. The inbox and the widget hear the same events; only who they are
 * sent to differs, and the hub decides that.
 */

/** A chat joined the queue. Staff only. */
export const Waiting = "handoff.waiting";

/** A waiting chat's place in the line moved. Pushed to the chat's visitor; the widget does not show it. */
export const Queue = "handoff.queue";

/** A member of staff took a chat. Staff, and the chat's visitor. */
export const Claimed = "handoff.claimed";

/** A chat went back to the bot. Staff, and the chat's visitor. */
export const Done = "handoff.done";

/** A message landed in a chat of the human phase. Staff, and the chat's visitor. */
export const MessageCreated = "message.created";

export type WaitingPush = HandoffSummary;

export type QueuePush = { readonly callId: string; readonly position: number };

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

/** The group of one chat's visitor. A member of staff signals it. */
export function callGroup(callId: string): string {
  return `call:${callId}`;
}

/** The one signal either side sends: whether they are typing in a chat. Nothing is stored. */
export const Typing = "typing";

export type TypingSignal = { readonly callId: string; readonly on: boolean };
