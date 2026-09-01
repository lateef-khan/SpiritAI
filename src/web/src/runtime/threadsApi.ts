import type { ExportedMessageRepository } from "@assistant-ui/react";
import type { WireMessage } from "./transport.ts";
import { authFetch } from "@/auth/authFetch";

/**
 * Everything the browser asks the host about threads, with no assistant-ui in sight.
 *
 * Separate from the adapter above it for the same reason `transport.ts` is separate from the
 * runtime: a URL built wrong, a cursor not escaped, a 404 read as success — each of those shows up
 * as a thread list that is quietly empty, with nothing in any log. This half is testable without a
 * React tree.
 */

/** Where the host maps the thread list. */
export const ThreadsPath = "/v1/threads";

/** Whether a thread is still listed as usual. The host spells it the same way. */
export type ThreadStatus = "regular" | "archived";

/** One thread, exactly as the host writes it. */
export type WireThread = {
  readonly remoteId: string;
  readonly status: ThreadStatus;
  readonly externalId?: string | null;
  readonly title?: string | null;
  /** ISO 8601. JSON has no date, so this is a string until the adapter revives it. */
  readonly lastMessageAt?: string | null;
  readonly custom?: Record<string, unknown> | null;
};

/** One page of the caller's threads. */
export type WireThreadPage = {
  readonly threads: readonly WireThread[];
  readonly nextCursor?: string | null;
};

/** What a thread's creation answers with. */
export type WireThreadCreated = {
  readonly remoteId: string;
  readonly externalId?: string | null;
};

/** The conversation as the host sends it, before its dates are dates. */
export type WireHistory = {
  readonly headId?: string | null;
  readonly messages: readonly {
    readonly parentId: string | null;
    readonly message: Record<string, unknown>;
  }[];
};

/** The part of `fetch` this module uses, so a test can hand it one that reaches no network. */
export type FetchLike = (url: string, init: RequestInit) => Promise<Response>;

/** Every question the browser asks about threads. */
export type ThreadsApi = {
  list(after?: string): Promise<WireThreadPage>;
  create(): Promise<WireThreadCreated>;
  fetch(remoteId: string): Promise<WireThread>;
  patch(remoteId: string, body: Record<string, unknown>): Promise<void>;
  remove(remoteId: string): Promise<void>;
  history(remoteId: string): Promise<ExportedMessageRepository>;
  /** Asks the host to name a thread from words the browser holds, reading it back as it is written. */
  title(remoteId: string, messages: readonly WireMessage[]): AsyncIterable<string>;
};

/**
 * Turns the wire's dates back into dates.
 *
 * assistant-ui refuses a message whose `createdAt` is a string, and JSON has no date type, so
 * something has to do this. Doing it here rather than in the adapter keeps every wire concern on
 * one side of the seam.
 *
 * @param raw The body the host sent.
 * @returns The same conversation, with real `Date`s on it.
 */
export function reviveHistory(raw: WireHistory): ExportedMessageRepository {
  return {
    ...(raw.headId != null ? { headId: raw.headId } : {}),
    messages: raw.messages.map((item) => ({
      parentId: item.parentId,
      message: {
        ...item.message,
        createdAt: new Date(item.message["createdAt"] as string),
      },
    })) as ExportedMessageRepository["messages"],
  };
}

/**
 * Binds the thread routes to one way of sending a request.
 *
 * @param send How a request reaches the host. Defaults to the signed-in path.
 * @returns The api the adapter runs on.
 */
export function createThreadsApi(send: FetchLike = authFetch): ThreadsApi {
  const one = (remoteId: string) => `${ThreadsPath}/${encodeURIComponent(remoteId)}`;

  return {
    list: (after) =>
      json<WireThreadPage>(
        send,
        after ? `${ThreadsPath}?after=${encodeURIComponent(after)}` : ThreadsPath,
      ),

    create: () => json<WireThreadCreated>(send, ThreadsPath, { method: "POST" }),

    fetch: (remoteId) => json<WireThread>(send, one(remoteId)),

    patch: (remoteId, body) =>
      noContent(send, one(remoteId), {
        method: "PATCH",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(body),
      }),

    remove: (remoteId) => noContent(send, one(remoteId), { method: "DELETE" }),

    history: async (remoteId) =>
      reviveHistory(await json<WireHistory>(send, `${one(remoteId)}/messages`)),

    title: (remoteId, messages) => pieces(send, `${one(remoteId)}/title`, { messages }),
  };
}

/**
 * Reads a streaming answer as text, a piece at a time.
 *
 * `TextDecoder` rather than `TextDecoderStream`: the second is absent from the test environment,
 * and `stream: true` on the first is what keeps a character split across two chunks whole. The
 * final `decode()` with no argument flushes whatever half-character was left over.
 */
async function* pieces(send: FetchLike, url: string, body: unknown): AsyncGenerator<string> {
  const response = await ok(send, url, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(body),
  });

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

/**
 * Sends one request and refuses anything that is not a success.
 *
 * The status is in the message on purpose. A thread list that silently empties is the failure this
 * whole file exists to make visible, and 404 versus 500 is the difference between "somebody else's
 * thread" and "the host is broken".
 */
async function ok(send: FetchLike, url: string, init: RequestInit): Promise<Response> {
  const response = await send(url, init);

  if (!response.ok) {
    throw new Error(`the host answered ${response.status} for ${url}.`);
  }

  return response;
}

async function json<T>(send: FetchLike, url: string, init: RequestInit = {}): Promise<T> {
  return (await ok(send, url, init)).json() as Promise<T>;
}

/** Sends one request whose answer has no body to read — the host replies 204. */
async function noContent(send: FetchLike, url: string, init: RequestInit): Promise<void> {
  await ok(send, url, init);
}
