import {
  CompositeAttachmentAdapter,
  SimpleImageAttachmentAdapter,
  SimpleTextAttachmentAdapter,
  useLocalRuntime,
  type ChatModelAdapter,
  type ChatModelRunResult,
  type CompleteAttachment,
  type MessageTiming,
  type ThreadMessage,
} from "@assistant-ui/react";
import { useRef } from "react";
import {
  runTurn,
  wireMessages,
  type Session,
  type SourcePart,
  type ToolPart,
  type TurnState,
  type WireMessage,
} from "./transport.ts";
import { authFetch } from "@/auth/authFetch";

/**
 * The bridge between assistant-ui and AgentCore's OpenAI-compatible endpoint.
 */

/**
 * What one attachment contributes to the message it rides on.
 *
 * A text file arrives already wrapped in a tag naming it, so it goes up as it is. Anything else —
 * an image — has no text to send and is named instead. The name is not decoration: a message whose
 * only content is an image would otherwise flatten to nothing, and the endpoint answers a turn with
 * no user text with a 400. Naming it keeps the turn sendable and tells the model a file it cannot
 * read was attached, rather than leaving it to answer a question about nothing.
 */
function attachmentText(attachment: CompleteAttachment): string {
  const text = attachment.content
    .filter((part): part is { type: "text"; text: string } => part.type === "text")
    .map((part) => part.text)
    .join("");

  return text.length > 0 ? text : `[attachment: ${attachment.name}]`;
}

/**
 * Flattens one assistant-ui message into the single string the OpenAI shape carries.
 *
 * AgentCore's endpoint reads text and no other content part, so anything else in the message — an
 * image, a tool call — has nothing to map onto and is left out rather than sent as `[object
 * Object]`. The thread list sends its words up the same way, so this is the app's one mapper
 * rather than one per caller.
 *
 * Attachments hang off the message rather than sitting in its content, so they are read separately
 * and placed ahead of the typed words: the model should read the material before the question asked
 * about it.
 */
export function flatten(message: ThreadMessage): WireMessage {
  const text = message.content
    .filter((part): part is { type: "text"; text: string } => part.type === "text")
    .map((part) => part.text)
    .join("");

  const attached = message.role === "user" ? message.attachments.map(attachmentText) : [];

  // Blocks joined by a newline, where the content parts above were joined by nothing: those parts
  // are pieces of one sentence, while an attachment and a question are two separate things to say.
  return {
    role: message.role,
    content: [...attached, ...(text.length > 0 ? [text] : [])].join("\n"),
  };
}

/**
 * Measures one turn, in the shape `message.metadata.timing` is read back in.
 *
 * `useLocalRuntime` does not time turns for you — assistant-ui ships `useStreamingTiming` for that,
 * but only external-store adapters call it. A chat model adapter reports its own timing instead,
 * which is what this does. The field semantics follow assistant-ui's own tracker: `streamStartTime`
 * is epoch milliseconds while `firstTokenTime` and `totalStreamTime` are milliseconds *since* it,
 * and the token count is the same length/4 estimate, so a reader cannot tell the two apart.
 */
function newTurnClock() {
  const startTime = Date.now();
  let firstTokenTime: number | undefined;
  let length = 0;
  let totalChunks = 0;
  let toolCallCount = 0;

  return {
    observe(state: TurnState) {
      // Growth, not arrival: the endpoint re-sends the whole text every frame, so an unchanged
      // length means nothing was added and must not count as a chunk.
      if (state.text.length > length) {
        firstTokenTime ??= Date.now() - startTime;
        length = state.text.length;
        totalChunks += 1;
      }
      // The wire now reports every tool the host ran, drawing tools included, so this is the count
      // itself rather than the drawings it used to be estimated from.
      toolCallCount = state.tools.length;
    },

    finish(): MessageTiming {
      const totalStreamTime = Date.now() - startTime;
      const tokenCount = Math.ceil(length / 4);

      return {
        streamStartTime: startTime,
        totalStreamTime,
        totalChunks,
        toolCallCount,
        ...(firstTokenTime !== undefined && { firstTokenTime }),
        ...(tokenCount > 0 && { tokenCount }),
        ...(totalStreamTime > 0 &&
          tokenCount > 0 && {
            tokensPerSecond: tokenCount / (totalStreamTime / 1000),
          }),
      };
    },
  };
}

