import { useCallback, useState } from "react";

import { Thread } from "@/components/assistant-ui/thread";
import { ContextRail } from "@/components/ContextRail";
import { SidebarProvider } from "@/components/ui/sidebar";
import { TooltipProvider } from "@/components/ui/tooltip";
import { AssistantRuntimeProvider, useRemoteThreadListRuntime } from "@assistant-ui/react";
import { AgentCoreSidebar } from "@/features/chat/AgentCoreSidebar";
import { AuthGate } from "@/features/auth/AuthGate";
import { currentToken } from "@/features/auth";
import * as Events from "@/features/handoff/events";
import { SocketProvider } from "@/lib/realtime/SocketProvider";
import { useAgentCoreRuntime } from "./features/threads/AgentCoreRuntime";
import {
  createAgentCoreThreadListAdapter,
  useThreadSession,
} from "./features/threads/AgentCoreThreadListAdapter";
import { authFetch } from "@/features/auth/authFetch";
import { useSession } from "@/features/auth/authClient";
import { callerKeyOf, type Handoff } from "@/features/inbox/api/handoffsApi";
import { InboxScreen } from "@/features/inbox/components/InboxScreen";
import { useHandoffMessages } from "@/features/inbox/hooks/useHandoffMessages";
import { ThreadContextPanel } from "@/features/unit/ThreadContextPanel";
import {
  ResizableHandle,
  ResizablePanel,
  ResizablePanelGroup,
  useDefaultLayout,
} from "@/components/ui/resizable";
import { Sheet, SheetContent, SheetTitle, SheetTrigger } from "@/components/ui/sheet";
import { useIsMobile } from "@/hooks/use-mobile";
import { PanelRightIcon } from "lucide-react";

/** Which of the two screens the main area shows. */
type View = "chat" | "inbox";

/**
 * The route the text endpoint answers on.
 *
 * It is read from the page rather than compiled in, because the host can move the endpoint —
 * `MapAgentCoreHost` takes a pattern — and a rebuilt bundle should not be the price of that. The
 * default is the one `MapResponses` uses when a host names none, with the host's one entry filled in.
 */
const endpoint = document.documentElement.dataset.agentcoreEndpoint || "/v1/main/responses";

/**
 * The thread list, on the host. Built once: swapping the adapter does not reload the list, so a
 * new one per render would be a new backing store that nothing ever reads.
 */
const threads = createAgentCoreThreadListAdapter();

/** Who the app's socket speaks as. The hub counts staff online by it, whichever screen is up. */
const staff = { kind: "staff", token: currentToken } as const;

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
 * The same two, on a narrow screen.
 *
 * There is no room for two columns, so the unit panel becomes a `Sheet` and there is no divider
 * to drag.
 */
function ChatAndUnitSheet() {
  return (
    <>
      <div className="relative min-w-0 flex-1 overflow-hidden">
        <Thread />
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

/**
 * The app: the sidebar, one main pane, and the context rail.
 *
 * The rail is the one unchanging column whatever the main pane shows — an agent thread reads
 * the unit off the live conversation, a picked handoff reads the visitor and the unit off the
 * shared transcript load, and with no pick yet the rail says so. The inbox mirrors its pick up
 * here for the rail; the transcript loads once here for both the chat and the rail.
 */
export function App() {
  const runtime = useRemoteThreadListRuntime({
    runtimeHook: useThreadRuntime,
    adapter: threads,
  });
  const isMobile = useIsMobile();
  const [view, setView] = useState<View>("chat");
  const { defaultLayout, onLayoutChanged } = useDefaultLayout({ id: "spirit-shell" });

  // `App` is `AuthGate`'s parent, so this runs before the session resolves and `meKey` is
  // `"user:"` on that render; `meKey` is only consumed once `AuthGate` lets its children through,
  // by which point the session has resolved to the signed-in user.
  const { data } = useSession();
  const meKey = callerKeyOf(data?.user.id ?? "");

  const [selectedHandoff, setSelectedHandoff] = useState<Handoff | null>(null);
  const handleInboxSelection = useCallback((handoff: Handoff | null) => {
    setSelectedHandoff(handoff);
  }, []);
  // Gated on the inbox view: nothing selected — or nothing shown — loads nothing.
  const transcript = useHandoffMessages(
    view === "inbox" ? (selectedHandoff?.callId ?? null) : null,
  );

  return (
    <AuthGate>
      <SocketProvider auth={staff} events={Events.StaffEvents}>
        <AssistantRuntimeProvider runtime={runtime}>
          <TooltipProvider>
            <SidebarProvider>
              <div className="flex h-dvh w-full">
                <AgentCoreSidebar
                  inboxOpen={view === "inbox"}
                  onOpenInbox={() => setView("inbox")}
                  onOpenChat={() => setView("chat")}
                />
                {isMobile ? (
                  view === "inbox" ? (
                    <InboxScreen
                      meKey={meKey}
                      transcript={transcript}
                      onSelectionChange={handleInboxSelection}
                    />
                  ) : (
                    <ChatAndUnitSheet />
                  )
                ) : (
                  <ResizablePanelGroup
                    orientation="horizontal"
                    className="min-w-0 flex-1"
                    defaultLayout={defaultLayout}
                    onLayoutChanged={onLayoutChanged}
                  >
                    <ResizablePanel id="main" minSize="24rem">
                      {view === "inbox" ? (
                        <InboxScreen
                          meKey={meKey}
                          transcript={transcript}
                          onSelectionChange={handleInboxSelection}
                        />
                      ) : (
                        <Thread />
                      )}
                    </ResizablePanel>
                    <ResizableHandle />
                    <ResizablePanel
                      id="context"
                      defaultSize="20rem"
                      minSize="16rem"
                      maxSize="40rem"
                      groupResizeBehavior="preserve-pixel-size"
                    >
                      <ContextRail
                        mode={view === "inbox" ? "handoff" : "thread"}
                        handoff={selectedHandoff}
                        history={transcript.history}
                      />
                    </ResizablePanel>
                  </ResizablePanelGroup>
                )}
              </div>
            </SidebarProvider>
          </TooltipProvider>
        </AssistantRuntimeProvider>
      </SocketProvider>
    </AuthGate>
  );
}
