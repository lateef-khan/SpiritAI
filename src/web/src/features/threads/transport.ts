/**
 * Everything the chat UI does over the wire, with no React and no assistant-ui in sight.
 *
 * This file is separate from {@link ./AgentCoreRuntime.ts} so it can be tested. The parts that are
 * easy to get wrong here — a server-sent event split across two reads, a conversation id that
 * outlives the call it names, a 404 for a call the host forgot — all fail in ways a browser shows
 * as a chat that simply stops working, with nothing in any log. The hook is a thin wrapper over
 * this.
 */

/** The conversation id the reply files the turn under, on the request and on the answer. */
export const ConversationField = "conversation";

/**
 * The header naming the stage the turn speaks in.
 *
 * It arrives with the response headers, before the first token, which is the only reason the stage
 * can be shown while the turn is still running: the `metadata` block that carries `stage_after`
 * rides the *last* event, so it says where the machine ended up, never where it is.
 */
export const StageHeader = "X-AgentCore-Stage";

/** The prefix every server-sent event's data line carries. */
const DataPrefix = "data: ";

/** The closing event of a Responses stream. */
const CompletedType = "response.completed";

/** The code the endpoint answers when the named conversation is gone. */
export const ContinuationNotFound = "continuation_not_found";

/**
 * One message, in the only shape the title route reads.
 *
 * The Responses endpoint itself takes only the last user text, but the title route still takes
 * the thread's messages whole — so `flatten` keeps returning this, and `wireMessages` narrows
 * it to the turn's words at the turn boundary.
 */
export type WireMessage = {
  readonly role: string;
  readonly content: string;
};
/**
 * Who produced a message, when that is not simply "the agent".
 *
 * Nothing on the Responses stream sends this yet. It is declared here so the browser already has
 * somewhere to put a human rep the day AgentCore can hand a conversation to one: the runtime
 * and the message metadata are the two places that would otherwise both need changing at once,
 * under time pressure, while a customer is waiting on the other end of a live handoff.
 */
export type Speaker = {
  /** `agent` is the model, `human` a real person, `system` the host speaking for itself. */
  readonly kind: "agent" | "human" | "system";
  readonly name: string;
  /** A role, team, or anything else worth showing under the name. Optional. */
  readonly detail?: string;
};
/** One thing the host asked the browser to draw. */
export type RenderPart = {
  readonly name: string;
  readonly data: unknown;
};

/**
 * One source the host cited, as the browser holds it.
 *
 * Producer-neutral by design: `origin` names what produced it — `knowledge` today — so a later web
 * search or parts lookup arrives on the same field and draws with the same component.
 */
export type SourcePart = {
  readonly id: string;
  readonly sourceType: "document" | "url";
  readonly title: string;
  /** Where inside the source it sits, such as `p.27`. Empty when the producer has none. */
  readonly locator: string;
  /** The link to open, or `null` when there is nothing to open. A document has none. */
  readonly url: string | null;
  readonly mediaType: string;
  /** What produced it, such as `knowledge`. */
  readonly origin: string;
  /** The tool call it was cited under. */
  readonly callId: string;
};

/** One source as the wire spells it. Every field is optional: the browser never trusts the host's shape. */
export type SourceFrame = {
  call_id?: string;
  id?: string;
  source_type?: string;
  title?: string;
  locator?: string;
  url?: string | null;
  media_type?: string;
  origin?: string;
};

/** Anything `JSON.parse` can produce. */
export type JsonValue =
  string | number | boolean | null | readonly JsonValue[] | { readonly [key: string]: JsonValue };

/** A JSON object, which is the only shape a tool's arguments ever take. */
export type JsonObject = { readonly [key: string]: JsonValue };

/**
 * One tool the host ran, with its result once it has one.
 *
 * Both halves of a call arrive as separate chunks and are folded together here, because the screen
 * wants one row per tool that fills in, and not two rows that have to be matched up by whoever draws
 * them. A part with no `result` yet is a tool still running.
 *
 * This is a *report* and never a request. AgentCore owns the tool loop, so nothing the browser does
 * with this can make a tool run, and the OpenAI `tool_calls` field — which does mean "you run this"
 * — is deliberately not what carries it.
 */
