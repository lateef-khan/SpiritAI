import {
  ResizableHandle,
  ResizablePanel,
  ResizablePanelGroup,
  useDefaultLayout,
} from "@/components/ui/resizable";
import { useIsMobile } from "@/hooks/use-mobile";

import { InboxPanel } from "./InboxPanel";

/**
 * The inbox, in place of the chat.
 *
 * On desktop the conversations column sits beside an empty main pane, sized and resized the same
 * way `ChatAndUnit`'s two panels are. On mobile there is no room for a second pane, so the panel
 * alone fills the width and picking a conversation is left for a later task.
 */
export function InboxScreen({ meKey }: { meKey: string }) {
  const isMobile = useIsMobile();
  const { defaultLayout, onLayoutChanged } = useDefaultLayout({ id: "spirit-inbox" });

  if (isMobile) {
    return (
      <div className="min-w-0 flex-1">
        <InboxPanel meKey={meKey} />
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
        <InboxPanel meKey={meKey} />
      </ResizablePanel>
      <ResizableHandle />
      <ResizablePanel id="inbox-main">
        <div className="flex h-full items-center justify-center text-muted-foreground">
          Pick a conversation.
        </div>
      </ResizablePanel>
    </ResizablePanelGroup>
  );
}
