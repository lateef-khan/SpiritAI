import assert from "node:assert/strict";
import { describe, expect, it, test } from "vitest";
import {
  ContinuationNotFound,
  ConversationField,
  foldSource,
  readEvent,
  runTurn,
  splitEvents,
  wireMessages,
  type FetchLike,
  type Session,
  type StreamChunk,
} from "./transport.ts";

/**
 * What the browser does over the wire.
 *
 * No network and no browser: every test drives `runTurn` with a fetch of its own, so the failures
 * these cover — a half-read event, a conversation id kept past the end of its call, a 404 for a
 * call the host forgot — are reproduced exactly rather than waited for.
 */

// -------------------------------------------------------------------------------------------------
// Fakes.
// -------------------------------------------------------------------------------------------------

/** One recorded request. */
type Sent = { conversation: string | null; body: unknown };

/** Builds a response whose body arrives in exactly the pieces given. */
function streaming(pieces: string[], headers: Record<string, string> = {}): Response {
  const body = new ReadableStream<Uint8Array>({
    start(controller) {
      const encoder = new TextEncoder();
      for (const piece of pieces) {
        controller.enqueue(encoder.encode(piece));
      }
      controller.close();
    },
  });

  return new Response(body, { status: 200, headers });
}

/** Builds one refusal in the shape the endpoint writes. */
function refusal(status: number, message: string, code?: string): Response {
  return new Response(JSON.stringify({ error: { message, code } }), {
    status,
    headers: { "Content-Type": "application/json" },
  });
}

/** A fetch that answers from a script and records what it was asked. */
function scripted(responses: Response[]): { fetch: FetchLike; sent: Sent[] } {
  const sent: Sent[] = [];
  let index = 0;

  const fetch: FetchLike = (_url, init) => {
    const body = JSON.parse(String(init.body)) as Record<string, unknown>;
    sent.push({
      conversation:
        typeof body[ConversationField] === "string" ? (body[ConversationField] as string) : null,
      body,
    });

    const response = responses[index++];
    assert.ok(response, `the test scripted ${responses.length} answers and got one more request.`);
    return Promise.resolve(response);
  };

  return { fetch, sent };
}

/** One data event. */
function event(payload: unknown, eventName?: string): string {
  return `${eventName ? `event: ${eventName}\n` : ""}data: ${JSON.stringify(payload)}\n\n`;
}

/** One text frame. */
function delta(text: string): string {
  return event(
    {
      type: "response.output_text.delta",
      delta: text,
    },
    "response.output_text.delta",
  );
}

/** The created event naming the conversation. */
function created(conversation: string): string {
  return event(
    {
      type: "response.created",
      response: { id: "resp_1", conversation: { id: conversation } },
    },
    "response.created",
  );
}

/** The closing event carrying the turn facts. */
function completed(metadata: Record<string, string> = {}, conversation = "conv_1"): string {
  return event(
    {
      type: "response.completed",
      response: { id: "resp_1", conversation: { id: conversation }, metadata },
    },
    "response.completed",
  );
}

/** Runs one turn to the end and collects every yield. */
async function collect(
  responses: Response[],
  session: Session,
  messages: { role: string; content: string }[] = [{ role: "user", content: "hi" }],
  threadId?: string,
): Promise<{ yields: string[]; sent: Sent[] }> {
  const { fetch, sent } = scripted(responses);
  const yields: string[] = [];

  for await (const state of runTurn({
    endpoint: "/v1/responses",
    session,
    input: wireMessages(messages),
    abortSignal: new AbortController().signal,
    fetch,
    ...(threadId ? { threadId } : {}),
  })) {
    yields.push(state.text);
  }

  return { yields, sent };
}

// -------------------------------------------------------------------------------------------------
// The event reader. These are the pieces that a chunk boundary can break.
// -------------------------------------------------------------------------------------------------

test("splitEvents keeps an incomplete event back for the next read", () => {
  const { events, rest } = splitEvents("data: one\n\ndata: tw");

  assert.deepEqual(events, ["data: one"]);
  assert.equal(rest, "data: tw");
});

test("splitEvents returns nothing when no event is complete yet", () => {
  const { events, rest } = splitEvents("data: par");

  assert.deepEqual(events, []);
  assert.equal(rest, "data: par");
});

