import {
  useExternalStoreRuntime,
  type AppendMessage,
  type AssistantRuntime,
  type ThreadMessage,
  type ThreadMessageLike,
} from "@assistant-ui/react";
import { useCallback, useEffect, useRef, useState } from "react";

import { flatten, sourceContent, toolContent } from "@/features/threads/AgentCoreRuntime";
import { runTurn, TurnRefusedError, type TurnState } from "@/features/threads/transport";
import { HostRefusedError, type FetchLike } from "@/lib/apiClient";
import { readVisitorMemory, rememberCall } from "../api/visitorIdentity";
import type { WidgetApi } from "../api/widgetApi";

/**
 * The widget's runtime: a store the widget owns, bound to the host's public routes.
 *
 * The signed-in app runs on `useLocalRuntime`, which owns its messages and starts a bot turn on
 * every send. The widget cannot: a chat that a person has taken needs sends that go to that person
 * and start no turn, and words that arrive over a socket while nobody typed. Both are what an
 * external store is for — the widget holds the messages, `onNew` decides where a send goes, and
 * anything may append. Today every send is a bot turn; the other branches come with the human
 * phase.
 *
 * The call id is the widget's own, kept in `localStorage` beside the visitor's key, and sent as
 * the conversation of every turn so the host files the call under it. It is made lazily, on the
 * first send: a visitor who opens the bubble and leaves makes no row.
 */

/** A message the widget holds, with the id the widget gave it. */
type Held = ThreadMessageLike & { readonly id: string };

/**
 * Reads back the name the host stored one message under, when it carries one.
 *
 * A reply does; the visitor's own words travel under the name the widget gave them, which the
 * host keeps.
 */
function hostMessageId(message: Held): string {
  const custom = message.metadata?.custom as { hostMessageId?: unknown } | undefined;
  return typeof custom?.hostMessageId === "string" ? custom.hostMessageId : message.id;
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
 * Binds assistant-ui to the widget's own thread on the host.
 *
 * @param endpoint The public Responses route.
 * @param api The widget's thread routes.
 * @param send How a turn reaches the host. Must carry the visitor's key.
 * @returns The runtime to hand to `AssistantRuntimeProvider`.
 */
export function useWidgetRuntime(
  endpoint: string,
  api: WidgetApi,
  send: FetchLike,
): AssistantRuntime {
  const [messages, setMessages] = useState<readonly Held[]>([]);
  const [isRunning, setRunning] = useState(false);
  // A ref and not state: the turn under way reads it, and cancelling must not wait for a paint.
  const abortRef = useRef<AbortController | null>(null);

  // A remembered call is restored on mount. A 404 means the host has forgotten it — a retention
  // sweep, a database reset — and the widget forgets it too. Any other refusal keeps the id and
  // shows nothing; the next send finds out.
  useEffect(() => {
    const { callId } = readVisitorMemory();
    if (callId === null) return;

    let cancelled = false;

    api
      .history(callId)
      .then((history) => {
        if (cancelled) return;
        setMessages(
          history.messages.map(({ message }) => ({
            ...message,
            id: message.id ?? crypto.randomUUID(),
          })),
        );
      })
      .catch((error: unknown) => {
        if (!cancelled && callIsGone(error)) rememberCall(null);
      });

    return () => {
      cancelled = true;
    };
  }, [api]);

  const replace = useCallback((id: string, next: (held: Held) => Held) => {
    setMessages((held) => held.map((message) => (message.id === id ? next(message) : message)));
  }, []);

  const onNew = useCallback(
    async (message: AppendMessage) => {
      const userId = crypto.randomUUID();
      const user: Held = {
        id: userId,
        role: "user",
        content: message.content,
        createdAt: new Date(),
      };
      const replyId = crypto.randomUUID();
      const reply: Held = {
        id: replyId,
        role: "assistant",
        content: [],
        createdAt: new Date(),
        status: { type: "running" },
      };

      // The message the new one hangs off is whatever the widget held last, under the name the
      // host knows it by. Null at the root of the call.
      const parent = messages.at(-1);
      const origin = {
        message_id: userId,
        parent_id: parent ? hostMessageId(parent) : null,
      };
      const input = flatten({ ...message, id: userId } as ThreadMessage).content;

      setMessages((held) => [...held, user, reply]);
      setRunning(true);

      const controller = new AbortController();
      abortRef.current = controller;

      const stream = async (callId: string) => {
        for await (const state of runTurn({
          endpoint,
          session: { current: null },
          threadId: callId,
          input,
          abortSignal: controller.signal,
          fetch: send,
          origin,
        })) {
          replace(replyId, (held) => replyFrom(held, state));
        }
      };

      try {
        let callId = readVisitorMemory().callId;
        if (callId === null) {
          callId = await api.createThread();
          rememberCall(callId);
        }

        try {
          await stream(callId);
        } catch (error) {
          // The host forgot the call. The words on screen are the visitor's and stay; the host
          // starts a fresh call for them. Once: a second 404 is an error.
          if (!callIsGone(error)) throw error;

          rememberCall(null);
          const fresh = await api.createThread();
          rememberCall(fresh);
          await stream(fresh);
        }

        replace(replyId, (held) => ({ ...held, status: { type: "complete", reason: "stop" } }));
      } catch (error) {
        replace(replyId, (held) => ({
          ...held,
          status: {
            type: "incomplete",
            reason: controller.signal.aborted ? "cancelled" : "error",
            error: error instanceof Error ? error.message : String(error),
          },
        }));
      } finally {
        if (abortRef.current === controller) abortRef.current = null;
        setRunning(false);
      }
    },
    [api, endpoint, messages, replace, send],
  );

  const onCancel = useCallback(async () => {
    abortRef.current?.abort();
  }, []);

  return useExternalStoreRuntime<Held>({
    messages,
    isRunning,
    onNew,
    onCancel,
    convertMessage: (message) => message,
  });
}
