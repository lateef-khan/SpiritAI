import { act, renderHook, waitFor } from "@testing-library/react";
import type { ExportedMessageRepository } from "@assistant-ui/react";
import { beforeEach, describe, expect, it } from "vitest";

import { ConversationField } from "@/features/threads/transport";
import { HostRefusedError, type FetchLike } from "@/lib/apiClient";
import { readVisitorMemory, rememberCall } from "../api/visitorIdentity";
import type { HandoffState, WidgetApi } from "../api/widgetApi";
import { WithBot, type HandoffDesk } from "./useHandoffDesk";
import { useWidgetRuntime } from "./useWidgetRuntime";

/**
 * The widget's store, one test per promise about the call id.
 *
 * The host is a fake api and a scripted `fetch`: every turn is answered from a canned Responses
 * stream, and what is held in place is which call id each turn went up under, and when a thread
 * is made on the host. The failures these cover — a call the host forgot, a thread made twice —
 * are the ones a browser shows as a chat that quietly starts over.
 */

/** One data event, as the endpoint writes it. */
function event(payload: unknown, eventName: string): string {
  return `event: ${eventName}\ndata: ${JSON.stringify(payload)}\n\n`;
}

/** A whole reply saying `text`, closed with the turn facts. `tools` are named as having run. */
function reply(text: string, messageId = "host-reply", tools: string[] = []): Response {
  const body = [
    ...tools.map((name) =>
      event(
        { agentcore_tool: { call_id: `t-${name}`, name, phase: "result", result: {} } },
        "agentcore_tool",
      ),
    ),
    event({ type: "response.output_text.delta", delta: text }, "response.output_text.delta"),
    event(
      {
        type: "response.completed",
        response: { id: "resp_1", metadata: { message_id: messageId, stage_after: "answer" } },
      },
      "response.completed",
    ),
  ].join("");

  return new Response(body, { status: 200, headers: { "Content-Type": "text/event-stream" } });
}

/** A refusal in the shape the endpoint writes. */
function refusal(status: number, code: string): Response {
  return new Response(JSON.stringify({ error: { message: `refused: ${code}`, code } }), {
    status,
    headers: { "Content-Type": "application/json" },
  });
}

/** The door's refusal once the call's row is gone: a problem body, no code. */
function noSuchThread(): Response {
  return new Response(
    JSON.stringify({ title: "No such thread.", status: 404, detail: "This caller has no thread." }),
    { status: 404, headers: { "Content-Type": "application/problem+json" } },
  );
}

/** One recorded turn. */
type Sent = { conversation: string | null };

/** A `fetch` that answers from a script and records the conversation each turn named. */
function scripted(responses: Response[]): { send: FetchLike; sent: Sent[] } {
  const sent: Sent[] = [];
  let index = 0;

  const send: FetchLike = (_url, init) => {
    const body = JSON.parse(String(init?.body)) as Record<string, unknown>;
    sent.push({
      conversation:
        typeof body[ConversationField] === "string" ? (body[ConversationField] as string) : null,
    });

    const response = responses[index++];
    if (!response) throw new Error("the test scripted fewer answers than turns.");
    return Promise.resolve(response);
  };

  return { send, sent };
}

/** A fake host that mints ids in order, answers one history, and records what was said to a person. */
function fakeApi(
  history: (callId: string) => Promise<ExportedMessageRepository>,
  say: (callId: string, text: string) => Promise<{ messageId: string }> = async () => ({
    messageId: "host-said",
  }),
): {
  api: WidgetApi;
  created: string[];
  said: { callId: string; text: string }[];
} {
  const created: string[] = [];
  const said: { callId: string; text: string }[] = [];

  return {
    created,
    said,
    api: {
      createThread: async () => {
        const id = `call-${created.length + 1}`;
        created.push(id);
        return id;
      },
      history,
      handoffState: async () => WithBot,
      leaveEmail: async () => {},
      say: async (callId, text) => {
        said.push({ callId, text });
        const { messageId } = await say(callId, text);
        return { callId, messageId, role: "user", text, speaker: null, at: "2026-09-16T09:00:00Z" };
      },
    },
  };
}