test("readEvent parses every data line of one event", () => {
  const chunks = readEvent(
    'event: response.output_text.delta\ndata: {"type":"response.output_text.delta","delta":"hi"}',
  );

  assert.deepEqual(chunks, [{ type: "response.output_text.delta", delta: "hi" }]);
});

test("readEvent ignores a line that is not data", () => {
  assert.deepEqual(readEvent(": keep-alive"), []);
});

test("readEvent skips half a JSON body split across reads", () => {
  assert.deepEqual(readEvent('data: {"type":"response.output_text.del'), []);
});

// -------------------------------------------------------------------------------------------------
// The request body.
// -------------------------------------------------------------------------------------------------

test("wireMessages sends the last user text", () => {
  assert.equal(
    wireMessages([
      { role: "user", content: "first" },
      { role: "assistant", content: "answer" },
      { role: "user", content: "second" },
    ]),
    "second",
  );
});

test("wireMessages answers empty when no user text remains", () => {
  assert.equal(wireMessages([{ role: "assistant", content: "answer" }]), "");
});

// -------------------------------------------------------------------------------------------------
// The conversation, which is the whole reason this file exists.
// -------------------------------------------------------------------------------------------------

test("the first turn names no conversation and keeps the one the stream mints", async () => {
  const session: Session = { current: null };

  const { sent } = await collect(
    [streaming([created("conv_1"), delta("hi"), completed()])],
    session,
  );

  assert.equal(
    sent[0]!.conversation,
    null,
    "a first turn must not name a call that does not exist.",
  );
  assert.equal(session.current, "conv_1");
});

test("the next turn sends the conversation back", async () => {
  const session: Session = { current: "conv_1" };

  const { sent } = await collect(
    [streaming([created("conv_1"), delta("again"), completed()])],
    session,
  );

  assert.equal(sent[0]!.conversation, "conv_1");
  const body = sent[0]!.body as Record<string, unknown>;
  assert.equal(body["input"], "hi");
  assert.equal(body["stream"], true);
  assert.ok(body["agentcore"], "the dialect member opts the stream into drawings and tools.");
});

test("a terminal turn lets the call go, so the next one opens a new call", async () => {
  // Holding the id past the end of a call answers the next message with a 409, and the UI would
  // show a chat that refuses everything from then on.
  const session: Session = { current: null };

  await collect(
    [
      streaming(
        [created("conv_1"), delta("goodbye"), completed({ is_terminal: "true" }, "conv_1")],
        {},
      ),
    ],
    session,
  );

  assert.equal(session.current, null);
});

test("a non-terminal turn keeps the call", async () => {
  const session: Session = { current: null };

  await collect(
    [
      streaming(
        [created("conv_1"), delta("still here"), completed({ is_terminal: "false" }, "conv_1")],
        {},
      ),
    ],
    session,
  );

  assert.equal(session.current, "conv_1");
});

test("a call the host has forgotten is started again rather than failing the turn", async () => {
  // The default session store does not survive a restart, so this is what every open tab sees the
  // first time somebody redeploys.
  const session: Session = { current: "gone" };

  const { yields, sent } = await collect(
    [
      refusal(404, "no call opens under 'gone'.", ContinuationNotFound),
      streaming([created("conv_2"), delta("fresh start"), completed({}, "conv_2")]),
    ],
    session,
  );

  assert.equal(sent.length, 2);
  assert.equal(sent[0]!.conversation, "gone");
  assert.equal(sent[1]!.conversation, null, "the retry must not name the call that is gone.");
  assert.equal(session.current, "conv_2");
  assert.deepEqual(yields, ["fresh start"]);
});

test("a thread-owned turn sends the thread and keeps nothing", async () => {
  // The thread id is the conversation, so the reply's id is the call's own bookkeeping and is
  // never adopted. A second id here would be a second id to keep in step forever.
  const session: Session = { current: null };

  const { sent } = await collect(
    [streaming([created("other"), delta("hi"), completed()])],
    session,
    [{ role: "user", content: "hi" }],
    "thread-7",
  );

  assert.equal(sent[0]!.conversation, "thread-7");
  assert.equal(session.current, null);
});

