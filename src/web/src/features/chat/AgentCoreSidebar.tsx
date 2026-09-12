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
            through every one of those layers. */}
        <div onClickCapture={onOpenChat}>
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
