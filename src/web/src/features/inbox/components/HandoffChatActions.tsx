import { Button } from "@/components/ui/button";

import type { Handoff } from "../api/handoffsApi";
import { useHandoffActions } from "../hooks/useHandoffActions";

/**
 * The Take/Done buttons in the chat pane header.
 *
 * A waiting handoff offers both, since either move is legal; once it is claimed, only the person
 * who claimed it can finish it, and everyone else sees nothing here — the badge next to these
 * buttons already tells them who has it.
 */
export function HandoffChatActions({
  handoff,
  meKey,
  onChanged,
}: {
  handoff: Handoff;
  meKey: string;
  onChanged(next: Handoff): void;
}) {
  const { take, finish, busy, error } = useHandoffActions();

  async function handleTake() {
    const next = await take(handoff.callId);
    if (next) onChanged(next);
  }

  async function handleFinish() {
    const done = await finish(handoff.callId);
    if (done) onChanged({ ...handoff, status: "done", doneAt: new Date() });
  }

  const showTake = handoff.status === "waiting";
  const showDone =
    handoff.status === "waiting" || (handoff.status === "human" && handoff.assignee?.key === meKey);

  return (
    <div className="flex flex-col items-end gap-1">
      <div className="flex items-center gap-2">
        {showDone ? (
          <Button type="button" variant="outline" size="sm" disabled={busy} onClick={handleFinish}>
            Done
          </Button>
        ) : null}
        {showTake ? (
          <Button type="button" size="sm" disabled={busy} onClick={handleTake}>
            Take
          </Button>
        ) : null}
      </div>
      {error ? <p className="text-xs text-destructive">{error}</p> : null}
    </div>
  );
}
