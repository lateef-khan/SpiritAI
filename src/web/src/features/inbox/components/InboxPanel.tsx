import { Skeleton } from "@/components/ui/skeleton";

import type { Handoff, HandoffFilter } from "../api/handoffsApi";
import type { InboxCounts } from "../hooks/useHandoffs";
import { reversed, tabOf, withTab, withView } from "../inboxFilter";
import { HandoffList } from "./HandoffList";
import { InboxHeader } from "./InboxHeader";
import { InboxTabs } from "./InboxTabs";

/**
 * The conversations column: every handoff waiting on, or already claimed by, a person.
 *
 * Width and placement are the parent's call — this only ever fills the height it is given, the
 * same contract `UnitPanel` uses for the column on the chat's other side. The rows, the filter
 * they were listed by, and which one reads as picked are all the parent's state; this only ever
 * draws what it is handed, reports a click back, and asks for a different filter. The
 * Open/Done switch, the Mine/Unassigned/All tabs, and the order toggle are three edits of that
 * one filter, spelled out in `inboxFilter.ts`.
 */
export function InboxPanel({
  filter,
  onFilterChange,
  rows,
  counts,
  loading,
  error,
  hasMore,
  loadMore,
  selectedId,
  onSelect,
}: {
  filter: HandoffFilter;
  onFilterChange: (filter: HandoffFilter) => void;
  rows: Handoff[];
  counts: InboxCounts;
  loading: boolean;
  error: Error | null;
  hasMore: boolean;
  loadMore: () => void;
  selectedId: number | null;
  onSelect: (row: Handoff) => void;
}) {
  return (
    <div className="flex h-full flex-col border-r">
      <InboxHeader
        view={filter.view}
        onViewChange={(view) => onFilterChange(withView(filter, view))}
        oldestFirst={filter.order === "oldest"}
        onToggleOrder={() => onFilterChange(reversed(filter))}
      />

      <InboxTabs
        view={filter.view}
        tab={tabOf(filter)}
        onTabChange={(tab) => onFilterChange(withTab(filter, tab))}
        counts={counts}
      />

      {loading ? (
        <RowsSkeleton />
      ) : error ? (
        <p className="p-3.5 text-sm text-destructive">{error.message}</p>
      ) : rows.length === 0 ? (
        <p className="p-3.5 text-sm text-muted-foreground">No conversations.</p>
      ) : (
        <HandoffList
          rows={rows}
          hasMore={hasMore}
          loadMore={loadMore}
          selectedId={selectedId}
          onSelect={onSelect}
        />
      )}
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
