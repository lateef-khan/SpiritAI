import { useCallback, useMemo, useState } from "react";

/**
 * Which of the two numbers a person can paste this is.
 *
 * A bare six-digit model number is deliberately not one. Too many six-digit numbers in a service
 * conversation are not model numbers, and a wrong match is worse than no match; the model number is
 * derived from a serial's first six digits, which is the rule the agent's own instructions state.
 */
export type IdentifierKind = "serial" | "order";

/** One number found in the conversation. */
export type Identifier = {
  readonly kind: IdentifierKind;
  readonly value: string;
};

/** One turn, reduced to the two things this file cares about. */
export type Said = {
  readonly role: string;
  readonly text: string;
};

/**
 * Sixteen digits, with nothing numeric either side.
 *
 * The guards matter: without them a longer run of digits would yield a serial out of its middle.
 */
const Serial = /(?<!\d)\d{16}(?!\d)/g;

/**
 * Digits, one dash, digits — the `845435-1` key.
 *
 * The guards reject a date. In `2026-09-03`, `2026-09` is followed by a dash and `09-03` is
 * preceded by one, so neither half is taken; a bare `845435-1` has neither neighbour and is.
 */
const Order = /(?<![\d-])\d+-\d+(?![\d-])/g;

/**
 * Finds every identifier in one piece of text, in the order it was written.
 *
 * @param text What the person typed.
 * @returns The identifiers, left to right, duplicates included.
 */
export function identifiersIn(text: string): Identifier[] {
  const found: { at: number; identifier: Identifier }[] = [];

  for (const match of text.matchAll(Serial)) {
    found.push({ at: match.index, identifier: { kind: "serial", value: match[0] } });
  }

  for (const match of text.matchAll(Order)) {
    found.push({ at: match.index, identifier: { kind: "order", value: match[0] } });
  }

  return found.sort((a, b) => a.at - b.at).map((entry) => entry.identifier);
}

/**
 * Reads the whole conversation as a chip row, newest first.
 *
 * Only the person's own turns are read. The agent repeating a wrong number back would otherwise
 * re-trigger the same failed lookup, and the person is the source of truth for a number anyway.
 *
 * @param said Every turn in the thread, oldest first.
 * @returns Every identifier seen, newest first, each appearing once.
 */
export function chipsOf(said: readonly Said[]): Identifier[] {
  const seen = new Map<string, Identifier>();

  for (const turn of said) {
    if (turn.role !== "user") continue;

    for (const identifier of identifiersIn(turn.text)) {
      // Deleted and re-added, so a number said again moves back to the front rather than staying
      // where it first appeared. "Newest wins" is the whole correction story.
      seen.delete(identifier.value);
      seen.set(identifier.value, identifier);
    }
  }

  return [...seen.values()].reverse();
}

/** What the panel reads. */
export type Identifiers = {
  /** Every identifier this thread has mentioned, newest first. */
  readonly chips: readonly Identifier[];
  /** The one the panel is showing, or `null` when nobody has pasted a number yet. */
  readonly selected: Identifier | null;
  /** Shows one of the chips instead of the newest. */
  readonly select: (value: string) => void;
};

/**
 * Holds which identifier the panel is showing.
 *
 * Nothing is stored that can be derived. The chip row is a function of the conversation, and the
 * only state is whether the person has clicked back to an older chip — so a correction reloads the
 * panel on its own, and a message with no number in it keeps the current unit because the newest
 * identifier has not changed.
 *
 * @param said Every turn in the thread, oldest first.
 * @returns The chip row, what is selected, and how to change it.
 */
export function useIdentifiers(said: readonly Said[]): Identifiers {
  const chips = useMemo(() => chipsOf(said), [said]);
  const newest = chips[0]?.value ?? null;

  // Keyed on the newest identifier rather than cleared in an effect: a new number arriving should
  // un-pin the panel, and doing that during render means the panel never paints the old unit first.
  const [pinned, setPinned] = useState<{ to: string | null; whenNewestWas: string | null }>({
    to: null,
    whenNewestWas: null,
  });

  const to = pinned.whenNewestWas === newest ? pinned.to : null;

  const select = useCallback(
    (value: string) => setPinned({ to: value, whenNewestWas: newest }),
    [newest],
  );

  const selected = chips.find((chip) => chip.value === to) ?? chips[0] ?? null;

  return { chips, selected, select };
}
