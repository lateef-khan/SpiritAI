import { Hidden, Thread } from "@/components/assistant-ui/thread";
import { LauncherBubble } from "@/components/assistant-ui/elements/launcher-bubble";
import { Popover, PopoverClose, PopoverContent, PopoverTrigger } from "@/components/ui/popover";
import { TooltipProvider } from "@/components/ui/tooltip";
import { AssistantRuntimeProvider } from "@assistant-ui/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { XIcon } from "lucide-react";
import { useEffect, useState } from "react";
import { readVisitorMemory, visitorFetch } from "./features/widget/api/visitorIdentity";
import { createWidgetApi } from "./features/widget/api/widgetApi";
import { TypingReporter } from "./features/handoff/TypingReporter";
import { HandoffBanner } from "./features/widget/components/HandoffBanner";
import { useHandoffDesk } from "./features/widget/hooks/useHandoffDesk";
import { useWidgetRuntime } from "./features/widget/hooks/useWidgetRuntime";
import { useWidgetSocket } from "./features/widget/hooks/useWidgetSocket";

/**
 * The embeddable form of the chat: a bubble on someone else's page that opens into a panel.
 */

/** The size the frame should be, in CSS pixels, for each state. */
const SIZE = {
  closed: { width: 96, height: 96 },
  open: { width: 400, height: 620 },
} as const;

/*
 * The widget's own route, and not the app's.
 */
const endpoint = document.documentElement.dataset.agentcoreEndpoint || "/v1/public/main/responses";

/*
 * Who this widget is, on every request it makes. Built once: the key is read per request, so
 * nothing here goes stale.
 */
const send = visitorFetch(readVisitorMemory);

const api = createWidgetApi(send);

/**
 * The pages of history the reader scrolls up for, once read.
 */
const queryClient = new QueryClient({
  defaultOptions: { queries: { staleTime: Infinity, retry: 1 } },
});

const WIDGET_COMPONENTS = {
  ToolGroup: Hidden,
  ToolFallback: Hidden,
  Sources: Hidden,
  Timing: Hidden,
} as const;

type Phase = "closed" | "open";

/**
 * Tells the host page how much room to give the frame.
 *
 */
function useFrameSize(phase: Phase) {
  useEffect(() => {
    const size = SIZE[phase];
    // "*" rather than a fixed origin: the widget is embedded on sites it cannot know the names of,
    // and the message carries no secret — only two numbers.
    window.parent?.postMessage({ source: "agentcore-widget", type: "resize", ...size }, "*");
  }, [phase]);
}

export function Widget() {
  const desk = useHandoffDesk(api);
  const widget = useWidgetRuntime(endpoint, api, send, desk);
  const [phase, setPhase] = useState<Phase>("closed");
  const [unread, setUnread] = useState(0);
  const [bounceKey, setBounceKey] = useState(0);
  const isOpen = phase === "open";

  const { typing, sayTyping } = useWidgetSocket({
    desk,
    widget,
    onMessage: () => {
      if (phase === "closed") {
        setUnread((n) => n + 1);
        setBounceKey((k) => k + 1);
      }
    },
  });

  useFrameSize(phase);

  const onOpenChange = (next: boolean) => {
    setPhase(next ? "open" : "closed");
    if (next) setUnread(0);
  };

  return (
    <AssistantRuntimeProvider runtime={widget.runtime}>
      <QueryClientProvider client={queryClient}>
        <TooltipProvider>
          <Popover open={isOpen} onOpenChange={onOpenChange}>
            <div className="flex h-dvh w-full items-end justify-end p-3">
              <PopoverTrigger asChild>
                <div>
                  <LauncherBubble open={isOpen} unread={unread} bounceKey={bounceKey} />
                </div>
              </PopoverTrigger>

              <PopoverContent
                onOpenAutoFocus={(event) => event.preventDefault()}
                className="flex h-[500px] w-[352px] flex-col overflow-hidden p-0"
              >
                <PopoverClose
                  aria-label="Close chat"
                  className="hover:bg-accent absolute end-2 top-2 z-10 rounded-full p-1.5"
                >
                  <XIcon className="size-4" />
                </PopoverClose>
                <HandoffBanner state={desk.state} typing={typing} onLeaveEmail={desk.leaveEmail} />
                <div className="min-h-0 flex-1">
                  <Thread components={WIDGET_COMPONENTS} olderMessages={widget.older} />
                </div>
                <TypingReporter sayTyping={sayTyping} />
              </PopoverContent>
            </div>
          </Popover>
        </TooltipProvider>
      </QueryClientProvider>
    </AssistantRuntimeProvider>
  );
}
