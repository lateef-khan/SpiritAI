import { createClient, createConfig, type Client } from "./api/client";
import { client } from "./api/client.gen";

/**
 * The generated client, and the one thing that is not generated about it.
 *
 * Everything under `src/api/` is written by `openapi-ts` from `openapi/v1.json` and is not edited.
 * This module is the seam beside it: it owns how a refusal is reported, and it is where a test
 * gets a client that reaches no network.
 */

/**
 * A request the host refused, with the status it refused it with.
 *
 * The status is on the object as well as in the message. A caller telling 404 from 500 is telling
 * "no unit carries that number" from "the database is down", and those are different things to put
 * on a screen; reading it back out of the sentence would be the kind of parsing that breaks the
 * first time the sentence is reworded.
 */
export class HostRefusedError extends Error {
  /**
   * Builds the refusal.
   *
   * @param status The status the host answered with.
   * @param path What was asked for.
   */
  constructor(
    readonly status: number,
    path: string,
  ) {
    super(`the host answered ${status} for ${path}.`);
    this.name = "HostRefusedError";
  }
}

/** The part of `fetch` this app injects. `authFetch` is one, and so is a test's stand-in. */
export type FetchLike = (input: RequestInfo | URL, init?: RequestInit) => Promise<Response>;

/**
 * Makes a refused request throw, with the status in the message.
 *
 * The status is there on purpose. A thread list that silently empties is the failure this seam
 * exists to make visible, and 404 versus 500 is the difference between "somebody else's thread"
 * and "the host is broken". `throwOnError` alone would throw the response body, which for a 401 or
 * a 404 here is empty.
 *
 * @param target The client to install the check on.
 * @returns The same client.
 */
function refusalsThrow(target: Client): Client {
  target.interceptors.response.use((response, request) => {
    if (!response.ok) {
      const { pathname, search } = new URL(request.url);

      throw new HostRefusedError(response.status, `${pathname}${search}`);
    }

    return response;
  });

  return target;
}

/** The signed-in client every generated call uses by default. */
export const apiClient = refusalsThrow(client);

/**
 * Builds a client that sends its requests somewhere else.
 *
 * @param send What the client calls instead of `authFetch`.
 * @returns A client of its own, sharing nothing with {@link apiClient}.
 */
export function createApiClient(send: FetchLike): Client {
  return refusalsThrow(
    createClient(createConfig({ baseUrl: "", fetch: send, throwOnError: true })),
  );
}
