import type * as React from "react";
import { InboxIcon } from "lucide-react";
import {
  Sidebar,
  SidebarContent,
  SidebarFooter,
  SidebarMenu,
  SidebarMenuButton,
  SidebarMenuItem,
  SidebarRail,
} from "@/components/ui/sidebar";
import { ThreadList } from "@/components/assistant-ui/thread-list";
import { AccountMenu } from "@/features/auth/AccountMenu";

/**
 * `thread-list.tsx`'s own `data-slot` names for the two clicks that actually navigate: picking a
 * thread, and starting a new one. Every other control in the list — the search input, a thread's
 * "More" menu and its Rename/Archive/Delete items — sits inside the same list without matching
 * either.
 */
const NAVIGATING_THREAD_LIST_SLOTS =
  '[data-slot="aui_thread-list-item-trigger"], [data-slot="aui_thread-list-new"]';

export function AgentCoreSidebar({
  inboxOpen,
  onOpenInbox,
  onOpenChat,
  ...props
}: React.ComponentProps<typeof Sidebar> & {
  inboxOpen: boolean;
  onOpenInbox: () => void;
  onOpenChat: () => void;
}) {
  return (
    <Sidebar {...props}>
      <SidebarContent className="px-2 py-2">
        <SidebarMenu>
          <SidebarMenuItem>
            <SidebarMenuButton isActive={inboxOpen} onClick={onOpenInbox}>
              <InboxIcon />
              Inbox
            </SidebarMenuButton>
          </SidebarMenuItem>
        </SidebarMenu>
        {/* Picking a thread, or starting a new one, is `ThreadList`'s own click handling several
            layers down; catching the click on the way up is simpler than threading a callback
            through every one of those layers. The search box and a thread's "More" menu live in
            the same list and must not send the view back to chat. */}
        <div
          onClickCapture={(event) => {
            const target = event.target as Element;
            if (target.closest(NAVIGATING_THREAD_LIST_SLOTS)) onOpenChat();
          }}
        >
          <ThreadList />
        </div>
      </SidebarContent>
      <SidebarRail />
      <SidebarFooter className="border-t">
        <AccountMenu />
      </SidebarFooter>
    </Sidebar>
  );
}
