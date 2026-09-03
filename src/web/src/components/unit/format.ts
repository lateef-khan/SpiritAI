import type { WarrantyTerm } from "@/api/types.gen";

/** How dates are written in the panel: short, unambiguous, and the same everywhere. */
const Day = new Intl.DateTimeFormat(undefined, { year: "numeric", month: "short", day: "numeric" });

/**
 * Writes one wire date as a day.
 *
 * @param iso What the host sent, or nothing.
 * @returns The day, or an em dash when there is no date.
 */
export function asDay(iso: string | null | undefined): string {
  if (!iso) return "—";

  const when = new Date(iso);

  return Number.isNaN(when.getTime()) ? "—" : Day.format(when);
}

/**
 * Breaks a serial into groups of four.
 *
 * Not decoration: a sixteen-digit number gets read digit by digit against a sticker on a machine
 * frame, and a person loses their place in an unbroken run of sixteen.
 *
 * @param serial The serial.
 * @returns The same digits, in fours.
 */
export function asSerial(serial: string): string {
  return serial.replace(/(.{4})(?=.)/g, "$1 ");
}

/**
 * The one warranty fact the pinned header carries.
 *
 * Eight categories do not fit above the tabs, and staff ask one question anyway: is this covered.
 * Labor is the term they quote, so it is the one named when it exists; otherwise the cover that
 * lapses soonest, which is the next thing anybody will be surprised by.
 */
export type Cover = {
  readonly state: "in" | "out" | "unknown";
  readonly label: string;
  readonly detail: string;
};

/**
 * Reduces a warranty list to that one fact.
 *
 * @param terms Every category on the machine, or nothing when the section could not be read.
 * @returns What to pin above the tabs, or `null` when there is nothing true to say.
 */
export function coverOf(terms: readonly WarrantyTerm[] | null | undefined): Cover | null {
  if (!terms || terms.length === 0) return null;

  if (terms.every((term) => term.isCovered === null)) {
    return {
      state: "unknown",
      label: "Cover cannot be dated",
      detail: "No purchase date on file",
    };
  }

  const covered = terms.filter((term) => term.isCovered === true);
  const chosen = pick(covered.length > 0 ? covered : terms, covered.length > 0);

  return covered.length > 0
    ? { state: "in", label: "In warranty", detail: `${chosen.category} ends ${asDay(chosen.expiresOn)}` }
    : { state: "out", label: "Out of warranty", detail: `${chosen.category} ended ${asDay(chosen.expiresOn)}` }; // prettier-ignore
}

/** Labor when it is there, else the term whose date runs out first, or last once they all have. */
function pick(terms: readonly WarrantyTerm[], soonest: boolean): WarrantyTerm {
  const labor = terms.find((term) => term.category.toLowerCase() === "labor");

  if (labor) return labor;

  return [...terms].sort((a, b) => {
    // A term with no date can never be the one named: it says nothing a reader can act on.
    if (!a.expiresOn) return 1;
    if (!b.expiresOn) return -1;

    return soonest
      ? a.expiresOn.localeCompare(b.expiresOn)
      : b.expiresOn.localeCompare(a.expiresOn);
  })[0];
}

/**
 * Writes a category the way a sentence wants it.
 *
 * The database stores it shouted — `TREADMILL` — which reads as an error in the middle of a line
 * of prose under the machine's name.
 *
 * @param category What the host sent, or nothing.
 * @returns The category in sentence case, or `null` when there is none.
 */
export function asKind(category: string | null | undefined): string | null {
  if (!category) return null;

  return category.charAt(0).toUpperCase() + category.slice(1).toLowerCase();
}