export type ToolPart = {
  readonly callId: string;
  readonly name: string;
  readonly arguments: JsonObject;
  /** What the tool answered, in whatever shape it answered in. Absent while it is still running. */
  readonly result?: unknown;
  /** Whether the tool failed. Absent while it is still running. */
  readonly failed?: boolean;
  /** The approval gate waiting on the caller, when the host asks before running. */
  readonly approval?: ApprovalAsk;
};

/** One tool call waiting on the caller: which request an answer carries back. */
export type ApprovalAsk = {
  /** The id the approval answer carries back. */
  readonly requestId: string;
};

/** Everything one turn has produced so far. */
export type TurnState = {
  readonly text: string;
  readonly data: readonly RenderPart[];
  /** Every tool this turn has called, in call order, each with its result once it has one. */
  readonly tools: readonly ToolPart[];
  /** Every source this turn cited, in cite order. */
  readonly sources: readonly SourcePart[];
  /** The stage the pipeline is in: the turn's own stage, then the stage it moved to at the end. */
  readonly stage: string | null;
  /** Whether the stage the turn moved to ends the call. Only ever true on the final state. */
  readonly isTerminal: boolean;
  /** Who is speaking, when the host says. Always null on Responses: kept for the handoff future. */
  readonly speaker: Speaker | null;
  /** What the host called this reply, once it says. Null until the closing event carries it. */
  readonly replyMessageId: string | null;
};

/** The id of the open call, held for the life of the tab. */
export type Session = {
  current: string | null;
};

/** The part of `fetch` this module uses, so a test can hand it one that reaches no network. */
export type FetchLike = (url: string, init: RequestInit) => Promise<Response>;

/** Where the caller says a turn's words hang, in the conversation it draws. */
export type TurnOrigin = {
  /** What this client calls the message it is sending. */
  readonly message_id?: string;
  /**
   * What this client calls the message the new one hangs off, or null at the root of the call.
   *
   * The host withdraws everything the call said after this message before it runs the turn. On an
   * ordinary send that is the last thing the call said, so nothing goes; it is an edit exactly when
   * the parent is further back, and then the answers to the replaced question stop reaching the
   * model.
   */
  readonly parent_id?: string | null;
};

/** What one turn needs to run. */
export type TurnOptions = {
  readonly endpoint: string;
  readonly session: Session;
  /** The turn's words: the last user text, which the endpoint runs. */
  readonly input: string;
  readonly abortSignal: AbortSignal;
  readonly fetch: FetchLike;
  /** Omitted by a caller that does not track its messages by name. */
  readonly origin?: TurnOrigin;
  /** Answers a pending approval instead of sending words. */
  readonly approval?: ApprovalAnswer;
  /**
   * The thread the turn belongs to, when a thread list owns it. Sent as the conversation the
   * turn runs under, so the host files the call under the id the thread already has. Never
   * taken from a reply: the reply's id is the call's own bookkeeping, not the thread's.
   */
  readonly threadId?: string;
};

/** One approval answer: which request the caller answers, and whether the tool may run. */
export type ApprovalAnswer = {
  readonly requestId: string;
  readonly approved: boolean;
};

/** What the endpoint files on the finished answer beside the Responses shape. */
export type TurnMetadata = {
  readonly call_id?: string;
  readonly turn_index?: string;
  readonly stage_before?: string;
  readonly stage_after?: string;
  readonly is_terminal?: string;
  /**
   * What the host called the reply it just wrote.
   *
   * Needed because the message an edit hangs off is usually a reply, and a reply has no name until
   * the host writes it — so this client cannot invent one and has to be told.
   */
  readonly message_id?: string;
  /** The pending approval asks, JSON-encoded, when the turn asked any. */
  readonly approvals?: string;
};

