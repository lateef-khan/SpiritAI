"use client";

import { ThreadPrimitive } from "@assistant-ui/react";
import { BotIcon } from "lucide-react";
import type { FC } from "react";

/**
 * The widget's first screen: A greeting written as a real message.
 */

/** The ways in, in the order they are offered. Each is sent as the visitor's own words. */
const OPENERS = [
  "Find a part for my treadmill",
  "My machine has an error code",
  "Check my warranty",
] as const;

export const WidgetWelcome: FC = () => (
  <div data-slot="aui_widget-welcome" className="mb-6 flex flex-col gap-3 px-2">
    <div className="flex items-start gap-2">
      <span className="bg-muted text-muted-foreground flex size-7 shrink-0 items-center justify-center rounded-full">
        <BotIcon aria-hidden className="size-4" />
      </span>
      <p className="bg-muted text-foreground rounded-2xl px-3 py-2 text-sm leading-relaxed">
        Hi, I am Spirit, an AI assistant. Pick an option below, or ask a question to get started.
      </p>
    </div>

    <div className="flex flex-col items-start gap-2 ps-9">
      {OPENERS.map((prompt) => (
        <ThreadPrimitive.Suggestion
          key={prompt}
          prompt={prompt}
          send
          className="bg-background hover:bg-muted/80 rounded-full border px-3 py-1 text-sm whitespace-nowrap transition-colors ease-in"
        >
          {prompt}
        </ThreadPrimitive.Suggestion>
      ))}
    </div>
  </div>
);