/** A desk that holds one state, and answers `next` on every refresh. */
function fakeDesk(
  state: HandoffState,
  next: HandoffState = state,
): HandoffDesk & { refreshed: number } {
  const desk = {
    refreshed: 0,
    state,
    refresh: async () => {
      desk.refreshed += 1;
      desk.state = next;
      return next;
    },
    leaveEmail: async () => {},
    apply: (change: Partial<HandoffState>) => {
      desk.state = { ...desk.state, ...change };
    },
  };
  return desk;
}

const waiting: HandoffState = {
  status: "waiting",
  position: 1,
  assigneeName: null,
  staffOnline: true,
  email: null,
};

const stored: ExportedMessageRepository = {
  headId: "m2",
  messages: [
    {
      parentId: null,
      message: {
        id: "m1",
        role: "user",
        content: [{ type: "text", text: "hello" }],
        createdAt: new Date("2026-09-16T09:00:00Z"),
        attachments: [],
        metadata: { custom: {} },
      },
    },
    {
      parentId: "m1",
      message: {
        id: "m2",
        role: "assistant",
        content: [{ type: "text", text: "hi there" }],
        createdAt: new Date("2026-09-16T09:00:01Z"),
        status: { type: "complete", reason: "stop" },
        metadata: {
          unstable_state: null,
          unstable_annotations: [],
          unstable_data: [],
          steps: [],
          custom: {},
        },
      },
    },
  ],
};

/** The text of every message the runtime holds, in order. */
function texts({ runtime }: ReturnType<typeof useWidgetRuntime>): string[] {
  return runtime.thread.getState().messages.map((message) =>
    message.content
      .filter((part): part is { type: "text"; text: string } => part.type === "text")
      .map((part) => part.text)
      .join(""),
  );
}

async function send({ runtime }: ReturnType<typeof useWidgetRuntime>, text: string) {
  await act(async () => {
    await runtime.thread.append({ role: "user", content: [{ type: "text", text }] });
  });
}

beforeEach(() => localStorage.clear());