test("a thread-owned 404 is not retried nameless", async () => {
  // Retrying without the thread would file the call under a fresh id and orphan the thread.
  const session: Session = { current: null };

  const { fetch, sent } = scripted([
    refusal(404, "no call opens under 'thread-7'.", ContinuationNotFound),
  ]);

  const drained: string[] = [];
  await assert.rejects(
    (async () => {
      for await (const state of runTurn({
        endpoint: "/v1/responses",
        session,
        input: "hi",
        abortSignal: new AbortController().signal,
        fetch,
        threadId: "thread-7",
      })) {
        drained.push(state.text);
      }
    })(),
    /no call opens under 'thread-7'/,
  );
  assert.deepEqual(drained, []);
  assert.equal(sent.length, 1);
});

test("a 404 that is not a lost conversation is not retried", async () => {
  const session: Session = { current: "conv_1" };

  await assert.rejects(
    () => collect([refusal(404, "the route is not mapped here.")], session),
    /the route is not mapped here/,
  );
});

test("a refused turn surfaces what the host said", async () => {
  const session: Session = { current: "conv_1" };

  await assert.rejects(
    () => collect([refusal(409, "this call is finished.", "turn_refused")], session),
    /this call is finished/,
  );
});

// -------------------------------------------------------------------------------------------------
// The reply.
// -------------------------------------------------------------------------------------------------

test("the reply grows with each piece", async () => {
  const session: Session = { current: null };

  const { yields } = await collect(
    [streaming([created("conv_1"), delta("Hel"), delta("lo "), delta("there"), completed()])],
    session,
  );

  assert.deepEqual(yields, ["Hel", "Hello ", "Hello there"]);
});

test("an event split across two reads is still read once, whole", async () => {
  // The regression this catches loses whole words silently, and only under load: the split happens
  // wherever the network happened to break the stream.
  const session: Session = { current: null };
  const whole = delta("split me");
  const at = Math.floor(whole.length / 2);

  const { yields } = await collect(
    [streaming([created("conv_1"), whole.slice(0, at), whole.slice(at), completed()])],
    session,
  );

  assert.deepEqual(yields, ["split me"]);
});

test("two events arriving in one read are both read", async () => {
  const session: Session = { current: null };

  const { yields } = await collect(
    [streaming([created("conv_1") + delta("one") + delta("two") + completed()])],
    session,
  );

  assert.deepEqual(yields, ["one", "onetwo"]);
});

test("a framework event without text yields nothing on its own", async () => {
  const session: Session = { current: null };

  const { yields } = await collect(
    [
      streaming([
        created("conv_1"),
        event(
          {
            type: "response.output_item.added",
            item: { type: "message", status: "in_progress" },
          },
          "response.output_item.added",
        ),
        delta("after"),
        completed(),
      ]),
    ],
    session,
  );

  assert.deepEqual(yields, ["after"]);
});
test("a tool's argument delta never enters the reply text", async () => {
  // The framework streams the raw tool-call JSON as function_call_arguments deltas. Those are
  // not words, so the reply must not absorb them even though they ride `delta`.
  const collected = await states([
    created("conv_1"),
    'data: {"agentcore_tool":{"call_id":"c1","name":"Search","phase":"call","arguments":{"userQuestion":"CT800"}}}\n\n',
    event(
      {
        type: "response.function_call_arguments.delta",
        delta: '{"userQuestion":"CT800 Spirit equipment: what the machine is"}',
      },
      "response.function_call_arguments.delta",
    ),
    delta("The CT800 is a treadmill."),
    completed(),
  ]);

  const last = collected[collected.length - 1]!;
  assert.equal(last.text, "The CT800 is a treadmill.");
  assert.equal(last.tools.length, 1);
});

test("the closing event updates the stage and the reply id", async () => {
  const session: Session = { current: null };
  const states = [];
  for await (const state of runTurn({
    endpoint: "/v1/responses",
    session,
    input: "hi",
    abortSignal: new AbortController().signal,
    fetch: scripted([
      streaming([
        created("conv_1"),
        delta("hi"),
        completed({ stage_after: "followup", message_id: "msg_1" }, "conv_1"),
      ]),
    ]).fetch,
  })) {
    states.push(state);
  }

  const last = states[states.length - 1];
  assert.equal(last!.stage, "followup");
  assert.equal(last!.replyMessageId, "msg_1");
});

// -------------------------------------------------------------------------------------------------
// Tool calls. The host runs the tool, so both halves arrive as facts and neither asks for anything.
// -------------------------------------------------------------------------------------------------

