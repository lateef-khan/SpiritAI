import assert from "node:assert/strict";
import { describe, it } from "vitest";
import { createThreadsApi, reviveHistory, ThreadsPath } from "./threadsApi.ts";

/** A `fetch` that answers one canned body and records what it was asked. */
function fakeFetch(body: unknown, status = 200) {
  const calls: { url: string; init: RequestInit }[] = [];

  const send = async (url: string, init: RequestInit) => {
    calls.push({ url, init });
    return new Response(body === undefined ? null : JSON.stringify(body), {
      status,
      headers: { "Content-Type": "application/json" },
    });
  };

  return { send, calls };
}

/** One caller turn, in the shape the browser holds it. */
const Said = [{ role: "user", content: "the belt keeps slipping" }];

describe("createThreadsApi", () => {
  it("sends the words to name with the request", async () => {
    const fetch = fakeFetch(undefined);

    for await (const _ of createThreadsApi(fetch.send).title("call-1", Said)) void _;

    assert.equal(fetch.calls[0]!.init.body, JSON.stringify({ messages: Said }));
  });

  it("reads a streamed title a piece at a time", async () => {
    const calls: { url: string; init: RequestInit }[] = [];
    const send = async (url: string, init: RequestInit) => {
      calls.push({ url, init });
      return new Response(
        new ReadableStream<Uint8Array>({
          start(controller) {
            controller.enqueue(new TextEncoder().encode("Belt"));
            controller.enqueue(new TextEncoder().encode(" slips"));
            controller.close();
          },
        }),
        { status: 200, headers: { "Content-Type": "text/plain" } },
      );
    };

    const read: string[] = [];

    for await (const piece of createThreadsApi(send).title("call-1", Said)) read.push(piece);

    assert.deepEqual(read, ["Belt", " slips"]);
    assert.equal(calls[0]!.url, `${ThreadsPath}/call-1/title`);
    assert.equal(calls[0]!.init.method, "POST");
  });

  it("keeps a character whole when the host splits it across two chunks", async () => {
    // The bytes of "é", cut down the middle. A decoder that ran per chunk would emit two
    // replacement characters here rather than the letter the model actually wrote.
    const split = new TextEncoder().encode("é");

    const send = async () =>
      new Response(
        new ReadableStream<Uint8Array>({
          start(controller) {
            controller.enqueue(split.slice(0, 1));
            controller.enqueue(split.slice(1));
            controller.close();
          },
        }),
        { status: 200 },
      );

    let whole = "";

    for await (const piece of createThreadsApi(send).title("call-1", Said)) whole += piece;

    assert.equal(whole, "é");
  });

  it("refuses a title the host would not give", async () => {
    const fetch = fakeFetch(undefined, 404);

    await assert.rejects(async () => {
      for await (const _ of createThreadsApi(fetch.send).title("call-1", Said)) void _;
    }, /404/);
  });


  it("asks for a page of threads", async () => {
    const fetch = fakeFetch({ threads: [], nextCursor: null });

    await createThreadsApi(fetch.send).list();

    assert.equal(fetch.calls[0]!.url, ThreadsPath);
  });

  it("carries the cursor of a following page", async () => {
    const fetch = fakeFetch({ threads: [], nextCursor: null });

    await createThreadsApi(fetch.send).list("cursor+with//characters");

    assert.equal(
      fetch.calls[0]!.url,
      `${ThreadsPath}?after=${encodeURIComponent("cursor+with//characters")}`,
    );
  });

  it("makes a thread with no body of its own", async () => {
    const fetch = fakeFetch({ remoteId: "call-1" });

    const made = await createThreadsApi(fetch.send).create();

    assert.equal(made.remoteId, "call-1");
    assert.equal(fetch.calls[0]!.init.method, "POST");
  });

  it("sends only the fields a change names", async () => {
    const fetch = fakeFetch(undefined, 204);

    await createThreadsApi(fetch.send).patch("call-1", { title: "Belt slips" });

    assert.equal(fetch.calls[0]!.init.method, "PATCH");
    assert.equal(fetch.calls[0]!.init.body, JSON.stringify({ title: "Belt slips" }));
  });

  it("throws a readable error when the host refuses", async () => {
    const fetch = fakeFetch({ title: "No such thread." }, 404);

    await assert.rejects(
      () => createThreadsApi(fetch.send).fetch("call-1"),
      /404/,
    );
  });
});

describe("reviveHistory", () => {
  it("turns the wire's dates back into dates", () => {
    const history = reviveHistory({
      headId: "call-1:0",
      messages: [
        {
          parentId: null,
          message: {
            id: "call-1:0",
            role: "user",
            content: [{ type: "text", text: "hello" }],
            createdAt: "2026-08-31T09:00:00Z",
            metadata: { custom: {} },
          },
        },
      ],
    });

    // JSON has no date type, and assistant-ui refuses a message whose createdAt is a string.
    assert.ok(history.messages[0]!.message.createdAt instanceof Date);
  });

  it("answers with an empty conversation when the host sends none", () => {
    assert.deepEqual(reviveHistory({ messages: [] }).messages, []);
  });
});
