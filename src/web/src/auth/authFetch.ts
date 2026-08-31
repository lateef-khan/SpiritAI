/**
 * `fetch`, with the host's price of entry attached.
 */
import { authClient } from "./authClient";
import { LOGIN_URL } from "./routes";

/**
 * How long a fetched token is reused.
 */
const TOKEN_TTL_MS = 10 * 60 * 1000;

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
async function currentToken(): Promise<string | null> {
  if (cached && Date.now() < cached.until) return cached.token;

  const token = (await fromSession()) ?? (await fromTokenEndpoint());

  if (!token) {
    cached = null;
    return null;
  }

  cached = { token, until: Date.now() + TOKEN_TTL_MS };
  return token;
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
 * Signs one request and sends it.
 */
export async function authFetch(
  input: RequestInfo | URL,
  init?: RequestInit,
): Promise<Response> {
  const token = await currentToken();

  if (!token) {
    if (!(await stillSignedIn())) {
      window.location.replace(LOGIN_URL);
      throw new NotSignedInError("You are signed out.");
    }

    throw new NotSignedInError(
      "Signed in, but Neon issued no access token for this session.",
    );
  }

  const headers = new Headers(init?.headers);
  headers.set("Authorization", `Bearer ${token}`);

  const response = await fetch(input, { ...init, headers });

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
