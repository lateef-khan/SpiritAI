import {
  useExternalStoreRuntime,
  type AppendMessage,
  type AssistantRuntime,
  type ThreadMessage,
} from "@assistant-ui/react";
import { useCallback, useEffect, useMemo, useRef, useState } from "react";

import { flatten, sourceContent, toolContent } from "@/features/threads/AgentCoreRuntime";
import { runTurn, TurnRefusedError, type TurnState } from "@/features/threads/transport";
import type { OlderMessagesSource } from "@/lib/history";
import { HostRefusedError, type FetchLike } from "@/lib/apiClient";
import { readVisitorMemory, rememberCall } from "../api/visitorIdentity";
import type { WidgetApi, WireHandoffMessage } from "../api/widgetApi";
import { heldFromPage, holds, hostMessageId, prependOlder, reloadPage, type Held } from "./held";
import type { HandoffDesk } from "./useHandoffDesk";

/**
 * The widget's runtime: a store the widget owns, bound to the host's public routes.
 */

/** The tool the bot calls to ask for a person, as `spirit.yaml` names it. */
const RequestHumanTool = "request_human";

/** Maps one streamed state onto the reply, the way the app's adapter does. */
function replyFrom(reply: Held, state: TurnState): Held {
  return {
    ...reply,
    content: [
      ...state.tools.map(toolContent),
      ...state.sources.map(sourceContent),
      ...(state.text.length > 0 ? [{ type: "text" as const, text: state.text }] : []),
    ],
    metadata: {
      custom: {
        stage: state.stage,
        isTerminal: state.isTerminal,
        speaker: state.speaker,
        hostMessageId: state.replyMessageId,
      },
    },
  };
}

/**
 * Whether a refusal says the host has forgotten the call, rather than anything else.
 *
 * A 404 on any public route named with this call means that. The door in front of the chat
 * answers a problem body with no code ("No such thread.") once the row is gone, and AgentCore
 * itself answers `continuation_not_found`; both are the same fact, so the code is not consulted.
 */
function callIsGone(error: unknown): boolean {
  return (
    (error instanceof HostRefusedError || error instanceof TurnRefusedError) && error.status === 404
  );
}

/**
 * Whether a refusal says the chat changed hands since the state was last read.
 *
 * Both doors answer 409 for exactly that: the chat door with `handoff_open` while a person has
 * the chat, the handoff door with "The assistant has this chat." once nobody does.
 */
