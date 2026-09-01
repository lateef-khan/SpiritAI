import { Thread } from "@/components/assistant-ui/thread";
import { SidebarProvider } from "@/components/ui/sidebar";
import { TooltipProvider } from "@/components/ui/tooltip";
import { AssistantRuntimeProvider, useRemoteThreadListRuntime } from "@assistant-ui/react";
import { AgentCoreSidebar } from "@/components/AgentCoreSidebar";
import { AuthGate } from "@/auth/AuthGate";
import { GenerativeUiDataUI } from "@/components/GenerativeUiDataUI";
import { useAgentCoreRuntime } from "./runtime/AgentCoreRuntime";
import {
  createAgentCoreThreadListAdapter,
  useThreadSession,
} from "./runtime/AgentCoreThreadListAdapter";
import { authFetch } from "@/auth/authFetch";

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
              <div className="flex-1 overflow-hidden">
                <Thread />
              </div>
            </div>
          </SidebarProvider>
        </TooltipProvider>
      </AssistantRuntimeProvider>
    </AuthGate>
  );
}
