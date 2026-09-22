import type { FetchLike } from "@/lib/apiClient";

/**
 * Who the widget is, as far as the host can tell.
 */

/** The header every public request carries the key in. */
export const VisitorHeader = "X-Spirit-Visitor";

const KeyEntry = "spirit.visitor";
const CallEntry = "spirit.call";

/** The key that names this visitor, and the call the widget last talked in, if any. */
export type VisitorMemory = {
  readonly key: string;
  readonly callId: string | null;
};

/**
 * Makes a key the host accepts: letters and digits only, well under its 128-character limit.
 */
function mintKey(): string {
  return crypto.randomUUID().replaceAll("-", "");
}

/**
 * Reads what the widget remembers, minting and storing a key on the first read.
 *
 * @param keyStore Where the visitor key lives. Defaults to the page's `localStorage`.
 * @param callStore Where the call id lives. Defaults to the page's `sessionStorage`.
 * @returns The key, and the call id or `null` when there is none.
 */
export function readVisitorMemory(
  keyStore: Storage = localStorage,
  callStore: Storage = sessionStorage,
): VisitorMemory {
  let key = keyStore.getItem(KeyEntry);

  if (!key) {
    key = mintKey();
    keyStore.setItem(KeyEntry, key);
  }

  return { key, callId: callStore.getItem(CallEntry) };
}

/**
 * Remembers which call the widget is talking in, or forgets it.
 *
 * @param callId The call, or `null` to forget.
 * @param storage Where the call id lives. Defaults to the page's `sessionStorage`.
 */
export function rememberCall(callId: string | null, storage: Storage = sessionStorage): void {
  if (callId === null) {
    storage.removeItem(CallEntry);
  } else {
    storage.setItem(CallEntry, callId);
  }
}

/**
 * A `fetch` that names the visitor on every request and changes nothing else.
 *
 * The key is read per request rather than closed over, so a memory made after this function was
 * built is still the one that goes up. The signed-in app's `authFetch` must never sit here: it
 * answers a 401 by sending the browser to the sign-in page, which a stranger's page has no use for.
 *
 * @param memory Where the key comes from.
 * @param send What actually sends the request. Defaults to the page's `fetch`.
 * @returns A `fetch` the generated client and the turn stream can both use.
 */
export function visitorFetch(
  memory: () => VisitorMemory,
  send: FetchLike = (input, init) => fetch(input, init),
): FetchLike {
  return (input, init) => {
    const headers = new Headers(input instanceof Request ? input.headers : undefined);
    new Headers(init?.headers).forEach((value, name) => headers.set(name, value));
    headers.set(VisitorHeader, memory().key);

    return send(input, { ...init, headers });
  };
}