/** One half of one tool call, as the endpoint writes it. */
export type ToolFrame = {
  call_id?: string;
  name?: string;
  phase?: string;
  /** Typed rather than `unknown` because it comes off `JSON.parse` of a field the host writes as an object. */
  arguments?: JsonObject;
  /** The host writes the answer as JSON, so this is an object as often as it is a string. */
  result?: unknown;
  failed?: boolean;
};

/** One tool call waiting on the caller, as the endpoint writes it. */
export type ApprovalFrame = {
  request_id?: string;
  tool?: string;
  arguments?: JsonObject;
};

/**
 * One parsed data line of a Responses stream.
 *
 * Text arrives as `response.output_text.delta`; the turn facts arrive on `response.completed`;
 * drawings, citations, tool halves and approval asks arrive as bare `agentcore_*` members with
 * no `type`. Reads are guarded at use, not here: the browser never trusts the host's shape.
 */
export type StreamChunk = {
  readonly type?: string;
  readonly delta?: string;
  readonly response?: {
    readonly metadata?: TurnMetadata;
    readonly conversation?: { readonly id?: string };
  };
  readonly agentcore_data?: RenderPart;
  readonly agentcore_source?: SourceFrame;
  readonly agentcore_tool?: ToolFrame;
  readonly agentcore_approval?: ApprovalFrame;
};

/** The body of one refusal. */
type WireError = {
  error?: { message?: string; code?: string };
};

/**
 * The last user text, which the endpoint runs. The call owns the history, so earlier turns stay
 * on the host and a turn carries its own words alone; an edit is an origin, not a replay.
 */
export function wireMessages(messages: readonly WireMessage[]): string {
  for (let at = messages.length - 1; at >= 0; at -= 1) {
    const message = messages[at];
    if (message && message.role === "user" && message.content.length > 0) {
      return message.content;
    }
  }
  return "";
}

/**
 * Splits whatever has arrived into whole events and whatever is still incomplete.
 *
 * An event ends at a blank line and a read can end anywhere, so the tail of a read is very often
 * half an event. Returning it rather than parsing it is the whole job.
 */
export function splitEvents(buffer: string): { events: string[]; rest: string } {
  const parts = buffer.split("\n\n");
  const rest = parts.pop() ?? "";
  return { events: parts, rest };
}

/** Reads one event's data lines, skipping keep-alives and halves split across reads. */
export function readEvent(event: string): StreamChunk[] {
  const chunks: StreamChunk[] = [];
  for (const line of event.split("\n")) {
    const trimmed = line.trim();
    if (!trimmed.startsWith(DataPrefix)) {
      continue;
    }
    const parsed: unknown = safeParse(trimmed.slice(DataPrefix.length));
    if (parsed && typeof parsed === "object") {
      chunks.push(parsed as StreamChunk);
    }
  }
  return chunks;
}

function safeParse(text: string): unknown {
  try {
    return JSON.parse(text) as unknown;
  } catch {
    // Half a JSON body split across two reads: dropped here, reread whole on the next pass.
    return null;
  }
}

/** Reads the failure the endpoint wrote, or falls back to the status line. */
async function failureOf(response: Response): Promise<{ message: string; code?: string }> {
  try {
    const body = (await response.json()) as WireError;
    if (body.error?.message) {
      return { message: body.error.message, code: body.error.code };
    }
  } catch {
    // A body that is not the documented shape tells us nothing the status line does not.
  }

  return { message: `the request failed with status ${response.status}.` };
}

