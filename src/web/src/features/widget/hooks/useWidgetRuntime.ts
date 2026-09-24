import {
  useExternalStoreRuntime,
  type AppendMessage,
  type AssistantRuntime,
  type ThreadMessage,
} from "@assistant-ui/react";
import { useCallback, useEffect, useMemo, useRef, useState } from "react";

import { flatten, sourceContent, toolContent } from "@/features/threads/AgentCoreRuntime";
import { runTurn, TurnRefusedError, type TurnState } from "@/features/threads/transport";
import type { FetchLike } from "@/lib/apiClient";
import type {
  ChatwootConversation,
  ChatwootConversationEvent,
  ChatwootMessage,
  ChatwootPresence,
} from "@/lib/chatwoot";
import type { OlderMessagesSource } from "@/lib/history";
import { heldFromChatwoot, pageFromChatwoot, systemNote } from "../api/chatwootRows";
import type { WidgetChat } from "../api/widgetChat";
import { handoffStatus, moved, NoChat, staffOnline, type Desk, type HandoffState } from "./desk";
import { heldFromPage, holds, prependOlder, reloadPage, type Held } from "./held";

/**
 * The widget's runtime: a store the widget owns, over the visitor's newest Chatwoot conversation.
 *
 * Chatwoot holds the chat. Every send goes to Chatwoot first. While the AI has the chat
 * (`pending`), Spirit is asked for the answer, which streams onto the page; Spirit posts it to
 * Chatwoot after. While a person has it (`open`), Chatwoot is all there is (spec 6.2, 6.3).
 */

/** The headers that name the Chatwoot conversation and message a turn answers (spec 6.2). */
export const ChatwootConversationHeader = "X-Chatwoot-Conversation";
export const ChatwootMessageHeader = "X-Chatwoot-Message";

/** Spirit's name for a Chatwoot conversation (spec 5). */
export function spiritConversationId(conversation: ChatwootConversation): string {
  return `cw_${conversation.uuid}`;
}

/** Maps one streamed state onto the reply, the way the app's adapter does. */
function replyFrom(reply: Held, state: TurnState): Held {
  return {
    ...reply,
    content: [
      ...state.tools.map(toolContent),
      ...state.sources.map(sourceContent),
      ...(state.text.length > 0 ? [{ type: "text" as const, text: state.text }] : []),
    ],
    metadata: { custom: { stage: state.stage, isTerminal: state.isTerminal } },
  };
}

/** An empty reply, drawn as running until the turn fills it. */
function pendingReply(): Held {
  return {
    id: crypto.randomUUID(),
    role: "assistant",
    content: [],
    createdAt: new Date(),
    status: { type: "running" },
  };
}

/** Names a held row by the id Chatwoot gave it. */
function tagged(row: Held, chatwootId: number): Held {
  return {
    ...row,
    metadata: { custom: { ...row.metadata?.custom, hostMessageId: String(chatwootId) } },
  };
}

/** What the widget hands `AssistantRuntimeProvider`, and the ways the socket reaches in. */
export type WidgetRuntime = {
  readonly runtime: AssistantRuntime;
  /** Where the chat stands. */
  readonly desk: HandoffState;
  /** The visitor's Chatwoot side, once started. The socket opens on it. */
  readonly chat: WidgetChat | null;
  /** Takes one pushed message. Answers whether it is a staff reply the visitor has not seen. */
  receive(message: ChatwootMessage): boolean;
  /** Takes one pushed change of the conversation's status or assignee. */
  move(event: ChatwootConversationEvent): void;
  /** Takes one pushed presence. */
  presence(push: ChatwootPresence): void;
  /** Reads the newest conversation and its newest page again. */
  sync(): Promise<void>;
  /** Tells a person on the chat whether the visitor is typing. */
  sayTyping(on: boolean): void;
  /** Where the pages before what is held come from, or `undefined` until there is a conversation. */
  readonly older: OlderMessagesSource | undefined;
};

/** What the widget holds, and where the page before it starts. One state, so a reload sets both. */
type Store = {
  readonly messages: readonly Held[];
  /** As {@link OlderMessagesSource.initialCursor}: `undefined` until a page has been read. */
  readonly olderCursor: string | null | undefined;
};

/**
 * Binds assistant-ui to the visitor's Chatwoot conversation.
 *
 * @param endpoint The public Responses route.
 * @param start Starts the visitor's Chatwoot side, once.
 * @param send How a turn reaches Spirit. Must carry the visitor's key.
 * @returns The runtime to hand to `AssistantRuntimeProvider`.
 */
