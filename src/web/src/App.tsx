import { Thread } from "@/components/assistant-ui/thread";
import { SidebarProvider } from "@/components/ui/sidebar";
import { TooltipProvider } from "@/components/ui/tooltip";
import { AssistantRuntimeProvider, useRemoteThreadListRuntime } from "@assistant-ui/react";
import { AgentCoreSidebar } from "@/components/chat/AgentCoreSidebar";
import { AuthGate } from "@/auth/AuthGate";
import { GenerativeUiDataUI } from "@/components/chat/GenerativeUiDataUI";
import { useAgentCoreRuntime } from "./runtime/AgentCoreRuntime";
import {
  createAgentCoreThreadListAdapter,
  useThreadSession,
} from "./runtime/AgentCoreThreadListAdapter";
import { authFetch } from "@/auth/authFetch";
import { ThreadUnitPanel } from "@/components/unit/ThreadUnitPanel";
import { Sheet, SheetContent, SheetTitle, SheetTrigger } from "@/components/ui/sheet";
import { useIsMobile } from "@/hooks/use-mobile";
import { PanelRightIcon } from "lucide-react";

/**
 * The route the text endpoint answers on.
 *
 * It is read from the page rather than compiled in, because the host can move the endpoint —
 * `MapAgentCoreHost` takes a pattern — and a rebuilt bundle should not be the price of that. The
 * default is the one `MapChatCompletions` uses when a host names none.
 */
const endpoint = document.documentElement.dataset.agentcoreEndpoint || "/v1/chat/completions";

/**
 * The thread list, on the host. Built once: swapping the adapter does not reload the list, so a
 * new one per render would be a new backing store that nothing ever reads.
 */
const threads = createAgentCoreThreadListAdapter();

/**
 * One thread's turn loop, bound to that thread's call.
 *
 * `useRemoteThreadListRuntime` calls this once per thread, so `useThreadSession` resolves to the
 * open thread rather than to the tab.
 */
function useThreadRuntime() {
  return useAgentCoreRuntime(endpoint, authFetch, useThreadSession());
}

/**
 * Where the unit panel sits.
 *
 * Beside the conversation on a wide screen. On a narrow one there is no room for two columns, becomes a `Sheet`.
 */
function UnitColumn() {
  const isMobile = useIsMobile();

  if (!isMobile) {
    return <ThreadUnitPanel className="w-80 shrink-0" />;
  }

  return (
    <Sheet>
      <SheetTrigger className="absolute end-3 top-3 z-10 rounded-md border bg-background p-1.5">
        <PanelRightIcon className="size-4" />
        <span className="sr-only">Show the unit</span>
      </SheetTrigger>
      <SheetContent side="right" className="w-80 p-0">
        <SheetTitle className="sr-only">Unit</SheetTitle>
        <ThreadUnitPanel className="border-l-0" />
      </SheetContent>
    </Sheet>
  );
}

export function App() {
  const runtime = useRemoteThreadListRuntime({
    runtimeHook: useThreadRuntime,
    adapter: threads,
  });

  return (
    <AuthGate>
      <AssistantRuntimeProvider runtime={runtime}>
        <GenerativeUiDataUI />
        <TooltipProvider>
          <SidebarProvider>
            <div className="flex h-dvh w-full">
              <AgentCoreSidebar />
              <div className="relative flex-1 overflow-hidden">
                <Thread />
              </div>
              <UnitColumn />
            </div>
          </SidebarProvider>
        </TooltipProvider>
      </AssistantRuntimeProvider>
    </AuthGate>
  );
}