/** Posts one turn. Always streams: the UI draws the reply as it arrives. */
function post(options: TurnOptions, session: string | null): Promise<Response> {
  // A thread-owned turn names its thread as the conversation, so the host files the call under
  // the id the thread list already has. A bare tab keeps its minted conversation in the session.
  const conversation = options.threadId ?? session;
  return options.fetch(options.endpoint, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({
      ...(conversation ? { [ConversationField]: conversation } : {}),
      input: options.approval ? [] : options.input,
      stream: true,
      // The dialect is opt-in: only a turn carrying `agentcore` gets the drawing, citation,
      // tool-half and approval lines. `message_id` keeps the edit/anchor contract the chat
      // endpoint had, so the runtime keeps sending the origin it already builds.
      agentcore: {
        ...(options.origin?.message_id
          ? { message_id: options.origin.message_id }
          : { message_id: crypto.randomUUID() }),
        ...(options.origin && "parent_id" in options.origin
          ? { parent_id: options.origin.parent_id }
          : {}),
        ...(options.approval
          ? {
              approval: {
                request_id: options.approval.requestId,
                approved: options.approval.approved,
              },
            }
          : {}),
      },
    }),
    signal: options.abortSignal,
  });
}

/**
 * Folds one tool frame into the list the state carries.
 */
export function foldTool(tools: readonly ToolPart[], frame: ToolFrame): readonly ToolPart[] {
  const callId = frame.call_id;
  if (!callId) {
    return tools;
  }

  const name = frame.name ?? callId;

  if (frame.phase !== "result") {
    return [...tools, { callId, name, arguments: frame.arguments ?? {} }];
  }

  const answered: ToolPart = {
    callId,
    name,
    arguments: {},
    result: frame.result ?? "",
    failed: frame.failed ?? false,
  };

  const index = tools.findIndex((tool) => tool.callId === callId);
  if (index < 0) {
    // A result with no call before it should not happen. Showing the answer with no question still
    // beats showing nothing and leaving the caller wondering what the wait was for.
    return [...tools, answered];
  }

  const merged = [...tools];
  merged[index] = { ...tools[index], ...answered, arguments: tools[index].arguments };
  return merged;
}

/**
 * Folds one approval ask into the tools the state carries, matched to its call.
 *
 * The ask arrives beside the call half rather than inside it, so it lands on the tool the
 * request names — or on a bare row carrying the gate when the call half never arrived.
 */
export function foldApproval(
  tools: readonly ToolPart[],
  frame: ApprovalFrame,
): readonly ToolPart[] {
  const requestId = frame.request_id;
  if (!requestId) {
    return tools;
  }

  const name = frame.tool ?? requestId;
  const at = tools.findIndex(
    (tool) => tool.name === frame.tool && tool.approval === undefined && tool.result === undefined,
  );
  const ask: ApprovalAsk = { requestId };
  if (at < 0) {
    return [...tools, { callId: name, name, arguments: frame.arguments ?? {}, approval: ask }];
  }

  const next = [...tools];
  next[at] = { ...tools[at], approval: ask };
  return next;
}

/**
 * Folds one wire frame into the sources held so far.
 *
 * Keyed by id, last write winning in the place the first took: the host already de-duplicates
 * within a turn, and this holds the line for a stream that reconnects mid-turn.
 */
export function foldSource(
  sources: readonly SourcePart[],
  frame: SourceFrame,
): readonly SourcePart[] {
  const id = frame.id;
  if (!id) {
    return sources;
  }

  const part: SourcePart = {
    id,
    sourceType: frame.source_type === "url" ? "url" : "document",
    title: frame.title ?? id,
    locator: frame.locator ?? "",
    url: frame.url ?? null,
    mediaType: frame.media_type ?? "text/plain",
    origin: frame.origin ?? "",
    callId: frame.call_id ?? "",
  };

  const at = sources.findIndex((existing) => existing.id === id);
  if (at < 0) {
    return [...sources, part];
  }

  const next = [...sources];
  next[at] = part;
  return next;
}

/**
 * Runs one turn and yields the reply as it grows.
 *
 * Each yield is the whole reply so far rather than the newest piece, because that is what the
 * runtime above renders.
 *
 * A thread-owned turn sends its thread id as the conversation and never touches the session:
 * the thread already names the call, so adopting the reply's id would keep a second id in step
 * for nothing. A bare tab has no thread and keeps the minted conversation in the session, so a
 * second turn can send it back.
 */
