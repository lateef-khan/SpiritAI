import { useEffect, useState } from "react";
import { useQueryClient } from "@tanstack/react-query";

import type { ExportedMessageRepository } from "@assistant-ui/react";
import {
  ResizableHandle,
  ResizablePanel,
  ResizablePanelGroup,
  useDefaultLayout,
} from "@/components/ui/resizable";
import { useIsMobile } from "@/hooks/use-mobile";

import type { Handoff, HandoffFilter } from "../api/handoffsApi";
import { applyClaimed, applyDone } from "../cache/handoffCache";
import { useHandoffs } from "../hooks/useHandoffs";
import { useInboxCounts } from "../hooks/useInboxCounts";
import { useInboxSocket } from "../hooks/useInboxSocket";
import { useMarkSeen } from "../hooks/useMarkSeen";
import { defaultFilter } from "../inboxFilter";
import { HandoffChat } from "./HandoffChat";
import { InboxPanel } from "./InboxPanel";

/**
 * The inbox, in place of the chat.
 *
 * Owns the list — which filter is loaded, its rows, and which row is picked — so a push that
 * edits the rows underneath never loses track of the one on screen. Every pick is mirrored up through
 * `onSelectionChange` for the context rail, which owns no selection of its own. The picked
 * handoff's transcript arrives as `transcript`, the one load the rail reads too. On desktop the
 * conversations column sits beside a main pane, sized and resized the same way `ChatAndUnit`'s
 * two panels are; the main pane shows the picked handoff's chat, or asks for a pick when none is
 * made yet. On mobile there is no room for a second pane, so a pick swaps the panel out for the
 * chat, full width, and `HandoffChat`'s own back button swaps it back.
 */
export function InboxScreen({
  meKey,
  transcript,
  onSelectionChange,
}: {
  meKey: string;
  transcript: {
    history: ExportedMessageRepository | null;
    loading: boolean;
    error: string | null;
    reload: () => void;
  };
  onSelectionChange(handoff: Handoff | null): void;
}) {
  const isMobile = useIsMobile();
  const cache = useQueryClient();
  const { defaultLayout, onLayoutChanged } = useDefaultLayout({ id: "spirit-inbox" });

  const [filter, setFilter] = useState<HandoffFilter>(() => defaultFilter("open"));
  const [selectedId, setSelectedId] = useState<number | null>(null);
  const [pinned, setPinned] = useState<Handoff | null>(null);

  const { rows, loading, error, hasMore, loadMore } = useHandoffs(filter);
  const counts = useInboxCounts(filter.view);

  // The cache hands back a fresh row object whenever a push edits it, so a live row that is
  // `!==` the last one pinned means the row itself changed (say waiting -> human) and `pinned`
  // is stale. Refreshing it here, during render, keeps `pinned` "the row as last seen" without
  // an effect. A row a push took out of the list — claimed by somebody else, or closed — stays
  // pinned, so the chat pane keeps showing it as last seen rather than going blank underfoot.
  const live = rows.find((row) => row.id === selectedId) ?? null;
  if (live && !loading && live !== pinned) setPinned(live);
  const selected = loading ? (pinned ?? live) : (live ?? pinned);

  // The context rail lives above this screen, so the pick is mirrored up for it — including a
  // reload that refreshes the picked row under a new object.
  useEffect(() => {
    onSelectionChange(selected);
  }, [selected, onSelectionChange]);

  const { typing, sayTyping } = useInboxSocket({ selectedCallId: selected?.callId ?? null });
  useMarkSeen(selected);

  function handleSelect(row: Handoff) {
    setSelectedId(row.id);
    setPinned(row);
  }

  // The host's answer is the row as it now stands, so the cache learns it here and now rather
  // than from the push that follows; hearing the same change twice, the cache does nothing.
  function handleChanged(next: Handoff) {
    if (next.status === "human" && next.assignee) {
      applyClaimed(cache, { callId: next.callId, assignee: next.assignee }, meKey);
    } else if (next.status === "done") {
      applyDone(cache, { callId: next.callId }, meKey);
    }
    setPinned(next);
  }

  function clearSelection() {
    setSelectedId(null);
    setPinned(null);
  }

  // A new view is a new list; the pick belonged to the old one. A new tab or order in the same
  // view keeps it: the row is still the same row, wherever it lands.
  function handleFilterChange(next: HandoffFilter) {
    if (next.view !== filter.view) clearSelection();
    setFilter(next);
  }

  const panel = (
    <InboxPanel
      filter={filter}
      onFilterChange={handleFilterChange}
      rows={rows}
      counts={counts}
      loading={loading}
      error={error}
      hasMore={hasMore}
      loadMore={loadMore}
      selectedId={selected?.id ?? null}
      onSelect={handleSelect}
    />
  );

  if (isMobile) {
    return (
      <div className="min-w-0 flex-1">
        {selected ? (
          <HandoffChat
            key={selected.id}
            handoff={selected}
            history={transcript.history}
            loading={transcript.loading}
            error={transcript.error}
            reload={transcript.reload}
            meKey={meKey}
            typing={typing}
            onTyping={sayTyping}
            onChanged={handleChanged}
            onBack={clearSelection}
          />
        ) : (
          panel
        )}
      </div>
    );
  }

  return (
    <ResizablePanelGroup
      orientation="horizontal"
      className="h-full min-w-0 flex-1"
      defaultLayout={defaultLayout}
      onLayoutChanged={onLayoutChanged}
    >
      <ResizablePanel id="inbox" defaultSize="22rem" minSize="18rem" maxSize="32rem">
        {panel}
      </ResizablePanel>
      <ResizableHandle />
      <ResizablePanel id="inbox-main">
        {selected ? (
          // `HandoffChat` reads the clock once per mount, so a new pick must be a new mount.
          <HandoffChat
            key={selected.id}
            handoff={selected}
            history={transcript.history}
            loading={transcript.loading}
            error={transcript.error}
            reload={transcript.reload}
            meKey={meKey}
            typing={typing}
            onTyping={sayTyping}
            onChanged={handleChanged}
          />
        ) : (
          <div className="flex h-full items-center justify-center text-muted-foreground">
            Pick a conversation.
          </div>
        )}
      </ResizablePanel>
    </ResizablePanelGroup>
  );
}
