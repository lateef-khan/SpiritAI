import {
  AssistantRuntimeProvider,
  fromThreadMessageLike,
  useExternalStoreRuntime,
  type ExportedMessageRepository,
} from "@assistant-ui/react";
import { ChevronLeftIcon } from "lucide-react";
import { useMemo, useState } from "react";

import { Hidden, Thread } from "@/components/assistant-ui/thread";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";

import type { Handoff } from "../api/handoffsApi";
import { useHandoffMessages } from "../hooks/useHandoffMessages";
import { HandoffChatHeader } from "./HandoffChatHeader";

type HistoryMessage = ExportedMessageRepository["messages"][number]["message"];

/**
 * One handoff's transcript, read-only.
 *
 * Staff read a conversation here before, during, and after they take it; nothing on this pane
 * ever sends a message, which is why the runtime it builds refuses to run a new one rather than
 * silently accepting it.
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
  const { history, loading, error } = useHandoffMessages(handoff.callId);
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
          <HandoffThread history={history} />
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

function HandoffThread({ history }: { history: ReturnType<typeof useHandoffMessages>["history"] }) {
  const messages = useMemo(() => history?.messages.map((item) => item.message) ?? [], [history]);

  const runtime = useExternalStoreRuntime({
    messages,
    isRunning: false,
    isDisabled: true,
    onNew: () => {
      throw new Error("read only");
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
      <Thread components={{ Welcome: Hidden, Composer: Hidden }} />
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