/** Runs one turn over the events given and answers every state it yielded. */
async function states(events: string[]) {
  const collected = [];
  for await (const state of runTurn({
    endpoint: "/v1/responses",
    session: { current: null },
    input: "look it up",
    abortSignal: new AbortController().signal,
    fetch: scripted([streaming(events)]).fetch,
  })) {
    collected.push(state);
  }

  return collected;
}

test("runTurn yields a tool call before its result arrives", async () => {
  // The point of showing a tool at all is showing it while it runs, so the call half must reach the
  // screen on its own rather than waiting to be paired with a result.
  const collected = await states([
    created("conv_1"),
    'data: {"agentcore_tool":{"call_id":"c1","name":"read_records","phase":"call","arguments":{"what":"revenue"}}}\n\n',
    completed(),
  ]);

  assert.equal(collected.length, 1);
  assert.equal(collected[0]!.tools.length, 1);
  assert.equal(collected[0]!.tools[0]!.name, "read_records");
  assert.equal(collected[0]!.tools[0]!.result, undefined);
});

test("runTurn keeps a tool answer that is an object as an object", async () => {
  const collected = await states([
    created("conv_1"),
    'data: {"agentcore_tool":{"call_id":"c1","name":"read_records","phase":"call","arguments":{}}}\n\n',
    'data: {"agentcore_tool":{"call_id":"c1","name":"read_records","phase":"result","result":{"entities":["it\'s"]},"failed":false}}\n\n',
    completed(),
  ]);

  assert.deepEqual(collected[1]!.tools[0]!.result, { entities: ["it's"] });
});

test("runTurn folds a tool result onto the call it answers", async () => {
  const collected = await states([
    created("conv_1"),
    'data: {"agentcore_tool":{"call_id":"c1","name":"read_records","phase":"call","arguments":{"what":"revenue"}}}\n\n',
    'data: {"agentcore_tool":{"call_id":"c1","name":"read_records","phase":"result","result":"42 rows","failed":false}}\n\n',
    'event: response.output_text.delta\ndata: {"type":"response.output_text.delta","delta":"there are 42."}\n\n',
    completed(),
  ]);

  assert.equal(collected[2]!.text, "there are 42.");
  assert.equal(collected[2]!.tools[0]!.result, "42 rows");
});

test("runTurn keeps a failed tool marked as failed", async () => {
  const collected = await states([
    created("conv_1"),
    'data: {"agentcore_tool":{"call_id":"c1","name":"read_records","phase":"call","arguments":{}}}\n\n',
    'data: {"agentcore_tool":{"call_id":"c1","name":"read_records","phase":"result","result":"the table is gone","failed":true}}\n\n',
    completed(),
  ]);

  assert.equal(collected[1]!.tools[0]!.failed, true);
});

test("runTurn keeps two tool calls apart and pairs each with its own result", async () => {
  const collected = await states([
    created("conv_1"),
    'data: {"agentcore_tool":{"call_id":"c1","name":"read_records","phase":"call","arguments":{}}}\n\n',
    'data: {"agentcore_tool":{"call_id":"c2","name":"aggregate_records","phase":"call","arguments":{}}}\n\n',
    'data: {"agentcore_tool":{"call_id":"c2","name":"aggregate_records","phase":"result","result":"second","failed":false}}\n\n',
    'data: {"agentcore_tool":{"call_id":"c1","name":"read_records","phase":"result","result":"first","failed":false}}\n\n',
    completed(),
  ]);

  assert.equal(collected[3]!.tools.length, 2);
  assert.equal(collected[3]!.tools[0]!.result, "first");
  assert.equal(collected[3]!.tools[1]!.result, "second");
});

test("a tool survives a later text-only yield", async () => {
  const collected = await states([
    created("conv_1"),
    'data: {"agentcore_tool":{"call_id":"c1","name":"read_records","phase":"call","arguments":{}}}\n\n',
    'event: response.output_text.delta\ndata: {"type":"response.output_text.delta","delta":"one moment"}\n\n',
    completed(),
  ]);

  assert.equal(collected[1]!.tools.length, 1);
  assert.equal(collected[1]!.text, "one moment");
});

test("a result for a call that never arrived is kept rather than dropped", async () => {
  const collected = await states([
    created("conv_1"),
    'data: {"agentcore_tool":{"call_id":"c9","name":"read_records","phase":"result","result":"orphan","failed":false}}\n\n',
    completed(),
  ]);

  assert.equal(collected[0]!.tools.length, 1);
});

