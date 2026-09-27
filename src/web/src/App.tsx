import { Thread } from "@/components/assistant-ui/thread";
import { SidebarProvider } from "@/components/ui/sidebar";
import { TooltipProvider } from "@/components/ui/tooltip";
import { AssistantRuntimeProvider, useRemoteThreadListRuntime } from "@assistant-ui/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { AgentCoreSidebar } from "@/features/chat/AgentCoreSidebar";
import { AuthGate } from "@/features/auth/AuthGate";
import { useAgentCoreRuntime } from "./features/threads/AgentCoreRuntime";
import {
  createAgentCoreThreadListAdapter,
  useThreadSession,
} from "./features/threads/AgentCoreThreadListAdapter";
import { useThreadListOlderMessages } from "./features/threads/useThreadListOlderMessages";
import { authFetch } from "@/features/auth/authFetch";
import { ThreadContextPanel } from "@/features/unit/ThreadContextPanel";
import {
  ResizableHandle,
  ResizablePanel,
  ResizablePanelGroup,
  useDefaultLayout,
} from "@/components/ui/resizable";
import { Sheet, SheetContent, SheetTitle, SheetTrigger } from "@/components/ui/sheet";
import { TooltipIconButton } from "@/components/assistant-ui/tooltip-icon-button";
import { useIsMobile } from "@/hooks/use-mobile";
import { PanelRightIcon, PrinterIcon } from "lucide-react";

/**
 * The route the text endpoint answers on.
 *
 * It is read from the page rather than compiled in, because the host can move the endpoint —
 * `MapAgentCoreHost` takes a pattern — and a rebuilt bundle should not be the price of that. The
 * route names no entry: the server picks the agent from the caller's access group.
 */
const endpoint = document.documentElement.dataset.agentcoreEndpoint || "/v1/chat/responses";

/**
 * The thread list, on the host. Built once: swapping the adapter does not reload the list, so a
 * new one per render would be a new backing store that nothing ever reads.
 */
const threads = createAgentCoreThreadListAdapter();

/**
 * Every answer the host has given, by name. Built once: the cache is the app's, not a render's.
 *
 * Nothing goes stale on a clock: an older page of a thread's history never changes once read.
 */
const queryClient = new QueryClient({
  defaultOptions: { queries: { staleTime: Infinity, retry: 1 } },
});

/**
 * One thread's turn loop, bound to that thread's call.
 *
 * `useRemoteThreadListRuntime` calls this once per thread, so `useThreadSession` resolves to the
 * open thread rather than to the tab.
 */
function useThreadRuntime() {
  return useAgentCoreRuntime(endpoint, authFetch, useThreadSession());
}

/** The open thread, paging back through its history as the reader scrolls up. */
function ChatThread() {
  return (
    <div className="relative h-full">
      <TooltipIconButton
        tooltip="Print conversation"
        onClick={() => window.print()}
        className="absolute end-14 top-3 z-10 size-8 rounded-md border bg-background hover:bg-accent"
      >
        <PrinterIcon className="size-4" />
      </TooltipIconButton>
      <Thread olderMessages={useThreadListOlderMessages()} />
    </div>
  );
}

/**
 * The same two, on a narrow screen.
 *
 * There is no room for two columns, so the unit panel becomes a `Sheet` and there is no divider
 * to drag.
 */
function ChatAndUnitSheet() {
  return (
    <>
      <div className="relative min-w-0 flex-1 overflow-hidden">
        <ChatThread />
      </div>
      <Sheet>
        <SheetTrigger className="absolute end-3 top-3 z-10 rounded-md border bg-background p-1.5">
          <PanelRightIcon className="size-4" />
          <span className="sr-only">Show the unit</span>
        </SheetTrigger>
        <SheetContent side="right" className="w-80 p-0">
          <SheetTitle className="sr-only">Unit</SheetTitle>
          <ThreadContextPanel className="border-l-0" />
        </SheetContent>
      </Sheet>
    </>
  );
}

/** The app: its providers, and the shell under them. */
export function App() {
  const runtime = useRemoteThreadListRuntime({
    runtimeHook: useThreadRuntime,
    adapter: threads,
  });

  return (
    <AuthGate>
      <QueryClientProvider client={queryClient}>
        <AssistantRuntimeProvider runtime={runtime}>
          <TooltipProvider>
            <SidebarProvider>
              <Shell />
            </SidebarProvider>
          </TooltipProvider>
        </AssistantRuntimeProvider>
      </QueryClientProvider>
    </AuthGate>
  );
}

/** The sidebar, the chat, and the unit rail beside it. */
function Shell() {
  const isMobile = useIsMobile();
  const { defaultLayout, onLayoutChanged } = useDefaultLayout({ id: "spirit-shell" });

  return (
    <div className="flex h-dvh w-full">
      <AgentCoreSidebar />
      {isMobile ? (
        <ChatAndUnitSheet />
      ) : (
        <ResizablePanelGroup
          orientation="horizontal"
          className="min-w-0 flex-1"
          defaultLayout={defaultLayout}
          onLayoutChanged={onLayoutChanged}
        >
          <ResizablePanel id="main" minSize="24rem">
            <ChatThread />
          </ResizablePanel>
          <ResizableHandle />
          <ResizablePanel
            id="context"
            defaultSize="20rem"
            minSize="16rem"
            maxSize="40rem"
            groupResizeBehavior="preserve-pixel-size"
          >
            <ThreadContextPanel className="border-l-0" />
          </ResizablePanel>
        </ResizablePanelGroup>
      )}
    </div>
  );
}