describe("useWidgetRuntime", () => {
  it("restores a remembered call's history on mount", async () => {
    rememberCall("call-kept");
    const { api } = fakeApi(async () => stored);
    const { send: fetch } = scripted([]);

    const view = renderHook(() =>
      useWidgetRuntime("/v1/public/responses", api, fetch, fakeDesk(WithBot)),
    );

    await waitFor(() => expect(texts(view.result.current)).toEqual(["hello", "hi there"]));
  });

  it("forgets a remembered call the host answers 404 for", async () => {
    rememberCall("call-gone");
    const { api } = fakeApi(async () => {
      throw new HostRefusedError(404, "/v1/public/threads/call-gone/messages");
    });
    const { send: fetch } = scripted([]);

    const view = renderHook(() =>
      useWidgetRuntime("/v1/public/responses", api, fetch, fakeDesk(WithBot)),
    );

    await waitFor(() => expect(readVisitorMemory().callId).toBeNull());
    expect(texts(view.result.current)).toEqual([]);
  });

  it("makes the thread on the first send and runs the turn under its id", async () => {
    const { api, created } = fakeApi(async () => stored);
    const { send: fetch, sent } = scripted([reply("welcome")]);

    const view = renderHook(() =>
      useWidgetRuntime("/v1/public/responses", api, fetch, fakeDesk(WithBot)),
    );
    expect(created).toEqual([]);

    await send(view.result.current, "hi");

    await waitFor(() => expect(texts(view.result.current)).toEqual(["hi", "welcome"]));
    expect(created).toEqual(["call-1"]);
    expect(sent.map((turn) => turn.conversation)).toEqual(["call-1"]);
    expect(readVisitorMemory().callId).toBe("call-1");
  });

  it("does not make the thread again on the second send", async () => {
    const { api, created } = fakeApi(async () => stored);
    const { send: fetch, sent } = scripted([reply("one"), reply("two", "host-reply-2")]);

    const view = renderHook(() =>
      useWidgetRuntime("/v1/public/responses", api, fetch, fakeDesk(WithBot)),
    );

    await send(view.result.current, "first");
    await waitFor(() => expect(texts(view.result.current)).toEqual(["first", "one"]));
    await send(view.result.current, "second");
    await waitFor(() =>
      expect(texts(view.result.current)).toEqual(["first", "one", "second", "two"]),
    );

    expect(created).toEqual(["call-1"]);
    expect(sent.map((turn) => turn.conversation)).toEqual(["call-1", "call-1"]);
  });

  it("makes a new thread and retries once when the host forgot the call", async () => {
    rememberCall("call-stale");
    const { api, created } = fakeApi(async () => ({ messages: [] }));
    const { send: fetch, sent } = scripted([noSuchThread(), reply("again")]);

    const view = renderHook(() =>
      useWidgetRuntime("/v1/public/responses", api, fetch, fakeDesk(WithBot)),
    );

    await send(view.result.current, "still there?");

    await waitFor(() => expect(texts(view.result.current)).toEqual(["still there?", "again"]));
    expect(created).toEqual(["call-1"]);
    expect(sent.map((turn) => turn.conversation)).toEqual(["call-stale", "call-1"]);
    expect(readVisitorMemory().callId).toBe("call-1");
  });

  it("reports a refusal that is not a forgotten call on the reply", async () => {
    rememberCall("call-kept");
    const { api, created } = fakeApi(async () => ({ messages: [] }));
    const { send: fetch } = scripted([refusal(429, "rate_limited")]);

    const view = renderHook(() =>
      useWidgetRuntime("/v1/public/responses", api, fetch, fakeDesk(WithBot)),
    );

    await send(view.result.current, "hi");

    await waitFor(() =>
      expect(view.result.current.runtime.thread.getState().messages.at(-1)?.status).toMatchObject({
        type: "incomplete",
        reason: "error",
      }),
    );
    expect(created).toEqual([]);
    expect(readVisitorMemory().callId).toBe("call-kept");
  });

  it("sends to the person and draws no reply while the chat is waiting", async () => {
    rememberCall("call-kept");
    const { api, said } = fakeApi(async () => ({ messages: [] }));
    const { send: fetch, sent } = scripted([]);
    const desk = fakeDesk(waiting);

    const view = renderHook(() => useWidgetRuntime("/v1/public/responses", api, fetch, desk));

    await send(view.result.current, "are you there?");

    await waitFor(() => expect(said).toEqual([{ callId: "call-kept", text: "are you there?" }]));
    const held = view.result.current.runtime.thread.getState().messages;
    expect(held.map((m) => m.role)).toEqual(["user"]);
    expect(held[0]?.metadata.custom).toMatchObject({ hostMessageId: "host-said" });
    expect(sent).toEqual([]);
  });

  it("reads the desk again after a turn in which the bot asked for a person", async () => {
    rememberCall("call-kept");
    const { api } = fakeApi(async () => ({ messages: [] }));
    const { send: fetch } = scripted([
      reply("I have asked a person to join.", "r1", ["request_human"]),
    ]);
    const desk = fakeDesk(WithBot, waiting);

    const view = renderHook(() => useWidgetRuntime("/v1/public/responses", api, fetch, desk));

    await send(view.result.current, "I want a human");

    await waitFor(() => expect(desk.refreshed).toBe(1));
    expect(texts(view.result.current)).toEqual([
      "I want a human",
      "I have asked a person to join.",
    ]);
  });

  it("does not read the desk again after an ordinary turn", async () => {
    rememberCall("call-kept");
    const { api } = fakeApi(async () => ({ messages: [] }));
    const { send: fetch } = scripted([reply("sure", "r1", ["lookup_model"])]);
    const desk = fakeDesk(WithBot);

    const view = renderHook(() => useWidgetRuntime("/v1/public/responses", api, fetch, desk));

    await send(view.result.current, "hi");

    await waitFor(() => expect(texts(view.result.current)).toEqual(["hi", "sure"]));
    expect(desk.refreshed).toBe(0);
  });

  it("goes through the other door once when the chat changed hands", async () => {
    rememberCall("call-kept");
    const { api, said } = fakeApi(async () => ({ messages: [] }));
    const { send: fetch } = scripted([
      new Response(
        JSON.stringify({ title: "A person has this chat.", status: 409, type: "handoff_open" }),
        { status: 409, headers: { "Content-Type": "application/problem+json" } },
      ),
    ]);
    const desk = fakeDesk(WithBot, waiting);

    const view = renderHook(() => useWidgetRuntime("/v1/public/responses", api, fetch, desk));

    await send(view.result.current, "still there?");

    await waitFor(() => expect(said).toEqual([{ callId: "call-kept", text: "still there?" }]));
    expect(desk.refreshed).toBe(1);
    expect(view.result.current.runtime.thread.getState().messages.map((m) => m.role)).toEqual([
      "user",
    ]);
  });

  it("takes a pushed reply once, and never the visitor's own words", async () => {
    rememberCall("call-kept");
    const { api } = fakeApi(async () => ({ messages: [] }));
    const { send: fetch } = scripted([]);

    const view = renderHook(() =>
      useWidgetRuntime("/v1/public/responses", api, fetch, fakeDesk(waiting)),
    );

    const reply = {
      callId: "call-kept",
      messageId: "host-9",
      role: "assistant",
      text: "Hi, Dana here.",
      speaker: { kind: "human", name: "Dana R.", detail: "Support" },
      at: "2026-09-16T09:00:00Z",
    };
    act(() => {
      view.result.current.receive(reply);
      view.result.current.receive(reply);
      view.result.current.receive({ ...reply, messageId: "host-10", role: "user", text: "me" });
    });

    const held = view.result.current.runtime.thread.getState().messages;
    expect(texts(view.result.current)).toEqual(["Hi, Dana here."]);
    expect(held[0]?.metadata.custom).toMatchObject({
      speaker: { kind: "human", name: "Dana R." },
      hostMessageId: "host-9",
    });
  });

  it("does not double a pushed reply the reload already brought", async () => {
    rememberCall("call-kept");
    const stored: ExportedMessageRepository = {
      headId: "host-9",
      messages: [
        {
          parentId: null,
          message: {
            id: "host-9",
            role: "assistant",
            content: [{ type: "text", text: "Hi, Dana here." }],
            createdAt: new Date("2026-09-16T09:00:00Z"),
            status: { type: "complete", reason: "stop" },
            metadata: {
              unstable_state: null,
              unstable_annotations: [],
              unstable_data: [],
              steps: [],
              custom: { hostMessageId: "host-9" },
            },
          },
        },
      ],
    };
    const { api } = fakeApi(async () => stored);
    const { send: fetch } = scripted([]);

    const view = renderHook(() =>
      useWidgetRuntime("/v1/public/responses", api, fetch, fakeDesk(waiting)),
    );
    await waitFor(() => expect(texts(view.result.current)).toEqual(["Hi, Dana here."]));

    act(() => {
      view.result.current.receive({
        callId: "call-kept",
        messageId: "host-9",
        role: "assistant",
        text: "Hi, Dana here.",
        speaker: null,
        at: "2026-09-16T09:00:00Z",
      });
    });

    expect(texts(view.result.current)).toEqual(["Hi, Dana here."]);
  });

  it("follows the call id as it is made", async () => {
    const { api } = fakeApi(async () => ({ messages: [] }));
    const { send: fetch } = scripted([reply("hello")]);

    const view = renderHook(() =>
      useWidgetRuntime("/v1/public/responses", api, fetch, fakeDesk(WithBot)),
    );
    expect(view.result.current.callId).toBeNull();

    await send(view.result.current, "hi");

    await waitFor(() => expect(view.result.current.callId).toBe("call-1"));
  });
});
