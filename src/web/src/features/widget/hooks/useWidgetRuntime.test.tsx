import { act, renderHook, waitFor } from "@testing-library/react";
import { describe, expect, it } from "vitest";

import { ConversationField } from "@/features/threads/transport";
import type { FetchLike } from "@/lib/apiClient";
import type {
  ChatwootClient,
  ChatwootContact,
  ChatwootConversation,
  ChatwootConversationEvent,
  ChatwootMessage,
} from "@/lib/chatwoot";
import contactCreated from "@/lib/chatwoot/payloads/contact_created.json";
import conversationCreated from "@/lib/chatwoot/payloads/conversation_created.json";
import conversationsList from "@/lib/chatwoot/payloads/conversations_list.json";
import messagePosted from "@/lib/chatwoot/payloads/message_posted.json";
import messagesNewest from "@/lib/chatwoot/payloads/messages_newest.json";
import socketFrames from "@/lib/chatwoot/payloads/socket_frames.json";
import type { WidgetChat } from "../api/widgetChat";
import {
  ChatwootConversationHeader,
  ChatwootMessageHeader,
  useWidgetRuntime,
  type WidgetRuntime,
} from "./useWidgetRuntime";

/*
 * Chatwoot is a fake Client API that answers with payloads recorded from local Chatwoot 4.18.0:
 * conversation 27, uuid f1d95917-…, whose first message is 180. Spirit is a scripted `fetch`
 * that answers each turn with a canned Responses stream.
 */

const Uuid = "f1d95917-bc28-4e1c-b4ef-2587df3a0113";

/** The socket's pushes, by event, in the order they were recorded. */
function pushes<T>(event: string): T[] {
  return (socketFrames as { message?: { event: string; data: unknown } }[]).flatMap((frame) =>
    frame.message?.event === event ? [frame.message.data as T] : [],
  );
}

const messagePushes = pushes<ChatwootMessage>("message.created");
const botAnswer = messagePushes.find((message) => message.sender?.type === "agent_bot")!;
const staffReply = messagePushes.find((message) => message.sender?.type === "user")!;
const staffTookIt = pushes<ChatwootConversationEvent>("conversation.status_changed").find(
  (event) => event.status === "open",
)!;

/** A whole reply saying `text`, as Spirit streams it. */
function reply(text: string): Response {
  const event = (payload: unknown, name: string) =>
    `event: ${name}\ndata: ${JSON.stringify(payload)}\n\n`;
  return new Response(
    event({ type: "response.output_text.delta", delta: text }, "response.output_text.delta") +
      event({ type: "response.completed", response: { id: "resp_1" } }, "response.completed"),
    { status: 200, headers: { "Content-Type": "text/event-stream" } },
  );
}

/** Spirit's refusal of a turn on a chat the AI does not have. */
function notPending(): Response {
  return new Response(JSON.stringify({ error: { message: "Not pending.", code: "conflict" } }), {
    status: 409,
    headers: { "Content-Type": "application/json" },
  });
}

type Turn = {
  conversation: unknown;
  chatwootConversation: string | null;
  chatwootMessage: string | null;
};

/** Spirit, answering from a script, recording what each turn named. */
function spirit(responses: Response[]): { send: FetchLike; turns: Turn[] } {
  const turns: Turn[] = [];
  const send: FetchLike = async (_url, init) => {
    const headers = new Headers(init?.headers);
    turns.push({
      conversation: (JSON.parse(String(init?.body)) as Record<string, unknown>)[ConversationField],
      chatwootConversation: headers.get(ChatwootConversationHeader),
      chatwootMessage: headers.get(ChatwootMessageHeader),
    });
    const next = responses.shift();
    if (!next) throw new Error("the test scripted fewer answers than turns.");
    return next;
  };
  return { send, turns };
}

/** Chatwoot, as one visitor's Client API, with or without a conversation already. */
function chatwoot(conversations: unknown[]) {
  const posted: { id: number; content: string }[] = [];
  let created = 0;
  const client: ChatwootClient = {
    contact: async () => contactCreated as ChatwootContact,
    conversations: async () => conversations as ChatwootConversation[],
    createConversation: async () => {
      created += 1;
      return conversationCreated as ChatwootConversation;
    },
    post: async (id, content) => {
      posted.push({ id, content });
      return { ...(messagePosted as ChatwootMessage), content };
    },
    messages: async () => messagesNewest as ChatwootMessage[],
    typing: async () => {},
  };
  const chat: WidgetChat = {
    settings: { chatwootBaseUrl: "http://localhost:53000", chatwootInboxIdentifier: "inbox" },
    client,
    contact: contactCreated as ChatwootContact,
  };
  const start = () => Promise.resolve(chat);
  return { start, posted, created: () => created };
}

function texts({ runtime }: WidgetRuntime): string[] {
  return runtime.thread
    .getState()
    .messages.map((message) =>
      message.content.map((part) => (part.type === "text" ? part.text : "")).join(""),
    );
}

async function say(widget: WidgetRuntime, text: string) {
  await act(async () => {
    await widget.runtime.thread.append({ role: "user", content: [{ type: "text", text }] });
  });
}

