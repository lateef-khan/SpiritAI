import type { CreateClientConfig } from "./api/client.gen";

import { authFetch } from "@/auth/authFetch";

/**
 * How every generated call reaches the host.
 *
 * `authFetch` carries the token cache, the 401 retry and the redirect to the sign-in page, so
 * attaching it here gives all of that to each generated function rather than to each call site.
 *
 * The base URL is emptied. The document names `http://localhost/` because that is where the test
 * server that wrote it was listening; the app is served by the host it talks to, so every path is
 * its own origin's.
 *
 * `throwOnError` is not a preference. The client's default is to answer `{ data, error }` and never
 * throw, and the thread list adapter is built on a refusal being thrown — a thread list that
 * silently empties is the failure that whole seam exists to make visible. See `apiClient.ts` for
 * what is actually thrown.
 */
export const createClientConfig: CreateClientConfig = (config) => ({
  ...config,
  baseUrl: "",
  fetch: authFetch,
  throwOnError: true,
});
