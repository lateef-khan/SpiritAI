import type { ChatwootSenderType } from "./wire.ts";

/** Someone a Chatwoot payload names: a message's sender, an assignee, or a typist. */
export type ChatwootPerson = {
  readonly type: ChatwootSenderType;
  readonly name: string;
  readonly available_name?: string;
};

/**
 * The name a member of staff goes by in front of the visitor: the display name they set, or else
 * the first word of their account name. `null` for anyone who is not staff. Spirit applies the same
 * rule in `ChatwootStaffName.cs`.
 *
 * `available_name` is the display name when one is set and the account name when not, so it
 * differs from `name` only when a display name is set.
 */
export function staffName(person: ChatwootPerson | null | undefined): string | null {
  if (person?.type !== "user") return null;

  const display = person.available_name?.trim();
  if (display && display !== person.name.trim()) return display;

  return person.name.trim().split(/\s+/)[0] || null;
}
