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