describe("useWidgetRuntime", () => {
  it("makes the conversation on the first send, posts to Chatwoot, then asks Spirit", async () => {
    const cw = chatwoot([]);
    const { send, turns } = spirit([reply("Yes, it is in stock.")]);
    const view = renderHook(() => useWidgetRuntime("/v1/public/main/responses", cw.start, send));
    await waitFor(() => expect(view.result.current.chat).not.toBeNull());

    await say(view.result.current, "Do you have the XT485 in stock?");

    expect(cw.created()).toBe(1);
    expect(cw.posted).toEqual([{ id: 27, content: "Do you have the XT485 in stock?" }]);
    expect(turns).toEqual([
      { conversation: `cw_${Uuid}`, chatwootConversation: "27", chatwootMessage: "180" },
    ]);
    expect(texts(view.result.current)).toEqual([
      "Do you have the XT485 in stock?",
      "Yes, it is in stock.",
    ]);
  });

  it("keeps a sentence and the program after it as two parts, so the program draws", async () => {
    // Item ids recorded from the public route, which hides the tool call between the two.
    const event = (payload: unknown) =>
      `event: response.output_text.delta\ndata: ${JSON.stringify(payload)}\n\n`;
    const answer = new Response(
      event({
        type: "response.output_text.delta",
        item_id: "msg_Kt5Rw0R2WNnH8NfWsvwoj2GB6fBZwqXF",
        delta: "Lubrication schedule — I'll pull the guide for your treadmill.",
      }) +
        event({
          type: "response.output_text.delta",
          item_id: "msg_DSfa6o9FGmHYgB1T7FsHTCT7EcR2fIwX",
          delta: 'root = Card([header])\nheader = CardHeader("Treadmill belt lubrication")',
        }) +
        `event: response.completed\ndata: ${JSON.stringify({ type: "response.completed", response: { id: "resp_1" } })}\n\n`,
      { status: 200, headers: { "Content-Type": "text/event-stream" } },
    );
    const cw = chatwoot([]);
    const { send } = spirit([answer]);
    const view = renderHook(() => useWidgetRuntime("/v1/public/main/responses", cw.start, send));
    await waitFor(() => expect(view.result.current.chat).not.toBeNull());

    await say(view.result.current, "How often should I lubricate my treadmill belt?");

    const reply = view.result.current.runtime.thread.getState().messages.at(-1)!;
    expect(reply.content.map((part) => (part.type === "text" ? part.text : part.type))).toEqual([
      "Lubrication schedule — I'll pull the guide for your treadmill.",
      'root = Card([header])\nheader = CardHeader("Treadmill belt lubrication")',
    ]);
  });

  it("drops Chatwoot's copy of an answer the page streamed, and shows a staff reply", async () => {
    const cw = chatwoot([]);
    const { send } = spirit([reply("Yes, it is in stock.")]);
    const view = renderHook(() => useWidgetRuntime("/v1/public/main/responses", cw.start, send));
    await waitFor(() => expect(view.result.current.chat).not.toBeNull());
    await say(view.result.current, "Do you have the XT485 in stock?");

    let copy = true;
    let staff = false;
    act(() => {
      copy = view.result.current.receive(botAnswer);
      staff = view.result.current.receive(staffReply);
    });

    expect([copy, staff]).toEqual([false, true]);
    expect(texts(view.result.current)).toEqual([
      "Do you have the XT485 in stock?",
      "Yes, it is in stock.",
      "Hi, this is Matthew. I can help.",
    ]);
  });

  it("posts to Chatwoot only once a person has the chat, and says who joined", async () => {
    const cw = chatwoot([]);
    const { send, turns } = spirit([reply("Yes, it is in stock.")]);
    const view = renderHook(() => useWidgetRuntime("/v1/public/main/responses", cw.start, send));
    await waitFor(() => expect(view.result.current.chat).not.toBeNull());
    await say(view.result.current, "Do you have the XT485 in stock?");

    act(() => view.result.current.move(staffTookIt));
    await say(view.result.current, "Thanks, Matthew.");

    expect(turns).toHaveLength(1);
    expect(cw.posted.map((post) => post.content)).toEqual([
      "Do you have the XT485 in stock?",
      "Thanks, Matthew.",
    ]);
    expect(texts(view.result.current).slice(2)).toEqual([
      "Matthew Hsu joined the chat.",
      "Thanks, Matthew.",
    ]);
    expect(view.result.current.desk).toMatchObject({
      status: "human",
      assigneeName: "Matthew Hsu",
    });
  });

  it("takes back the empty reply when Spirit says the AI no longer has the chat", async () => {
    const cw = chatwoot([]);
    const { send } = spirit([notPending()]);
    const view = renderHook(() => useWidgetRuntime("/v1/public/main/responses", cw.start, send));
    await waitFor(() => expect(view.result.current.chat).not.toBeNull());

    await say(view.result.current, "Hello?");

    expect(texts(view.result.current)).toEqual(["Hello?"]);
  });

  it("opens the newest conversation on start, with its newest page and where to page from", async () => {
    const cw = chatwoot(conversationsList);
    const { send } = spirit([]);
    const view = renderHook(() => useWidgetRuntime("/v1/public/main/responses", cw.start, send));

    await waitFor(() => expect(view.result.current.older?.initialCursor).toBe("189"));
    expect(view.result.current.older?.id).toBe("27");
    expect(texts(view.result.current)).toHaveLength(20);
    expect(texts(view.result.current).at(-1)).toBe("Page probe 22");
  });
});