test("a turn that calls no tool yields an empty tool list", async () => {
  const collected = await states([created("conv_1"), delta("plain answer"), completed()]);

  assert.deepEqual(collected[0]!.tools, []);
});

// -------------------------------------------------------------------------------------------------
// Sources. The host cites them, the browser folds them, and nothing draws them yet.
// -------------------------------------------------------------------------------------------------

describe("foldSource", () => {
  it("starts a source from its first frame", () => {
    const sources = foldSource([], {
      call_id: "c1",
      id: "card-42",
      source_type: "document",
      title: "Spirit CT900 owner's manual, p.27",
      locator: "p.27",
      media_type: "text/plain",
      origin: "knowledge",
    });

    expect(sources).toHaveLength(1);
    expect(sources[0]).toMatchObject({ id: "card-42", title: "Spirit CT900 owner's manual, p.27" });
  });

  it("keeps a later frame for the same id over the earlier one", () => {
    const first = foldSource([], { id: "card-42", title: "old" });
    const second = foldSource(first, { id: "card-42", title: "new" });

    expect(second).toHaveLength(1);
    expect(second[0]!.title).toBe("new");
  });

  it("ignores a frame with no id", () => {
    expect(foldSource([], { title: "no id" })).toEqual([]);
  });

  it("reads a url source as a url", () => {
    const sources = foldSource([], {
      id: "page-1",
      source_type: "url",
      url: "https://example.com/manual",
    });

    expect(sources[0]!.sourceType).toBe("url");
  });
});

describe("foldApproval", () => {
  it("lands the ask on the tool it names", async () => {
    const collected = await states([
      created("conv_1"),
      'data: {"agentcore_tool":{"call_id":"c1","name":"send_email","phase":"call","arguments":{"to":"a@b.com"}}}\n\n',
      'data: {"agentcore_approval":{"request_id":"req_1","tool":"send_email","arguments":{"to":"a@b.com"}}}\n\n',
      completed(),
    ]);

    expect(collected[1]!.tools).toHaveLength(1);
    expect(collected[1]!.tools[0]).toMatchObject({
      name: "send_email",
      approval: { requestId: "req_1" },
    });
    expect(collected[1]!.tools[0]!.result).toBeUndefined();
  });

  it("keeps a gate whose call half never arrived", async () => {
    const collected = await states([
      created("conv_1"),
      'data: {"agentcore_approval":{"request_id":"req_9","tool":"send_email","arguments":{}}}\n\n',
      completed(),
    ]);

    expect(collected[0]!.tools).toHaveLength(1);
    expect(collected[0]!.tools[0]!.approval).toEqual({ requestId: "req_9" });
  });

  it("ignores an ask with no request id", async () => {
    const collected = await states([
      created("conv_1"),
      'data: {"agentcore_approval":{"tool":"send_email"}}\n\n',
      completed(),
    ]);

    expect(collected[0]!.tools).toEqual([]);
  });
});

test("an approval answer posts no words and names the request", async () => {
  const session: Session = { current: "conv_1" };
  const { fetch, sent } = scripted([streaming([created("conv_1"), delta("done."), completed()])]);
  const drained: string[] = [];

  for await (const state of runTurn({
    endpoint: "/v1/responses",
    session,
    input: "anything",
    abortSignal: new AbortController().signal,
    fetch,
    approval: { requestId: "req_1", approved: true },
  })) {
    drained.push(state.text);
  }

  assert.ok(drained.length > 0);

  const body = sent[0]!.body as Record<string, unknown>;
  expect(body["input"]).toEqual([]);
  expect(body["conversation"]).toBe("conv_1");
  expect(body["agentcore"]).toMatchObject({
    approval: { request_id: "req_1", approved: true },
  });
});

test("readEvent keeps the dialect frames apart from text frames", () => {
  const [dialect] = readEvent('data: {"agentcore_tool":{"call_id":"c1"}}\n') as StreamChunk[];
  const [text] = readEvent(
    'event: response.output_text.delta\ndata: {"type":"response.output_text.delta","delta":"hi"}\n',
  ) as StreamChunk[];

  assert.ok(dialect!.agentcore_tool);
  assert.equal(text!.type, "response.output_text.delta");
});
