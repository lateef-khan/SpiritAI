import type {
  ChatwootAgent,
  ChatwootConversationEvent,
  ChatwootPresence,
  ChatwootStatus,
} from "@/lib/chatwoot";

/**
 * Where the visitor's chat stands, read from Chatwoot's status and assignee.
 *
 * Chatwoot sends the visitor none of its activity messages, so the widget draws "Dana joined" and
 * "the chat was closed" itself, from the same pushes that move the desk.
 */

/** Where a chat stands, as the visitor is told: with the AI, waiting, with a person, or done. */
export type HandoffStatus = "bot" | "waiting" | "human" | "done";

/** Where the chat stands, and who from staff is on it. */
export type HandoffState = {
  readonly status: HandoffStatus;
  /** The member of staff on the chat. Never the agent bot. */
  readonly assigneeName: string | null;
  readonly staffOnline: boolean;
};

/** The desk as the widget holds it: what the visitor is told, and Chatwoot's own status. */
export type Desk = HandoffState & {
  /** `null` until the visitor has a conversation. */
  readonly chatwoot: ChatwootStatus | null;
};

/** A visitor with no conversation yet. */
export const NoChat: Desk = {
  status: "bot",
  assigneeName: null,
  staffOnline: false,
  chatwoot: null,
};

/** The member of staff an assignee names, or `null` for the agent bot and for no one. */
export function personOf(assignee: ChatwootAgent | null): string | null {
  return assignee?.type === "user" ? assignee.name : null;
}

/** Reads Chatwoot's status the way the visitor is told it. `open` is a person's, or waited for. */
export function handoffStatus(status: ChatwootStatus, person: string | null): HandoffStatus {
  if (status === "resolved") return "done";
  if (status === "pending") return "bot";
  return person === null ? "waiting" : "human";
}

/**
 * Moves the desk the way a conversation push says, and names the line to draw for it.
 *
 * A join is an `open` chat whose assignee turns into a member of staff the desk did not have. A
 * close is the move into `resolved`. Chatwoot pushes both `conversation.status_changed` and
 * `conversation.updated` for one change, so a line is drawn only for a change the desk has not
 * seen.
 *
 * @param held The desk as it stands, with the status Chatwoot last gave.
 * @param event The push.
 * @returns The new desk, and the line to draw, if any.
 */
export function moved(
  held: Desk,
  event: ChatwootConversationEvent,
): { desk: Desk; note: string | null } {
  const person = personOf(event.meta.assignee) ?? held.assigneeName;
  const desk: Desk = {
    ...held,
    chatwoot: event.status,
    assigneeName: person,
    status: handoffStatus(event.status, person),
  };

  if (event.status === "resolved" && held.chatwoot !== "resolved")
    return { desk, note: "The chat was closed." };

  const joined = personOf(event.meta.assignee);
  if (event.status === "open" && joined !== null && joined !== held.assigneeName)
    return { desk, note: `${joined} joined the chat.` };

  return { desk, note: null };
}

/** Whether any member of staff is reachable, as a presence push says. */
export function staffOnline(presence: ChatwootPresence): boolean {
  return Object.values(presence.users).some((status) => status === "online");
}
