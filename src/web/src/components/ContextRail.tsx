import type { ExportedMessageRepository } from "@assistant-ui/react";

import type { Handoff } from "@/features/inbox/api/handoffsApi";
import { HandoffContextPanel } from "@/features/inbox/components/HandoffContextPanel";
import { ThreadContextPanel } from "@/features/unit/ThreadContextPanel";

/**
 * The right rail, above both views: the chrome never moves, only the inside changes. An
 * agent thread shows its context panel off the live conversation; a picked handoff shows the
 * visitor, the unit its transcript names, and the handoff's own facts; with no pick yet the
 * rail says so instead of rendering a unit nobody asked about.
 *
 * The handoff branch keys on the call, so a pick carries no chip or fold state into the next
 * conversation. History arrives as a prop from the one `useHandoffMessages` load the chat
 * reads too — the rail never fetches on its own.
 */
export function ContextRail({
  mode,
  handoff,
  history,
}: {
  mode: "thread" | "handoff";
  handoff: Handoff | null;
  history: ExportedMessageRepository | null;
}) {
  if (mode === "thread") {
    return <ThreadContextPanel className="border-l-0" />;
  }

  if (!handoff) {
    return (
      <div className="flex h-full items-center justify-center bg-card p-6 text-center text-sm text-muted-foreground">
        Pick a conversation.
      </div>
    );
  }

  return (
    <HandoffContextPanel
      key={handoff.callId}
      handoff={handoff}
      history={history}
      className="border-l-0"
    />
  );
}
