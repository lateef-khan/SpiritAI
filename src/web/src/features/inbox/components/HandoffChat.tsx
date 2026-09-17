import {
  AssistantRuntimeProvider,
  fromThreadMessageLike,
  MessageNotSentError,
  useExternalStoreRuntime,
  type AppendMessage,
  type ExportedMessageRepository,
} from "@assistant-ui/react";
import { ChevronLeftIcon, PanelRightIcon } from "lucide-react";
import { useMemo, useState } from "react";

import { Hidden, Thread, type ThreadComponents } from "@/components/assistant-ui/thread";
import { Button } from "@/components/ui/button";
import { Sheet, SheetContent, SheetTitle, SheetTrigger } from "@/components/ui/sheet";
import { Skeleton } from "@/components/ui/skeleton";
import { TypingReporter } from "@/features/handoff/TypingReporter";
import { useIsMobile } from "@/hooks/use-mobile";
import { HostRefusedError } from "@/lib/apiClient";

import { createHandoffsApi, type Handoff } from "../api/handoffsApi";
import { HandoffChatHeader } from "./HandoffChatHeader";
import { HandoffComposer } from "./HandoffComposer";
import { HandoffComposerContext } from "./HandoffComposerContext";
import { HandoffContextPanel } from "./HandoffContextPanel";

type HistoryMessage = ExportedMessageRepository["messages"][number]["message"];

/** The signed-in api, built once so the pane does not allocate one per render. */
const api = createHandoffsApi();

const COMPONENTS: ThreadComponents = {
  Welcome: Hidden,
  Composer: HandoffComposer,
  isTranscript: true,
};

/**
 * One handoff's transcript.
 *
 * The transcript arrives as props from the one `useHandoffMessages` load the context rail
 * reads too. The pane itself is read-only until the viewer takes the chat: before that, and
 * once someone else has it, the composer only explains why it will not send. Once the viewer
 * holds it, the same pane also sends — a reply goes out through the handoffs api and the
 * transcript reloads to show it. On mobile the context rail becomes a sheet over this pane.
 */
export function HandoffChat({
  handoff,
  history,
  loading,
  error,
  reload,
  meKey,
  typing = false,
  onTyping,
  onChanged,
  onBack,
}: {
  handoff: Handoff;
  history: ExportedMessageRepository | null;
  loading: boolean;
  error: string | null;
  reload: () => void;
  meKey: string;
  typing?: boolean;
  onTyping?: (on: boolean) => void;
  onChanged(next: Handoff): void;
  onBack?: () => void;
}) {
  const [now] = useState(() => new Date());
  const isMobile = useIsMobile();

  return (
    <div className="relative flex h-full flex-col">
      <div className="flex items-center border-b">
        {onBack ? <BackButton onBack={onBack} /> : null}
        <div className="min-w-0 flex-1">
          <HandoffChatHeader
            handoff={handoff}
            now={now}
            meKey={meKey}
            typing={typing}
            onChanged={onChanged}
          />
        </div>
      </div>

      <div className="min-h-0 flex-1">
        {loading ? (
          <MessagesSkeleton />
        ) : error ? (
          <p className="p-3.5 text-sm text-destructive">{error}</p>
        ) : (
          <HandoffThread
            handoff={handoff}
            meKey={meKey}
            history={history}
            reload={reload}
            onTyping={onTyping}
          />
        )}
      </div>

      {isMobile ? (
        <Sheet>
          <SheetTrigger className="absolute end-3 top-3 z-10 rounded-md border bg-background p-1.5">
            <PanelRightIcon className="size-4" />
            <span className="sr-only">Show the context</span>
          </SheetTrigger>
          <SheetContent side="right" className="w-80 p-0">
            <SheetTitle className="sr-only">Context</SheetTitle>
            <HandoffContextPanel handoff={handoff} history={history} />
          </SheetContent>
        </Sheet>
      ) : null}
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
  onTyping,
}: {
  handoff: Handoff;
  meKey: string;
  history: ExportedMessageRepository | null;
  reload: () => void;
  onTyping?: (on: boolean) => void;
}) {
  const messages = useMemo(() => history?.messages.map((item) => item.message) ?? [], [history]);
  const [sending, setSending] = useState(false);
  const [sendError, setSendError] = useState<string | null>(null);

  const canReply = handoff.status === "human" && handoff.assignee?.key === meKey;

  const runtime = useExternalStoreRuntime({
    messages,
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
        <Thread components={COMPONENTS} followNewMessages />
        {canReply && onTyping ? <TypingReporter sayTyping={onTyping} /> : null}
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