/**
 * Turns one reported tool into the content part assistant-ui draws it as.
 *
 * `result` is left off entirely while the tool is still running, because that absence is what the
 * kit reads as "running" — an empty string there would draw a finished tool that answered nothing.
 */
function toolContent(tool: ToolPart) {
  return {
    type: "tool-call" as const,
    toolCallId: tool.callId,
    toolName: tool.name,
    args: tool.arguments,
    argsText: JSON.stringify(tool.arguments, null, 2),
    ...(tool.result !== undefined
      ? { result: tool.result, isError: tool.failed === true }
      : {}),
  };
}

/**
 * Turns one cited source into the content part assistant-ui draws it as.
 *
 * The two variants are not interchangeable. `url` renders a clickable chip and needs a real href;
 * `document` renders a badge and needs a title and a media type. A knowledge card has no URL, so it
 * is always a document, and a `url` source that arrives without its link degrades to one rather
 * than drawing a chip that goes nowhere.
 *
 * `parentId` is the tool call that cited it, which is how assistant-ui ties a source to the step
 * that found it. `providerMetadata` carries what is ours and not assistant-ui's: which producer
 * cited it, and where inside the source it sits.
 */
export function sourceContent(source: SourcePart) {
  const shared = {
    type: "source" as const,
    id: source.id,
    title: source.title,
    parentId: source.callId,
    providerMetadata: {
      agentcore: { origin: source.origin, locator: source.locator },
    },
  };

  if (source.sourceType === "url" && source.url) {
    return { ...shared, sourceType: "url" as const, url: source.url };
  }

  return {
    ...shared,
    sourceType: "document" as const,
    mediaType: source.mediaType,
  };
}

/**
 * Reads back the name the host stored one message under, if this one carries it.
 *
 * Only a reply ever does. The caller's own messages already travel under names this browser gave
 * them, and the host keeps those, so they need no translation.
 */
function hostMessageId(message: ThreadMessage): string | undefined {
  const custom = message.metadata?.custom as { hostMessageId?: unknown } | undefined;
  return typeof custom?.hostMessageId === "string" ? custom.hostMessageId : undefined;
}

/**
 * How a turn reaches the host.
 */
export type TurnFetch = (url: string, init: RequestInit) => Promise<Response>;

/**
 * Where the call id of a turn comes from, when something else owns it.
 *
 * The signed-in app has a thread list, and a thread *is* a call — see
 * {@link ./AgentCoreThreadListAdapter.ts}. The widget has no thread list and no signed-in caller,
 * so it has nowhere to ask and keeps the id itself.
 */
export type ThreadSession = () => Promise<string>;

/**
 * What the composer accepts when someone attaches a file.
 *
 * Without this the composer has no attachment support at all: assistant-ui reads the capability off
 * the presence of this adapter, and adding a file without one throws an error the add-attachment
 * button swallows — so the file picker opens, a file is picked, and nothing appears.
 *
 * Built once at module scope rather than per render. Both adapters are stateless — each reads a
 * file and hands back its content — so there is nothing for a second instance to own, and the
 * composer would only re-read an object that behaves identically.
 *
 * The image adapter is first because the order decides which one claims a file, and the text
 * adapter's list is the narrower of the two. An image becomes a data URL the browser draws in the
 * thread; the endpoint cannot carry it, so {@link flatten} sends the file's name in its place until
 * AgentCore's wire shape grows a content part for it.
 */
const attachments = new CompositeAttachmentAdapter([
  new SimpleImageAttachmentAdapter(),
  new SimpleTextAttachmentAdapter(),
]);

