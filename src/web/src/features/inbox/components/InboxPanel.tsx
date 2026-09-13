import { useState } from "react";

import { Skeleton } from "@/components/ui/skeleton";

import type { Handoff } from "../api/handoffsApi";
import { filterHandoffs, useHandoffs, type InboxTab, type InboxView } from "../hooks/useHandoffs";
import { HandoffRow } from "./HandoffRow";
import { InboxHeader } from "./InboxHeader";
import { InboxTabs } from "./InboxTabs";

/**
 * The conversations column: every handoff waiting on, or already claimed by, a person.
 *
 * Width and placement are the parent's call — this only ever fills the height it is given, the
 * same contract `UnitPanel` uses for the column on the chat's other side. Which row reads as
 * picked, and what a click on one does, both belong to the parent: this only ever draws the state
 * it is handed and reports a click back.
 */
export function InboxPanel({
  meKey,
  selectedId,
  onSelect,
}: {
  meKey: string;
  selectedId: number | null;
  onSelect: (row: Handoff | null) => void;
}) {
  const [view, setView] = useState<InboxView>("open");
  const [tab, setTab] = useState<InboxTab>("all");
  const [reversed, setReversed] = useState(false);

  const { rows, counts, loading, error } = useHandoffs(view, meKey);

  // The done view has no unassigned rows, so a tab choice made in the open view falls back to all
  // rather than showing nothing.
  const effectiveTab: InboxTab = view === "done" && tab === "unassigned" ? "all" : tab;
  const filtered = filterHandoffs(rows, effectiveTab, meKey);

  // `useHandoffs` loads `open` oldest-first and `done` newest-first, so the same `reversed` flag
  // reads as the opposite `oldestFirst` sense on each view.
  const oldestFirst = view === "open" ? !reversed : reversed;
  const ordered = reversed ? [...filtered].reverse() : filtered;

  function handleViewChange(next: InboxView) {
    setView(next);
    setReversed(false);
    onSelect(null);
  }

  return (
    <div className="flex h-full flex-col border-r">
      <InboxHeader
        view={view}
        onViewChange={handleViewChange}
        oldestFirst={oldestFirst}
        onToggleOrder={() => setReversed((current) => !current)}
      />

      <InboxTabs view={view} tab={effectiveTab} onTabChange={setTab} counts={counts} />

      <div className="min-h-0 flex-1 overflow-y-auto">
        {loading ? (
          <RowsSkeleton />
        ) : error ? (
          <p className="p-3.5 text-sm text-destructive">{error.message}</p>
        ) : ordered.length === 0 ? (
          <p className="p-3.5 text-sm text-muted-foreground">No conversations.</p>
        ) : (
          <ul>
            {ordered.map((row) => (
              <li key={row.id}>
                <HandoffRow
                  handoff={row}
                  selected={row.id === selectedId}
                  onSelect={() => onSelect(row)}
                />
              </li>
            ))}
          </ul>
        )}
      </div>
    </div>
  );
}

function RowsSkeleton() {
  return (
    <div className="space-y-3 p-3.5">
      {[0, 1, 2].map((row) => (
        <Skeleton key={row} className="h-16 w-full" />
      ))}
    </div>
  );
}