export function useWidgetRuntime(
  endpoint: string,
  start: () => Promise<WidgetChat>,
  send: FetchLike,
): WidgetRuntime {
  const [store, setStore] = useState<Store>({ messages: [], olderCursor: undefined });
  const [isRunning, setRunning] = useState(false);
  const [chat, setChat] = useState<WidgetChat | null>(null);
  const [conversation, setConversation] = useState<ChatwootConversation | null>(null);
  const [desk, setDeskState] = useState<Desk>(NoChat);
  // The same conversation and desk, for the async paths below: they must see a change made
  // after their render.
  const conversationRef = useRef<ChatwootConversation | null>(null);
  const deskRef = useRef<Desk>(NoChat);
  // The reply this page streamed last, until Chatwoot's copy of it names its id.
  const awaitingCopy = useRef<string | null>(null);
  const streamedHere = useRef(false);
  const abortRef = useRef<AbortController | null>(null);

  const setMessages = useCallback(
    (next: (held: readonly Held[]) => readonly Held[]) =>
      setStore((s) => ({ ...s, messages: next(s.messages) })),
    [],
  );

  const setDesk = useCallback((next: Desk) => {
    deskRef.current = next;
    setDeskState(next);
  }, []);

  const replace = useCallback(
    (id: string, next: (held: Held) => Held) =>
      setMessages((held) => held.map((message) => (message.id === id ? next(message) : message))),
    [setMessages],
  );

  const adopt = useCallback((next: ChatwootConversation) => {
    conversationRef.current = next;
    setConversation(next);
  }, []);

  const sync = useCallback(async () => {
    const { client } = await start();
    const newest = (await client.conversations()).reduce<ChatwootConversation | null>(
      (a, b) => (a === null || b.id > a.id ? b : a),
      null,
    );
    const held = conversationRef.current;
    if (newest === null || (held !== null && newest.id < held.id)) return;

    const page = await client.messages(newest.id);
    const fresh = pageFromChatwoot(page);
    // The Client API names no assignee. The last member of staff who wrote stands in for one.
    const person =
      deskRef.current.assigneeName ??
      [...page].reverse().find((message) => message.sender?.type === "user")?.sender?.name ??
      null;

    adopt(newest);
    setDesk({
      ...deskRef.current,
      chatwoot: newest.status,
      assigneeName: person,
      status: handoffStatus(newest.status, person),
    });
    setStore((s) => {
      const { messages, kept } = reloadPage(s.messages, heldFromPage(fresh.repository));
      // Rows kept above the page were paged in already, from the cursor held.
      return {
        messages,
        olderCursor: kept ? (s.olderCursor ?? fresh.nextCursor) : fresh.nextCursor,
      };
    });
  }, [adopt, setDesk, start]);

  useEffect(() => {
    let cancelled = false;
    start().then(
      (started) => {
        if (cancelled) return;
        setChat(started);
        void sync().catch(() => {});
      },
      () => {},
    );
    return () => {
      cancelled = true;
    };
  }, [start, sync]);

  const receive = useCallback(
    (message: ChatwootMessage) => {
      if (message.conversation_id !== conversationRef.current?.id) return false;
      // The visitor's own words are on screen from the moment they were typed.
      if (message.sender?.type === "contact") return false;

      if (message.sender?.type === "agent_bot" && streamedHere.current) {
        const reply = awaitingCopy.current;
        awaitingCopy.current = null;
        if (reply !== null) replace(reply, (held) => tagged(held, message.id));
        return false;
      }

      const row = heldFromChatwoot(message);
      if (row === null) return false;
      setMessages((held) => (holds(held, String(message.id)) ? held : [...held, row]));
      return message.sender?.type === "user";
    },
    [replace, setMessages],
  );

  const move = useCallback(
    (event: ChatwootConversationEvent) => {
      if (event.id !== conversationRef.current?.id) return;
      const { desk: next, note } = moved(deskRef.current, event);
      setDesk(next);
      if (note !== null) setMessages((held) => [...held, systemNote(note)]);
    },
    [setDesk, setMessages],
  );

  const presence = useCallback(
    (push: ChatwootPresence) => setDesk({ ...deskRef.current, staffOnline: staffOnline(push) }),
    [setDesk],
  );

  const sayTyping = useCallback(
    (on: boolean) => {
      const held = conversationRef.current;
      if (chat === null || held === null || deskRef.current.chatwoot !== "open") return;
      void chat.client.typing(held.id, on).catch(() => {});
    },
    [chat],
  );

  /** Runs one AI turn on the conversation, filling `replyId` as it streams. */
  const runBot = useCallback(
    async (
      conversation: ChatwootConversation,
      messageId: number,
      replyId: string,
      input: string,
      signal: AbortSignal,
    ) => {
      const named: FetchLike = (url, init) => {
        const headers = new Headers(init?.headers);
        headers.set(ChatwootConversationHeader, String(conversation.id));
        headers.set(ChatwootMessageHeader, String(messageId));
        return send(url, { ...init, headers });
      };

      streamedHere.current = true;
      for await (const state of runTurn({
        endpoint,
        session: { current: null },
        threadId: spiritConversationId(conversation),
        input,
        abortSignal: signal,
        fetch: named,
      })) {
        replace(replyId, (held) => replyFrom(held, state));
      }

      replace(replyId, (held) => ({ ...held, status: { type: "complete", reason: "stop" } }));
      awaitingCopy.current = replyId;
    },
    [endpoint, replace, send],
  );

  const onNew = useCallback(
    async (message: AppendMessage) => {
      const userId = crypto.randomUUID();
      const input = flatten({ ...message, id: userId } as ThreadMessage).content;

      setMessages((held) => [
        ...held,
        { id: userId, role: "user", content: message.content, createdAt: new Date() },
      ]);
      setRunning(true);

      const controller = new AbortController();
      abortRef.current = controller;

      // Made only for an AI turn: a person answers in their own time.
      let replyId: string | null = null;

      try {
        const started = await start();
        const { client } = started;
        // A start that failed on mount opens the socket from here.
        setChat(started);

        let held = conversationRef.current;
        if (held === null) {
          held = await client.createConversation();
          adopt(held);
          setDesk({ ...deskRef.current, chatwoot: held.status, status: "bot" });
          setStore((s) => ({ ...s, olderCursor: null }));
        }

        const posted = await client.post(held.id, input);
        replace(userId, (row) => tagged(row, posted.id));

        // A person has the chat: Chatwoot is all there is. A resolved chat reopens as `pending`
        // on this very post, so it goes to the AI.
        if (deskRef.current.chatwoot === "open") return;

        const reply = pendingReply();
        replyId = reply.id;
        setMessages((rows) => [...rows, reply]);

        try {
          await runBot(held, posted.id, reply.id, input, controller.signal);
        } catch (error) {
          if (!(error instanceof TurnRefusedError && error.status === 409)) throw error;
          // The chat is not the AI's any more; the socket says whose it is.
          const gone = reply.id;
          replyId = null;
          setMessages((rows) => rows.filter((row) => row.id !== gone));
        }
      } catch (error) {
        const status = {
          type: "incomplete" as const,
          reason: controller.signal.aborted ? ("cancelled" as const) : ("error" as const),
          error: error instanceof Error ? error.message : String(error),
        };
        // Read through a widening: the narrowing here cannot see the assignment above.
        const id = replyId as string | null;

        setMessages((rows) =>
          id !== null && rows.some((row) => row.id === id)
            ? rows.map((row) => (row.id === id ? { ...row, status } : row))
            : [...rows, { ...pendingReply(), status }],
        );
      } finally {
        if (abortRef.current === controller) abortRef.current = null;
        setRunning(false);
      }
    },
    [adopt, replace, runBot, setDesk, setMessages, start],
  );

  const onCancel = useCallback(async () => {
    abortRef.current?.abort();
  }, []);

  const runtime = useExternalStoreRuntime<Held>({
    messages: store.messages,
    isRunning,
    onNew,
    onCancel,
    convertMessage: (message) => message,
  });

  const { olderCursor } = store;
  const older = useMemo<OlderMessagesSource | undefined>(
    () =>
      chat === null || conversation === null
        ? undefined
        : {
            id: String(conversation.id),
            initialCursor: olderCursor,
            fetchPage: async (before) =>
              pageFromChatwoot(await chat.client.messages(conversation.id, Number(before))),
            merge: (page) => setMessages((held) => prependOlder(held, heldFromPage(page))),
          },
    [chat, conversation, olderCursor, setMessages],
  );

  return { runtime, desk, chat, receive, move, presence, sync, sayTyping, older };
}