/**
 * Binds assistant-ui to one AgentCore endpoint.
 *
 * @param endpoint The route the host mapped the text endpoint on.
 * @param fetchTurn How to send a turn. Defaults to the signed-in path.
 * @param openThread Where the call id comes from, or nothing to keep one per tab.
 * @returns The runtime to hand to `AssistantRuntimeProvider`.
 */
export function useAgentCoreRuntime(
  endpoint: string,
  fetchTurn: TurnFetch = authFetch,
  openThread?: ThreadSession,
) {
  // A ref and not state: changing the session must never re-render, and the value has to be the
  // current one by the time the next turn reads it rather than on the next paint.
  const session = useRef<Session>({ current: null });

  const adapter: ChatModelAdapter = {
    async *run({ messages, abortSignal }) {
      // Asked once per turn rather than held, and that is not laziness. A stage that ends a call
      // clears the id it was given, so a held one would go null part-way through a thread that is
      // still perfectly open.
      const named = openThread ? { current: await openThread() } : session.current;

      // assistant-ui hands over the path from the root to the message being sent, so the last entry
      // is what the caller just said and the one before it is what that hangs off. On an edit the
      // parent is the message BEFORE the one replaced, which is exactly what this picks up, and the
      // abandoned branch is not in this list at all.
      const parent = messages.at(-2);

      const turn = runTurn({
        endpoint,
        session: named,
        messages: wireMessages(messages.map(flatten)),
        abortSignal,
        fetch: (url, init) => fetchTurn(url, init),
        origin: {
          message_id: messages.at(-1)?.id,

          // Null at the root of the call, which is what an edit of the first message asks for. A
          // parent the host does not know is sent under this browser's own name and simply matches
          // nothing there, which the host reads as a plain new turn.
          parent_id: parent ? (hostMessageId(parent) ?? parent.id) : null,
        },
      });

      const clock = newTurnClock();
      let content: ChatModelRunResult["content"] = [];
      let stage: ChatModelRunResult["metadata"] = undefined;

      for await (const state of turn) {
        clock.observe(state);

        // `metadata.custom` is the one slot on a message that is the app's to define. Nothing on
        // screen reads the stage or `isTerminal` — the caller is never shown which stage answered
        // them — but both stay here because they cost one field each and the alternative is
        // re-plumbing the runtime the day something does want them.
        stage = {
          custom: {
            stage: state.stage,
            isTerminal: state.isTerminal,
            // Absent today. Carried anyway so a live handoff is a server change on its own.
            speaker: state.speaker,

            // The host's own name for this reply, kept ON the message rather than in a map beside
            // it. The adapter never learns the id assistant-ui gives the message it is producing, so
            // there is nothing to key a map on — and metadata rides the message through a branch
            // switch and through the reload that restores it, which a map in a ref would not.
            hostMessageId: state.replyMessageId,
          },
        };

        // Every yield replaces the message content rather than adding to it, so each one repeats
        // everything drawn so far. Drop the repeat and a later text-only yield erases the drawing.
        // Tools first, and then the words: the host runs every tool before it speaks, so this is
        // the order the turn actually happened in.
        // Sources sit between the tools and the words: they are what the tools found, and the
        // caller should read the answer last. Every yield replaces the whole content, so a source
        // dropped from one yield would be erased from the message rather than merely not added.
        content = [
          ...state.tools.map(toolContent),
          ...state.sources.map(sourceContent),
          ...(state.text.length > 0
            ? [{ type: "text" as const, text: state.text }]
            : []),
          ...state.data.map((part) => ({
            type: "data" as const,
            name: part.name,
            data: part.data,
          })),
        ];
        yield { content, metadata: stage };
      }

      // A final yield carrying the same content, so the timing lands on the finished message
      // without blanking what was already drawn — `content` is optional on the result, but
      // omitting it here would make this yield the message's last word on its own content.
      yield { content, metadata: { ...stage, timing: clock.finish() } };
    },
  };

  return useLocalRuntime(adapter, { adapters: { attachments } });
}
