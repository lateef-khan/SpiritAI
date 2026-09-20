/**
 * How a page read from the host goes in among the rows a reader already holds.
 *
 * Every surface that pages a conversation holds its rows somewhere of its own — the inbox's
 * query cache, a store the widget owns — and each holds a different row shape. What they do
 * with a page is the same, so it lives once, over any row with an `idOf`.
 */

/**
 * Puts a freshly read newest page over what is held.
 *
 * The page replaces every held row from its first one on: a reload is how a reader learns what
 * the host has that it does not, so what the page covers is the host's word. What is held from
 * *before* the page stays — those are older pages the reader scrolled up for, and the newest
 * page does not reach back to them. A page that shares no row with what is held is the whole
 * conversation as far as the reader knows, and replaces it outright.
 *
 * @param held The rows the reader holds, oldest first.
 * @param fresh The newest page, oldest first. Not empty: what an empty page means is the caller's call.
 * @param idOf The host's name for a row, on either side.
 * @returns The rows to hold, and how many held rows were kept ahead of the page.
 */
export function reloadOver<T>(
  held: readonly T[],
  fresh: readonly T[],
  idOf: (row: T) => string,
): { rows: readonly T[]; kept: number } {
  const first = fresh[0];

  if (first === undefined) return { rows: fresh, kept: 0 };

  const firstId = idOf(first);
  const at = held.findIndex((row) => idOf(row) === firstId);

  if (at <= 0) return { rows: fresh, kept: 0 };

  return { rows: [...held.slice(0, at), ...fresh], kept: at };
}

/**
 * Whether an older page is already among the rows held.
 *
 * The loader may hand the same page over twice — it forgets what it merged when the list
 * unmounts, and the reader's store does not — so a page whose first row is held goes nowhere.
 *
 * @param held The rows the reader holds.
 * @param page The older page, oldest first.
 * @param idOf The host's name for a row, on either side.
 * @returns `true` for an empty page too: there is nothing in it to put in.
 */
export function alreadyHolds<T>(
  held: readonly T[],
  page: readonly T[],
  idOf: (row: T) => string,
): boolean {
  const first = page[0];

  if (first === undefined) return true;

  const firstId = idOf(first);

  return held.some((row) => idOf(row) === firstId);
}
