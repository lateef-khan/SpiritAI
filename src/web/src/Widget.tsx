import { Hidden, Thread } from "@/components/assistant-ui/thread";
import { LauncherBubble } from "@/components/assistant-ui/elements/launcher-bubble";
import { TooltipProvider } from "@/components/ui/tooltip";
import { AssistantRuntimeProvider } from "@assistant-ui/react";
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
 *
 * It is a second page rather than a mode of the main one. The full app owns the whole viewport and
 * carries a sidebar and a thread list; a widget owns a corner, has no room for either, and has to
 * be able to render as *nothing but a bubble* so the host page shows through around it. Those are
 * different layouts, not one layout with a flag.
 *
 * Read {@link ../public/embed.js} next: this half only knows how big it wants to be, and says so.
 * The script on the host page is what actually resizes the frame.
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
  // Replies that landed while the panel was closed. The bubble shows the count; opening clears it.
  const [unread, setUnread] = useState(0);

  const { typing, sayTyping } = useWidgetSocket({
    desk,
    widget,
    onMessage: () => {
      if (phase === "closed") setUnread((n) => n + 1);
    },
  });

  useFrameSize(phase);

  const open = () => {
    setUnread(0);
    setPhase("open");
  };

  return (
    <AssistantRuntimeProvider runtime={widget.runtime}>
      <TooltipProvider>
        <div className="flex h-dvh w-full items-end justify-end p-3">
          {phase === "open" ? (
            <div className="bg-background border-border/60 relative flex h-full w-full flex-col overflow-hidden rounded-2xl border shadow-xl">
              <button
                type="button"
                onClick={() => setPhase("closed")}
                aria-label="Close chat"
                className="hover:bg-accent absolute end-2 top-2 z-10 rounded-full p-1.5"
              >
                <XIcon className="size-4" />
              </button>
              <HandoffBanner state={desk.state} typing={typing} onLeaveEmail={desk.leaveEmail} />
              <div className="min-h-0 flex-1">
                <Thread
                  components={WIDGET_COMPONENTS}
                  followNewMessages={
                    desk.state.status === "waiting" || desk.state.status === "human"
                  }
                />
              </div>
              <TypingReporter sayTyping={sayTyping} />
            </div>
          ) : (
            <LauncherBubble unread={unread} onToggle={open} />
          )}
        </div>
      </TooltipProvider>
    </AssistantRuntimeProvider>
  );
}
