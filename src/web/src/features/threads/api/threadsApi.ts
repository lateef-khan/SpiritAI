import type { WireMessage } from "../transport.ts";
import {
  createThread,
  deleteThread,
  getThread,
  getThreadMessages,
  listThreads,
  updateThread,
} from "@/api/sdk.gen";
import type { ThreadCreated, ThreadPage, ThreadStatus, ThreadSummary } from "@/api/types.gen";
import { apiClient, createApiClient, type FetchLike } from "@/lib/apiClient";
import { pageQuery, revivePage, type ReadHistory } from "@/lib/history";

/**
 * Everything the browser asks the host about threads, with no assistant-ui in sight.
 *
 * The requests themselves are generated — `src/api/` is written by `openapi-ts` from the document
 * the host emits, so a URL built wrong or a cursor left unescaped is no longer a thing that can
 * happen here. What is left is the two jobs a generated client cannot do: reading a text stream a
 * piece at a time, and turning the wire's dates back into dates.
 */

/** Where the host maps the thread list. */
export const ThreadsPath = "/v1/threads";

export type { FetchLike, ThreadStatus };

/** One thread, exactly as the host writes it. */
export type WireThread = ThreadSummary;

/** One page of the caller's threads. */
export type WireThreadPage = ThreadPage;

/** What a thread's creation answers with. */
export type WireThreadCreated = ThreadCreated;

/** Every question the browser asks about threads. */
export type ThreadsApi = {
  list(after?: string): Promise<WireThreadPage>;
  create(): Promise<WireThreadCreated>;
  fetch(remoteId: string): Promise<WireThread>;
  patch(remoteId: string, body: Record<string, unknown>): Promise<void>;
  remove(remoteId: string): Promise<void>;
  history: ReadHistory;
  /** Asks the host to name a thread from words the browser holds, reading it back as it is written. */
  title(remoteId: string, messages: readonly WireMessage[]): AsyncIterable<string>;
};

/**
 * Binds the thread routes to one way of sending a request.
 *
 * @param send How a request reaches the host. Defaults to the signed-in path.
 * @returns The api the adapter runs on.
 */
export function createThreadsApi(send?: FetchLike): ThreadsApi {
  const client = send ? createApiClient(send) : apiClient;
  const request = send ?? apiClient.getConfig().fetch!;

  return {
    list: async (after) =>
      (
        await listThreads({
          client,
          throwOnError: true,
          ...(after ? { query: { after } } : {}),
        })
      ).data,

    create: async () => (await createThread({ client, throwOnError: true })).data,

    fetch: async (remoteId) =>
      (await getThread({ client, throwOnError: true, path: { remoteId } })).data,

    patch: async (remoteId, body) => {
      await updateThread({ client, throwOnError: true, path: { remoteId }, body });
    },

    remove: async (remoteId) => {
      await deleteThread({ client, throwOnError: true, path: { remoteId } });
    },

    history: async (remoteId, before, limit) =>
      revivePage(
        (
          await getThreadMessages({
            client,
            throwOnError: true,
            path: { remoteId },
            query: pageQuery(before, limit),
          })
        ).data,
      ),

    title: (remoteId, messages) =>
      pieces(request, `${ThreadsPath}/${encodeURIComponent(remoteId)}/title`, { messages }),
  };
}

/**
 * Reads a streaming answer as text, a piece at a time.
 *
 * Hand-written, and the reason is in the document rather than here: this route is marked
 * `ExcludeFromDescription`, because a generated call parses one whole body and hands back an
 * object, which is the one thing this caller must not do.
 *
 * `TextDecoder` rather than `TextDecoderStream`: the second is absent from the test environment,
 * and `stream: true` on the first is what keeps a character split across two chunks whole. The
 * final `decode()` with no argument flushes whatever half-character was left over.
 */
async function* pieces(send: FetchLike, url: string, body: unknown): AsyncGenerator<string> {
  const response = await send(url, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(body),
  });

  if (!response.ok) {
    throw new Error(`the host answered ${response.status} for ${url}.`);
  }

  if (!response.body) return;

  const reader = response.body.getReader();
  const decoder = new TextDecoder();

  try {
    for (;;) {
      const { done, value } = await reader.read();

      if (done) break;

      const piece = decoder.decode(value, { stream: true });

      if (piece) yield piece;
    }
  } finally {
    reader.releaseLock();
  }

  const rest = decoder.decode();

  if (rest) yield rest;
}
