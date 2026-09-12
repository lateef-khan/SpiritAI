/**
 * How the inbox writes time. Local time throughout: a handoff is worked by whoever is signed in,
 * in their own timezone, not the visitor's.
 */

/**
 * How many whole minutes separate two instants.
 *
 * @param from The earlier instant, such as `askedAt`.
 * @param to The later instant, such as now, or `claimedAt` / `doneAt`.
 * @returns The whole minutes between them, floored.
 */
export function minutesBetween(from: Date, to: Date): number {
  return Math.floor((to.getTime() - from.getTime()) / 60_000);
}

/**
 * Writes an instant as a 24-hour clock time.
 *
 * @param at The instant to write.
 * @returns `HH:mm`, zero-padded.
 */
export function clockTime(at: Date): string {
  const hours = String(at.getHours()).padStart(2, "0");
  const minutes = String(at.getMinutes()).padStart(2, "0");

  return `${hours}:${minutes}`;
}
