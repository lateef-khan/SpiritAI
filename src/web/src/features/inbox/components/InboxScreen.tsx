import { useState } from "react";

import {
  ResizableHandle,
  ResizablePanel,
  ResizablePanelGroup,
  useDefaultLayout,
} from "@/components/ui/resizable";
import { useIsMobile } from "@/hooks/use-mobile";

import type { Handoff } from "../api/handoffsApi";
import { useHandoffs, type InboxView } from "../hooks/useHandoffs";
import { HandoffChat } from "./HandoffChat";
import { InboxPanel } from "./InboxPanel";

/**
 * The inbox, in place of the chat.
 *
 * Owns the list — which view is loaded, its rows, and which row is picked — so a later reload can
 * refresh the rows without losing track of the one on screen. On desktop the conversations column
 * sits beside a main pane, sized and resized the same way `ChatAndUnit`'s two panels are; the main
 * pane shows the picked handoff's chat, or asks for a pick when none is made yet. On mobile there
 * is no room for a second pane, so a pick swaps the panel out for the chat, full width, and
 * `HandoffChat`'s own back button swaps it back.
 */
export function InboxScreen({ meKey }: { meKey: string }) {
  const isMobile = useIsMobile();
  const { defaultLayout, onLayoutChanged } = useDefaultLayout({ id: "spirit-inbox" });

  const [view, setView] = useState<InboxView>("open");
  const [selectedId, setSelectedId] = useState<number | null>(null);
  const [pinned, setPinned] = useState<Handoff | null>(null);

  const { rows, counts, loading, error, reload } = useHandoffs(view, meKey);

  // `useHandoffs` hands back a fresh row object on every load, so a live row that is `!==` the
  // last one pinned means the row itself changed (say waiting -> human) and `pinned` is stale.
  // Refreshing it here, during render, keeps `pinned` "the row as last seen" without an effect.
  // While a load is in flight, `rows` is empty and `live` is null anyway, but a reload that lands
  // between renders could otherwise carry a row for the *previous* selection through `selectedId`
  // for one tick; skipping the refresh during loading, and preferring `pinned` for `selected`,
  // makes "the pinned row is the truth while the list is loading" the actual rule instead of an
  // accident of `rows` being empty.
  const live = rows.find((row) => row.id === selectedId) ?? null;
  if (live && !loading && live !== pinned) setPinned(live);
  const selected = loading ? (pinned ?? live) : (live ?? pinned);

  function handleSelect(row: Handoff) {
    setSelectedId(row.id);
    setPinned(row);
  }

  function handleChanged(next: Handoff) {
    setPinned(next);
    reload();
  }

  function clearSelection() {
    setSelectedId(null);
    setPinned(null);
  }

  function handleViewChange(next: InboxView) {
    setView(next);
    clearSelection();
  }

  const panel = (
    <InboxPanel
      meKey={meKey}
      view={view}
      onViewChange={handleViewChange}
      rows={rows}
      counts={counts}
      loading={loading}
      error={error}
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
            meKey={meKey}
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
      className="min-w-0 flex-1"
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
            meKey={meKey}
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
