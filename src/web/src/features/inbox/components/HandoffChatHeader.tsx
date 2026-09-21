import { TypingDots } from "@/components/assistant-ui/elements/typing-indicator";
import { Badge } from "@/components/ui/badge";

import type { Handoff } from "../api/handoffsApi";
import { handoffTitle, minutesBetween } from "../format";
import { HandoffChatActions } from "./HandoffChatActions";

/**
 * The chat pane's header: who this conversation is with, when it started, where it stands, and
 * the Take/Done buttons for moving it along.
 *
 * The status badge is the one place a viewer sees whether the conversation is still waiting on a
 * person, already claimed, or handed back to Spirit — the same three states `HandoffRow` marks in
 * the list, read here for the one open conversation instead of a row.
 */
export function HandoffChatHeader({
  handoff,
  now,
  meKey,
  typing = false,
  onChanged,
}: {
  handoff: Handoff;
  now: Date;
  meKey: string;
  /** Whether the visitor is typing right now. Takes the place of the started-ago line while so. */
  typing?: boolean;
  onChanged(next: Handoff): void;
}) {
  return (
    <div className="flex items-start justify-between gap-3 px-4 py-3">
      <div className="min-w-0">
        <h2 className="truncate text-sm font-semibold">{handoffTitle(handoff)}</h2>
        <p className="text-xs text-muted-foreground" aria-live="polite">
          {typing ? (
            <>
              Visitor is typing <TypingDots />
            </>
          ) : (
            `Started ${minutesBetween(handoff.askedAt, now)} min ago`
          )}
        </p>
      </div>

      <div className="flex items-center gap-2">
        <StatusBadge handoff={handoff} now={now} meKey={meKey} />
        <HandoffChatActions handoff={handoff} meKey={meKey} onChanged={onChanged} />
      </div>
    </div>
  );
}

function StatusBadge({ handoff, now, meKey }: { handoff: Handoff; now: Date; meKey: string }) {
  if (handoff.status === "waiting") {
    return <Badge>{minutesBetween(handoff.askedAt, now)} min</Badge>;
  }

  if (handoff.status === "human") {
    return (
      <Badge>
        {handoff.assignee?.key === meKey
          ? "You have this chat"
          : `${handoff.assignee?.name ?? "Someone"} has this chat`}
      </Badge>
    );
  }

  return <Badge>Back with Spirit</Badge>;
}
