import { useState } from "react";

import {
  ResizableHandle,
  ResizablePanel,
  ResizablePanelGroup,
  useDefaultLayout,
} from "@/components/ui/resizable";
import { useIsMobile } from "@/hooks/use-mobile";

import type { Handoff } from "../api/handoffsApi";
import { HandoffChat } from "./HandoffChat";
import { InboxPanel } from "./InboxPanel";

/**
 * The inbox, in place of the chat.
 *
 * On desktop the conversations column sits beside a main pane, sized and resized the same way
 * `ChatAndUnit`'s two panels are; the main pane shows the picked handoff's chat, or asks for a
 * pick when none is made yet. On mobile there is no room for a second pane, so a pick swaps the
 * panel out for the chat, full width, and `HandoffChat`'s own back button swaps it back.
 */
export function InboxScreen({ meKey }: { meKey: string }) {
  const isMobile = useIsMobile();
  const { defaultLayout, onLayoutChanged } = useDefaultLayout({ id: "spirit-inbox" });
  const [selected, setSelected] = useState<Handoff | null>(null);

  if (isMobile) {
    return (
      <div className="min-w-0 flex-1">
        {selected ? (
          <HandoffChat key={selected.id} handoff={selected} onBack={() => setSelected(null)} />
        ) : (
          <InboxPanel meKey={meKey} selectedId={null} onSelect={setSelected} />
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
        <InboxPanel meKey={meKey} selectedId={selected?.id ?? null} onSelect={setSelected} />
      </ResizablePanel>
      <ResizableHandle />
      <ResizablePanel id="inbox-main">
        {selected ? (
          // `HandoffChat` reads the clock once per mount, so a new pick must be a new mount.
          <HandoffChat key={selected.id} handoff={selected} />
        ) : (
          <div className="flex h-full items-center justify-center text-muted-foreground">
            Pick a conversation.
          </div>
        )}
      </ResizablePanel>
    </ResizablePanelGroup>
  );
}
