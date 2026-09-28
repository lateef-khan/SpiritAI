import type { FetchLike } from "@/lib/apiClient";

/**
 * Who the widget is, as far as the host can tell.
 *
 * The key is kept by the page the widget is embedded in, as a cookie `embed.js` writes: a frame
 * from another site gets the least storage a browser gives. `embed.js` hands the key in as
 * `#visitor=<key>`, and the widget hands its key back out on every start, so a key the frame
 * had to find or mint reaches the cookie too. The frame's own `localStorage` keeps a copy, which
 * is all a widget opened on its own has.
 */

/** The header every public request carries the key in. */
export const VisitorHeader = "X-Spirit-Visitor";

const KeyEntry = "spirit.visitor";
const FragmentEntry = "visitor";

/** The host's rule for a key (`VisitorPrincipal.IsWellFormed`). */
const WellFormed = /^[A-Za-z0-9_-]{1,128}$/;

/**
 * Makes a key the host accepts: letters and digits only, well under its 128-character limit.
 */
function mintKey(): string {
  return crypto.randomUUID().replaceAll("-", "");
}

/** The key the embedding page handed in, or `null` when it handed in none the host accepts. */
function keyFrom(fragment: string): string | null {
  const key = new URLSearchParams(fragment.replace(/^#/, "")).get(FragmentEntry);
  return key !== null && WellFormed.test(key) ? key : null;
}

/**
 * Reads the visitor's key, minting and storing one on the first read.
 *
 * @param keyStore Where the frame keeps its copy of the key. Defaults to the page's `localStorage`.
 * @param fragment Where the embedding page hands its key in. Defaults to the frame's `location.hash`.
 * @returns The key.
 */
export function readVisitorKey(
  keyStore: Storage = localStorage,
  fragment: string = location.hash,
): string {
  const stored = keyStore.getItem(KeyEntry);
  const key = keyFrom(fragment) ?? stored ?? mintKey();

  if (key !== stored) {
    keyStore.setItem(KeyEntry, key);
  }

  return key;
}

/**
 * Hands the key to the embedding page, which keeps it as a cookie. Sent on every start, so the
 * cookie's year is counted from the last visit.
 *
 * @param key The visitor's key.
 * @param parent The embedding page. `null` for a widget opened on its own.
 */
export function shareVisitorKey(
  key: string,
  parent: Window | null = window.parent === window ? null : window.parent,
): void {
  parent?.postMessage({ source: "agentcore-widget", type: "visitor", key }, "*");
}

/**
 * A `fetch` that names the visitor on every request and changes nothing else.
 *
 * The key is read per request rather than closed over, so a key stored after this function was
 * built is still the one that goes up. The signed-in app's `authFetch` must never sit here: it
 * answers a 401 by sending the browser to the sign-in page, which a stranger's page has no use for.
 *
 * @param key Where the key comes from.
 * @param send What actually sends the request. Defaults to the page's `fetch`.
 * @returns A `fetch` the generated client and the turn stream can both use.
 */
export function visitorFetch(
  key: () => string,
  send: FetchLike = (input, init) => fetch(input, init),
): FetchLike {
  return (input, init) => {
    const headers = new Headers(input instanceof Request ? input.headers : undefined);
    new Headers(init?.headers).forEach((value, name) => headers.set(name, value));
    headers.set(VisitorHeader, key());

    return send(input, { ...init, headers });
  };
}
