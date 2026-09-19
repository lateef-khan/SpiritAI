/**
 * `fetch`, with the host's price of entry attached.
 */
import { authClient } from "./authClient";
import { LOGIN_URL } from "./routes";

/**
 * The longest a fetched token is reused. A token that expires sooner is dropped sooner.
 */
const TOKEN_TTL_MS = 10 * 60 * 1000;

/** How long before a token's `exp` it is treated as dead, so a slow request cannot outlive it. */
const EXPIRY_MARGIN_MS = 30 * 1000;

/** How long a token whose `exp` cannot be read is reused. */
const UNREADABLE_TTL_MS = 60 * 1000;

let cached: { token: string; until: number } | null = null;

/** Drops the cached token, so the next request fetches a new one. */
export function forgetToken(): void {
  cached = null;
}

/** Thrown when the turn cannot be signed. Never a reason to navigate — see {@link authFetch}. */
export class NotSignedInError extends Error {}

/**
 * Reads the current access token.
 */
export async function currentToken(): Promise<string | null> {
  if (cached && Date.now() < cached.until) return cached.token;

  const token = (await fromSession()) ?? (await fromTokenEndpoint());

  if (!token) {
    cached = null;
    return null;
  }

  cached = { token, until: expiresAt(token) };
  return token;
}

/**
 * When the token stops being reusable: its own `exp` minus a margin, capped at
 * {@link TOKEN_TTL_MS}. Neon hands back whatever its session holds, which may be a token with
 * seconds left, so a fixed lifetime from the moment of fetching is not safe.
 */
function expiresAt(token: string): number {
  const now = Date.now();
  const exp = jwtExpiry(token);

  if (exp === null) return now + UNREADABLE_TTL_MS;

  return Math.min(exp - EXPIRY_MARGIN_MS, now + TOKEN_TTL_MS);
}

/** The `exp` claim in milliseconds, or null if the token is not a readable JWT. */
function jwtExpiry(token: string): number | null {
  const parts = token.split(".");
  if (parts.length !== 3) return null;

  try {
    const payload = JSON.parse(atob(parts[1].replace(/-/g, "+").replace(/_/g, "/")));
    return typeof payload.exp === "number" ? payload.exp * 1000 : null;
  } catch {
    return null;
  }
}

async function fromSession(): Promise<string | null> {
  const { data, error } = await authClient.getSession();

  if (error) {
    // Loud, because the alternative is a chat that fails with no explanation anywhere.
    console.error("[auth] could not read the session", error);
    return null;
  }

  return data?.session?.token ?? null;
}

async function fromTokenEndpoint(): Promise<string | null> {
  const { data, error } = await authClient.token();

  if (error) {
    console.error("[auth] could not mint an access token", error);
    return null;
  }

  return data?.token ?? null;
}

/** Whether Neon still considers this browser signed in. */
async function stillSignedIn(): Promise<boolean> {
  const { data } = await authClient.getSession();
  return Boolean(data?.session);
}

/**
 * Signs one request and sends it. A 401 on a cached token is retried once with a fresh one,
 * because the host's clock and the cache's clock never agree exactly on when a token died.
 */
export async function authFetch(input: RequestInfo | URL, init?: RequestInit): Promise<Response> {
  const token = await currentToken();

  if (!token) {
    if (!(await stillSignedIn())) {
      window.location.replace(LOGIN_URL);
      throw new NotSignedInError("You are signed out.");
    }

    throw new NotSignedInError("Signed in, but Neon issued no access token for this session.");
  }

  let response = await send(input, init, token);

  if (response.status === 401) {
    forgetToken();

    const fresh = await currentToken();

    if (fresh && fresh !== token) {
      response = await send(input, init, fresh);
    }
  }

  if (response.status === 401) {
    forgetToken();

    if (!(await stillSignedIn())) {
      window.location.replace(LOGIN_URL);
      throw new NotSignedInError("Your session expired.");
    }

    throw new NotSignedInError("The host refused this session's token.");
  }

  return response;
}

function send(input: RequestInfo | URL, init: RequestInit | undefined, token: string): Promise<Response> {
  const headers = new Headers(
    init?.headers ?? (input instanceof Request ? input.headers : undefined),
  );

  headers.set("Authorization", `Bearer ${token}`);

  // A Request's body can be read once, and a retry needs it again.
  const target = input instanceof Request ? input.clone() : input;

  return fetch(target, { ...init, headers });
}
