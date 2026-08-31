/**
 * `fetch`, with the host's price of entry attached.
 *
 * The conversation endpoint now refuses a request that carries no Neon token, so every call the
 * runtime makes has to go through here. It is a wrapper and not a change to transport.ts on
 * purpose: `runTurn` already takes the `fetch` it should use, and that seam is exactly this.
 */
import { authClient } from "./authClient";
import { LOGIN_URL } from "./routes";

/**
 * How long a fetched token is reused.
 *
 * Neon's access tokens live 15 minutes. Asking for a fresh one on every turn would put a round
 * trip in front of every message for no gain, and reusing one right up to its last second would
 * hand the host a token that expires mid-flight. Ten minutes sits between the two.
 */
const TOKEN_TTL_MS = 10 * 60 * 1000;

let cached: { token: string; until: number } | null = null;

/** Drops the cached token, so the next request fetches a new one. */
export function forgetToken(): void {
  cached = null;
}

async function currentToken(): Promise<string | null> {
  if (cached && Date.now() < cached.until) return cached.token;

  const { data, error } = await authClient.token();

  if (error || !data?.token) {
    cached = null;
    return null;
  }

  cached = { token: data.token, until: Date.now() + TOKEN_TTL_MS };
  return data.token;
}

/**
 * Signs one request and sends it.
 *
 * A 401 means the session died while the tab stayed open — the link expired, or somebody signed
 * out elsewhere. There is nothing the chat can do with that, so it goes back to the door.
 */
export async function authFetch(
  input: RequestInfo | URL,
  init?: RequestInit,
): Promise<Response> {
  const token = await currentToken();

  const headers = new Headers(init?.headers);
  if (token) headers.set("Authorization", `Bearer ${token}`);

  const response = await fetch(input, { ...init, headers });

  if (response.status === 401) {
    forgetToken();
    window.location.replace(LOGIN_URL);
  }

  return response;
}
