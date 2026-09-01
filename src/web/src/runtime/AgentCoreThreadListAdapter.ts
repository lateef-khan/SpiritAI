import { createAssistantStream } from "assistant-stream";
import {
  useAui,
  type RemoteThreadListAdapter,
  type RuntimeAdapters,
  type ThreadHistoryAdapter,
} from "@assistant-ui/react";
import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { createThreadsApi, type ThreadsApi, type WireThread } from "./threadsApi.ts";
import { flatten } from "./AgentCoreRuntime.ts";

/**
 * The thread list, kept by the host.
 *
 * The host's routes were shaped to assistant-ui's own vocabulary — `remoteId`, `status`,
 * `externalId`, `lastMessageAt`, `custom` — so almost everything here is a rename and a fetch. What
 * is worth reading is the three places the two sides do *not* line up:
 *
 * 1. `initialize` is handed an id assistant-ui made up locally. It is thrown away. The host mints
 *    the call id, because a browser that chose it could choose one somebody else could predict.
 * 2. `append` on the history adapter does nothing. AgentCore writes every message to store 1 as the
 *    turn commits, so a browser that also posted them would store the conversation twice.
 * 3. `generateTitle` sends the words up rather than naming the thread here. The host has the model
 *    and writes the name straight to the thread's row, so this stream is only how the sidebar shows
 *    it without a reload.
 *
 * The widget uses none of this. Nobody behind it is signed in, so it has no thread list at all —
 * one bubble, one conversation, gone when the frame closes.
 */

/**
 * One thread as assistant-ui holds it.
 *
 * Read off the adapter rather than imported. `RemoteThreadMetadata` is exported by
 * `@assistant-ui/core` and not by `@assistant-ui/react`, and a type derived from the contract we
 * are implementing cannot drift from it.
 */
type ThreadMetadata = Awaited<ReturnType<RemoteThreadListAdapter["fetch"]>>;

/**
 * Reads one thread the way assistant-ui holds it.
 *
 * Every optional field is omitted rather than sent as `null`. assistant-ui reads a present
 * `nextCursor` as "there is another page" whatever its value, so a `null` passed straight through
 * asks the host for the next page forever.
 */
function threadOf(thread: WireThread): ThreadMetadata {
  return {
    remoteId: thread.remoteId,
    status: thread.status,
    ...(thread.externalId ? { externalId: thread.externalId } : {}),
    ...(thread.title ? { title: thread.title } : {}),
    ...(thread.lastMessageAt ? { lastMessageAt: new Date(thread.lastMessageAt) } : {}),
    ...(thread.custom ? { custom: thread.custom } : {}),
  };
}

/**
 * Holds the current `aui` in a ref.
 *
 * Not `useEffectEvent`: the history adapter's `load` runs during render, before an effect has had a
 * chance to fire, so what it reads has to be a ref rather than a closed-over value.
 *
 * @returns A ref that always holds the latest `aui`.
 */
function useAuiRef() {
  const aui = useAui();
  const ref = useRef(aui);

  useEffect(() => {
    ref.current = aui;
  });

  return ref;
}

/**
 * Gives one thread its history, read from the host.
 *
 * @param api How the host is asked.
 * @returns The adapters assistant-ui mounts around the open thread.
 */
function useServerHistory(api: ThreadsApi): RuntimeAdapters {
  const auiRef = useAuiRef();

  const [history] = useState<ThreadHistoryAdapter>(() => ({
    async load() {
      const { remoteId } = auiRef.current.threadListItem.getState();

      // A thread the host has never heard of is a thread with no words, not an error. It is the
      // one assistant-ui opens on a fresh tab before anybody has typed.
      return remoteId ? api.history(remoteId) : { messages: [] };
    },

    async append() {
      // Nothing, deliberately. AgentCore writes the turn to store 1 as it commits.
    },
  }));

  return useMemo(() => ({ history }), [history]);
}

/**
 * Reads the call id a turn should run under, making the thread on the host if it has none yet.
 *
 * This is what ties a browser thread to an AgentCore call. `initialize` resolves to the id the host
 * minted, and that same id is what the turn sends as `X-AgentCore-Session`, so one conversation has
 * one id everywhere rather than two that have to be kept in step.
 *
 * @returns A function the runtime awaits once per turn.
 */
export function useThreadSession(): () => Promise<string> {
  const auiRef = useAuiRef();

  return useCallback(async () => (await auiRef.current.threadListItem.initialize()).remoteId, []);
}

/**
 * Binds assistant-ui's thread list to the host's.
 *
 * @param api How the host is asked. Defaults to the signed-in path.
 * @returns The adapter to hand to `useRemoteThreadListRuntime`.
 */
export function createAgentCoreThreadListAdapter(
  api: ThreadsApi = createThreadsApi(),
): RemoteThreadListAdapter {
  return {
    unstable_useAdapters: function useAgentCoreThreadAdapters() {
      return useServerHistory(api);
    },

    async list(params) {
      const page = await api.list(params?.after);

      return {
        threads: page.threads.map(threadOf),
        ...(page.nextCursor ? { nextCursor: page.nextCursor } : {}),
      };
    },

    async initialize() {
      const made = await api.create();

      return {
        remoteId: made.remoteId,
        ...(made.externalId ? { externalId: made.externalId } : {}),
      };
    },

    async fetch(remoteId) {
      return threadOf(await api.fetch(remoteId));
    },

    async rename(remoteId, newTitle) {
      await api.patch(remoteId, { title: newTitle });
    },

    async updateCustom(remoteId, custom) {
      // An explicit null, because an omitted key means "leave it alone" to the host and this call
      // means the opposite.
      await api.patch(remoteId, { custom: custom ?? null });
    },

    async archive(remoteId) {
      await api.patch(remoteId, { status: "archived" });
    },

    async unarchive(remoteId) {
      await api.patch(remoteId, { status: "regular" });
    },

    async delete(remoteId) {
      await api.remove(remoteId);
    },

    async generateTitle(remoteId, messages) {
      // The words go up with the request rather than being looked up by the host. AgentCore writes
      // a turn to store 1 only once it has finished, and assistant-ui asks for a name the moment
      // the first message appears — so a host reading its own store would read an empty call. The
      // browser is the one holding the words at that point, and assistant-ui's own adapters send
      // them the same way.
      return createAssistantStream(async (controller) => {
        for await (const piece of api.title(remoteId, messages.map(flatten))) {
          controller.appendText(piece);
        }
      });
    },
  };
}
