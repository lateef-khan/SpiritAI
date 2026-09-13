import {
  AssistantRuntimeProvider,
  fromThreadMessageLike,
  MessageNotSentError,
  useExternalStoreRuntime,
  type AppendMessage,
  type ExportedMessageRepository,
} from "@assistant-ui/react";
import { ChevronLeftIcon } from "lucide-react";
import { useMemo, useState } from "react";

import { Hidden, Thread, type ThreadComponents } from "@/components/assistant-ui/thread";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import { HostRefusedError } from "@/lib/apiClient";

import { createHandoffsApi, type Handoff } from "../api/handoffsApi";
import { useHandoffMessages } from "../hooks/useHandoffMessages";
import { HandoffChatHeader } from "./HandoffChatHeader";
import { HandoffComposer } from "./HandoffComposer";
import { HandoffComposerContext } from "./HandoffComposerContext";

type HistoryMessage = ExportedMessageRepository["messages"][number]["message"];

/** The signed-in api, built once so the pane does not allocate one per render. */
const api = createHandoffsApi();

// Slots are read by component identity, so this must stay one object across renders rather than
// a literal written inline at the `Thread` call site.
const COMPONENTS: ThreadComponents = { Welcome: Hidden, Composer: HandoffComposer };

/**
 * One handoff's transcript.
 *
 * The pane is read-only until the viewer takes the chat: before that, and once someone else has
 * it, the composer only explains why it will not send. Once the viewer holds it, the same pane
 * also sends — a reply goes out through the handoffs api and the transcript reloads to show it.
 */
export function HandoffChat({
  handoff,
  meKey,
  onChanged,
  onBack,
}: {
  handoff: Handoff;
  meKey: string;
  onChanged(next: Handoff): void;
  onBack?: () => void;
}) {
  const { history, loading, error, reload } = useHandoffMessages(handoff.callId);
  const [now] = useState(() => new Date());

  return (
    <div className="flex h-full flex-col">
      <div className="flex items-center border-b">
        {onBack ? <BackButton onBack={onBack} /> : null}
        <div className="min-w-0 flex-1">
          <HandoffChatHeader handoff={handoff} now={now} meKey={meKey} onChanged={onChanged} />
        </div>
      </div>

      <div className="min-h-0 flex-1">
        {loading ? (
          <MessagesSkeleton />
        ) : error ? (
          <p className="p-3.5 text-sm text-destructive">{error}</p>
        ) : (
          <HandoffThread handoff={handoff} meKey={meKey} history={history} reload={reload} />
        )}
      </div>
    </div>
  );
}

function BackButton({ onBack }: { onBack: () => void }) {
  return (
    <Button
      type="button"
      variant="ghost"
      size="icon"
      aria-label="Back"
      onClick={onBack}
      className="ml-2 shrink-0 md:hidden"
    >
      <ChevronLeftIcon />
    </Button>
  );
}

/** Joins an appended message's text parts into the one string the host's reply route takes. */
function textOf(message: AppendMessage): string {
  return message.content
    .filter((part) => part.type === "text")
    .map((part) => part.text)
    .join("\n");
}

function HandoffThread({
  handoff,
  meKey,
  history,
  reload,
}: {
  handoff: Handoff;
  meKey: string;
  history: ReturnType<typeof useHandoffMessages>["history"];
  reload: () => void;
}) {
  const messages = useMemo(() => history?.messages.map((item) => item.message) ?? [], [history]);
  const [sending, setSending] = useState(false);
  const [sendError, setSendError] = useState<string | null>(null);

  const canReply = handoff.status === "human" && handoff.assignee?.key === meKey;

  const runtime = useExternalStoreRuntime({
    messages,
    // `sending` covers only the reply POST, not a reply Spirit is composing, so it gates
    // `send()` through `isSendDisabled` rather than driving `isRunning` and drawing an
    // empty assistant bubble while the POST is in flight.
    isSendDisabled: sending,
    isDisabled: !canReply,
    onNew: async (message) => {
      setSending(true);
      setSendError(null);

      try {
        await api.reply(handoff.callId, textOf(message));
        reload();
      } catch (failure) {
        setSendError(
          failure instanceof HostRefusedError
            ? (failure.title ?? failure.message)
            : failure instanceof Error
              ? failure.message
              : String(failure),
        );
        // The composer clears the draft the moment `send()` starts and restores it only when
        // `onNew` rejects with this error, so a failed send hands the text back instead of
        // losing what the sender typed.
        throw new MessageNotSentError();
      } finally {
        setSending(false);
      }
    },
    // The host writes the full ThreadMessage shape already; this fills in any field a thinner
    // payload left out, so a gap on the wire degrades to "complete, reason unknown" rather than
    // crashing the renderer.
    convertMessage: (message: HistoryMessage) =>
      fromThreadMessageLike(message, message.id, { type: "complete", reason: "unknown" }),
  });

  if (messages.length === 0) {
    return <p className="p-3.5 text-sm text-muted-foreground">No messages.</p>;
  }

  return (
    <AssistantRuntimeProvider runtime={runtime}>
      <HandoffComposerContext.Provider value={{ handoff, canReply, sendError }}>
        <Thread components={COMPONENTS} />
      </HandoffComposerContext.Provider>
    </AssistantRuntimeProvider>
  );
}

function MessagesSkeleton() {
  return (
    <div className="space-y-3 p-3.5">
      {[0, 1, 2].map((row) => (
        <Skeleton key={row} className="h-16 w-full" />
      ))}
    </div>
  );
}