export async function* runTurn(options: TurnOptions): AsyncGenerator<TurnState> {
  const { session } = options;

  let response = await post(options, session.current);

  // Continuations live in AgentCore's store, which a restart wipes. An id from before one names
  // a call that is gone; starting a new call is what the caller wanted, and failing the turn
  // over an id they never saw is not. A thread-owned turn never retries: its conversation is
  // the thread itself, minted by the thread list — retrying nameless would orphan it.
  if (response.status === 404 && !options.threadId) {
    const failure = await failureOf(response);
    if (failure.code !== ContinuationNotFound) {
      throw new Error(failure.message);
    }

    session.current = null;
    response = await post(options, null);
  }

  if (!response.ok) {
    throw new Error((await failureOf(response)).message);
  }

  if (!response.body) {
    throw new Error("the host answered with no body, so there is nothing to read.");
  }

  // Named before the first token, so the very first yield already knows the stage.
  let stage = response.headers.get(StageHeader);
  let isTerminal = false;
  let replyMessageId: string | null = null;

  const reader = response.body.pipeThrough(new TextDecoderStream()).getReader();
  let pending = "";
  let text = "";
  let data: RenderPart[] = [];
  let tools: readonly ToolPart[] = [];
  let sources: readonly SourcePart[] = [];

  try {
    for (;;) {
      const { done, value } = await reader.read();
      if (done) {
        break;
      }

      pending += value;

      const { events, rest } = splitEvents(pending);
      pending = rest;

      for (const event of events) {
        for (const chunk of readEvent(event)) {
          const state = () => ({
            text,
            data,
            tools,
            sources,
            stage,
            isTerminal,
            speaker: null,
            replyMessageId,
          });
          // The closing event carries the turn facts: where the machine moved to, what it
          // called the reply, and whether the call is over. It yields on its own only when it
          // says something the stream has not already — a bare close otherwise just ends the
          // turn without repeating the last words back.
          const info = chunk.type === CompletedType ? chunk.response?.metadata : undefined;
          const saysSomething =
            info !== undefined &&
            ((info.stage_after !== undefined && info.stage_after !== stage) ||
              (info.message_id !== undefined && info.message_id !== replyMessageId) ||
              (info.is_terminal !== undefined &&
                ((info.is_terminal === "true") !== isTerminal || info.is_terminal === "true")));
          if (info) {
            stage = info.stage_after ?? stage;
            if (info.is_terminal === "true") {
              isTerminal = true;
            } else if (info.is_terminal === "false") {
              isTerminal = false;
            }
            replyMessageId = info.message_id ?? replyMessageId;
            if (saysSomething) {
              yield state();
            }
          }

          const rendered = chunk.agentcore_data;
          if (rendered && typeof rendered.name === "string") {
            // A new array each time: the yielded state is read after the yield, so the consumer must
            // never see a list this loop keeps changing underneath it.
            data = [...data, rendered];
            yield state();
          }

          const tool = chunk.agentcore_tool;
          if (tool) {
            tools = foldTool(tools, tool);
            yield state();
          }

          const ask = chunk.agentcore_approval;
          if (ask) {
            tools = foldApproval(tools, ask);
            yield state();
          }

          const source = chunk.agentcore_source;
          if (source) {
            sources = foldSource(sources, source);
            yield state();
          }

          if (chunk.type === "response.output_text.delta" && typeof chunk.delta === "string" && chunk.delta.length > 0) {
            text += chunk.delta;
            yield state();
          }
          // The minted conversation rides the created event; a continued one rides it too.
          // Either way it is where the next turn belongs — unless the closing event already
          // ended the call, in which case holding on would answer the next turn with a 409.
          // A thread-owned turn skips all of this: its conversation is the thread itself.
          if (!options.threadId) {
            const named = chunk.response?.conversation?.id;
            if (typeof named === "string" && named.length > 0 && !isTerminal) {
              session.current = named;
            } else if (isTerminal) {
              session.current = null;
            }
          }
        }
      }
    }
  } finally {
    reader.cancel().catch(() => {
      // The turn is over either way, and a reader that will not close is not worth a failure.
    });
  }
}