function wrongDoor(error: unknown): boolean {
  return (
    (error instanceof HostRefusedError || error instanceof TurnRefusedError) && error.status === 409
  );
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

/** Whether the desk's state sends words to a person rather than the bot. */
function withPerson(status: string): boolean {
  return status === "waiting" || status === "human";
}

/**
 * One message of the human phase as the widget holds it: a staff reply, or the host saying who
 * joined or left. Drawn under the speaker's name, the way `speaker.tsx` reads it.
 */
function heldFrom(message: WireHandoffMessage): Held {
  return {
    id: message.messageId,
    role: "assistant",
    content: [{ type: "text", text: message.text }],
    createdAt: new Date(message.at),
    status: { type: "complete", reason: "stop" },
    metadata: {
      custom: {
        ...(message.speaker ? { speaker: message.speaker } : {}),
        hostMessageId: message.messageId,
      },
    },
  };
}

/** What the widget hands `AssistantRuntimeProvider`, and the ways the outside reaches in. */
export type WidgetRuntime = {
  readonly runtime: AssistantRuntime;
  /** The call the widget talks in, or `null` until the first send makes one. */
  readonly callId: string | null;
  /** Takes one pushed message of the human phase. The visitor's own are already on screen. */
  receive(message: WireHandoffMessage): void;
  /** Reads the newest page again from the host. What it covers is replaced, not doubled. */
  reload(): Promise<void>;
  /** Where the pages before what is held come from, or `undefined` until there is a call. */
  readonly older: OlderMessagesSource | undefined;
};

/**
 * What the widget holds, and where the page before it starts.
 *
 * One state rather than two: a reload decides both from the same look at the rows it lands on,
 * and two setters would let a push slip in between them.
 */
type Store = {
  readonly messages: readonly Held[];
  /** As {@link OlderMessagesSource.initialCursor}: `undefined` until a page has been read. */
  readonly olderCursor: string | null | undefined;
};

/**
 * Binds assistant-ui to the widget's own thread on the host.
 *
 * @param endpoint The public Responses route.
 * @param api The widget's thread routes.
 * @param send How a turn reaches the host. Must carry the visitor's key.
 * @param desk Where the chat stands, and how to read it again.
 * @returns The runtime to hand to `AssistantRuntimeProvider`.
 */
export function useWidgetRuntime(
  endpoint: string,
  api: WidgetApi,
  send: FetchLike,
  desk: HandoffDesk,
): WidgetRuntime {
  const [store, setStore] = useState<Store>({ messages: [], olderCursor: undefined });
  const { messages, olderCursor } = store;
  const [isRunning, setRunning] = useState(false);
  const [callId, setCallId] = useState<string | null>(() => readVisitorMemory().callId);
  const abortRef = useRef<AbortController | null>(null);

  const setMessages = useCallback(
    (next: (held: readonly Held[]) => readonly Held[]) =>
      setStore((s) => ({ ...s, messages: next(s.messages) })),
    [],
  );

  // A call just made has nothing older than what is on screen; no call has no pages at all.
  const remember = useCallback((id: string | null) => {
    rememberCall(id);
    setCallId(id);
    setStore((s) => ({ ...s, olderCursor: id === null ? undefined : null }));
  }, []);

  /**
   * Reads the newest page from the host. A 404 means the host has forgotten the call — a
   * retention sweep, a database reset — and the widget forgets it too. Any other refusal keeps
   * the id and what is on screen; the next send finds out.
   */
  const reload = useCallback((): Promise<void> => {
    const id = readVisitorMemory().callId;
    if (id === null) return Promise.resolve();

    return api.history(id).then(
      (page) => {
        setStore((s) => {
          const { messages, kept } = reloadPage(s.messages, heldFromPage(page.repository));
          // Older rows kept above the page were paged in from the cursor already held; the
          // page's own cursor names where they start, which is the wrong place to page from.
          // Rows kept with no cursor held yet were typed here, and the page's cursor stands.
          return {
            messages,
            olderCursor: kept ? (s.olderCursor ?? page.nextCursor) : page.nextCursor,
          };
        });
      },
      (error: unknown) => {
        if (callIsGone(error)) remember(null);
      },
    );
  }, [api, remember]);

  // A remembered call is restored on mount.
  useEffect(() => {
    void reload();
  }, [reload]);

  const receive = useCallback(
    (message: WireHandoffMessage) => {
      // The visitor's own words are on screen from the moment they were typed; the push is the
      // host telling staff. A row the reload already brought is not doubled either.
      if (message.role === "user") return;
      setMessages((held) => (holds(held, message.messageId) ? held : [...held, heldFrom(message)]));
    },
    [setMessages],
  );

  const replace = useCallback(
    (id: string, next: (held: Held) => Held) => {
      setMessages((held) => held.map((message) => (message.id === id ? next(message) : message)));
    },
    [setMessages],
  );

  /** Runs one bot turn under `callId`, filling `replyId` as it streams. */
  const runBot = useCallback(
    async (
      callId: string,
      replyId: string,
      input: string,
      origin: { message_id: string; parent_id: string | null },
      signal: AbortSignal,
    ) => {
      let askedForHuman = false;

      for await (const state of runTurn({
        endpoint,
        session: { current: null },
        threadId: callId,
        input,
        abortSignal: signal,
        fetch: send,
        origin,
      })) {
        askedForHuman ||= state.tools.some((tool) => tool.name === RequestHumanTool);
        replace(replyId, (held) => replyFrom(held, state));
      }

      replace(replyId, (held) => ({ ...held, status: { type: "complete", reason: "stop" } }));

      // The bot asked for a person. The row exists on the host now, and the desk is the only
      // one who can say where in the line the chat stands.
      if (askedForHuman) await desk.refresh(callId);
    },
    [desk, endpoint, replace, send],
  );

  const onNew = useCallback(
    async (message: AppendMessage) => {
      const userId = crypto.randomUUID();
      const user: Held = {
        id: userId,
        role: "user",
        content: message.content,
        createdAt: new Date(),
      };

      // The message the new one hangs off is whatever the widget held last, under the name the
      // host knows it by. Null at the root of the call.
      const parent = messages.at(-1);
      const origin = {
        message_id: userId,
        parent_id: parent ? hostMessageId(parent) : null,
      };
      const input = flatten({ ...message, id: userId } as ThreadMessage).content;

      setMessages((held) => [...held, user]);
      setRunning(true);

      const controller = new AbortController();
      abortRef.current = controller;

      // The reply is made only for a bot turn. A person answers in their own time, and an empty
      // row drawn as running would promise otherwise.
      let reply: Held | null = null;

      const bot = async (callId: string) => {
        if (reply === null) {
          const made = pendingReply();
          reply = made;
          setMessages((held) => [...held, made]);
        }
        await runBot(callId, reply.id, input, origin, controller.signal);
      };

      const person = async (callId: string) => {
        if (reply !== null) {
          const { id } = reply;
          setMessages((held) => held.filter((m) => m.id !== id));
          reply = null;
        }
        const created = await api.say(callId, input);
        replace(userId, (held) => ({
          ...held,
          metadata: { custom: { hostMessageId: created.messageId } },
        }));
      };

      const through = (status: string, callId: string) =>
        withPerson(status) ? person(callId) : bot(callId);

      try {
        let id = readVisitorMemory().callId;
        if (id === null) {
          id = await api.createThread();
          remember(id);
        }

        try {
          await through(desk.state.status, id);
        } catch (error) {
          if (wrongDoor(error)) {
            // The chat changed hands since the state was read. Read it again and go through
            // the other door, once.
            const fresh = await desk.refresh(id);

            await through(fresh.status, id);
          } else if (callIsGone(error)) {
            // The host forgot the call. The words on screen are the visitor's and stay; the host
            // starts a fresh call for them, and a fresh call is always with the bot. Once.
            remember(null);

            const fresh = await api.createThread();

            remember(fresh);

            await bot(fresh);
          } else {
            throw error;
          }
        }
      } catch (error) {
        // The failure lands on the reply when there is one — with whatever it had streamed — and
        // on a reply made for the purpose when a person's door refused.
        const status = {
          type: "incomplete" as const,
          reason: controller.signal.aborted ? ("cancelled" as const) : ("error" as const),
          error: error instanceof Error ? error.message : String(error),
        };

        // Read through a widening: the closures above assign `reply`, which the narrowing here
        // cannot see.
        const id = (reply as Held | null)?.id;

        setMessages((held) =>
          id !== undefined && held.some((m) => m.id === id)
            ? held.map((m) => (m.id === id ? { ...m, status } : m))
            : [...held, { ...pendingReply(), status }],
        );
      } finally {
        if (abortRef.current === controller) abortRef.current = null;

        setRunning(false);
      }
    },
    [api, desk, messages, remember, replace, runBot, setMessages],
  );

  const onCancel = useCallback(async () => {
    abortRef.current?.abort();
  }, []);

  const runtime = useExternalStoreRuntime<Held>({
    messages,
    isRunning,
    onNew,
    onCancel,
    convertMessage: (message) => message,
  });

  const older = useMemo<OlderMessagesSource | undefined>(
    () =>
      callId === null
        ? undefined
        : {
            id: callId,
            initialCursor: olderCursor,
            fetchPage: (before) => api.history(callId, before),
            merge: (page) => setMessages((held) => prependOlder(held, heldFromPage(page))),
          },
    [api, callId, olderCursor, setMessages],
  );

  return { runtime, callId, receive, reload, older };
}
