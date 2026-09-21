import type { ExportedMessageRepository, ThreadMessageLike } from "@assistant-ui/react";

import { alreadyHolds, reloadOver } from "@/lib/history";

/**
 * The messages the widget holds, and how pages read from the host go in among them.
 */

/** A message the widget holds, with the id the widget gave it. */
export type Held = ThreadMessageLike & { readonly id: string };

/**
 * Reads back the name the host stored one message under, when it carries one.
 *
 * A reply does, and so do the visitor's words once a person's door stored them; a bot turn's
 * words travel under the name the widget gave them, which the host keeps.
 */
export function hostMessageId(message: Held): string {
  const custom = message.metadata?.custom as { hostMessageId?: unknown } | undefined;
  return typeof custom?.hostMessageId === "string" ? custom.hostMessageId : message.id;
}

/** Whether the widget already holds the host's row of that name. */
export function holds(held: readonly Held[], messageId: string): boolean {
  return held.some((message) => hostMessageId(message) === messageId);
}

/** One page of history as the widget holds it. The host names every row; the fallback is for the type. */
export function heldFromPage(page: ExportedMessageRepository): Held[] {
  return page.messages.map(({ message }) => ({
    ...message,
    id: message.id ?? crypto.randomUUID(),
  }));
}

/**
 * Puts a freshly read newest page over what is held, as {@link reloadOver} says.
 *
 * An empty page covers nothing and replaces nothing: words typed before the host answered stay
 * on screen.
 *
 * @returns The rows to hold, and whether any held row was kept ahead of the page.
 */
export function reloadPage(
  held: readonly Held[],
  fresh: readonly Held[],
): { messages: readonly Held[]; kept: boolean } {
  if (fresh.length === 0) return { messages: held, kept: held.length > 0 };

  const { rows, kept } = reloadOver(held, fresh, hostMessageId);

  return { messages: rows, kept: kept > 0 };
}

/** Puts an older page ahead of what is held, unless it is already there. */
export function prependOlder(held: readonly Held[], page: readonly Held[]): readonly Held[] {
  return alreadyHolds(held, page, hostMessageId) ? held : [...page, ...held];
}
