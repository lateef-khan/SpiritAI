import { HeadsetIcon, HourglassIcon } from "lucide-react";

import { TypingDots } from "@/components/assistant-ui/elements/typing-indicator";
import type { HandoffState } from "../hooks/desk";

/**
 * The strip above the chat while a person is asked for, or has the chat.
 */

/** Where the chat stands, and whether the person on it is typing. */
export type HandoffBannerProps = {
  state: HandoffState;
  typing?: boolean;
};

/** The one line that says where the chat stands. */
function Line({ state, typing }: { state: HandoffState; typing: boolean }) {
  if (state.status === "human") {
    const name = state.assigneeName ?? "A member of staff";
    return (
      <p className="flex items-center gap-2">
        <HeadsetIcon className="size-4 shrink-0" aria-hidden />
        <span>
          <span className="font-medium">{name}</span>{" "}
          {typing ? (
            <>
              is typing <TypingDots />
            </>
          ) : (
            "is with you."
          )}
        </span>
      </p>
    );
  }

  return (
    <p className="flex items-center gap-2">
      <HourglassIcon className="size-4 shrink-0" aria-hidden />
      <span>Waiting for a person.</span>
    </p>
  );
}

/**
 * Whether anyone is behind the desk.
 */
function Presence({ online }: { online: boolean }) {
  return (
    <p className="text-muted-foreground flex items-center gap-2">
      <span
        aria-hidden
        className={`size-2 shrink-0 rounded-full ${online ? "bg-emerald-500" : "bg-muted-foreground/40"}`}
      />
      <span>{online ? "Someone is online." : "Nobody is online right now."}</span>
    </p>
  );
}

export function HandoffBanner({ state, typing = false }: HandoffBannerProps) {
  if (state.status !== "waiting" && state.status !== "human") return null;

  return (
    <div
      role="status"
      className="bg-muted text-foreground border-border/60 flex flex-col gap-2 border-b ps-4 pe-10 py-3 text-sm"
    >
      <Line state={state} typing={typing} />
      {state.status === "waiting" && <Presence online={state.staffOnline} />}
    </div>
  );
}
