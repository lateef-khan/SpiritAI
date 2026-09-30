import type * as React from "react";
import { Sidebar, SidebarContent, SidebarRail } from "@/components/ui/sidebar";
import { ThreadList } from "@/components/assistant-ui/thread-list";

export function AgentCoreSidebar(props: React.ComponentProps<typeof Sidebar>) {
  return (
    <Sidebar {...props}>
      <SidebarContent className="px-2 py-2">
        <ThreadList />
      </SidebarContent>
      <SidebarRail />
    </Sidebar>
  );
}
