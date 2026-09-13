import { Badge } from "@/components/ui/badge";

import type { Handoff } from "../api/handoffsApi";
import { handoffTitle, minutesBetween } from "../format";

/**
 * The chat pane's header: who this conversation is with, when it started, and where it stands.
 *
 * The status badge is the one place a viewer sees whether the conversation is still waiting on a
 * person, already claimed, or handed back to Spirit — the same three states `HandoffRow` marks in
 * the list, read here for the one open conversation instead of a row.
 */
export function HandoffChatHeader({ handoff, now }: { handoff: Handoff; now: Date }) {
  return (
    <div className="flex items-start justify-between gap-3 px-4 py-3">
      <div className="min-w-0">
        <h2 className="truncate text-sm font-semibold">{handoffTitle(handoff)}</h2>
        <p className="text-xs text-muted-foreground">
          Started {minutesBetween(handoff.askedAt, now)} min ago
        </p>
      </div>

      <StatusBadge handoff={handoff} now={now} />
    </div>
  );
}

function StatusBadge({ handoff, now }: { handoff: Handoff; now: Date }) {
  if (handoff.status === "waiting") {
    return <Badge>{minutesBetween(handoff.askedAt, now)} min</Badge>;
  }

  if (handoff.status === "human") {
    return <Badge>{handoff.assignee?.name ?? "Someone"} has this chat</Badge>;
  }

  return <Badge>Back with Spirit</Badge>;
}
