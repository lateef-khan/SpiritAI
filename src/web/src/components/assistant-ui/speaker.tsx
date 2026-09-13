"use client";

import type { Speaker } from "@/features/threads/transport";
import { useAuiState } from "@assistant-ui/react";
import { BotIcon, HeadsetIcon, ServerIcon } from "lucide-react";
import { createContext, useContext, type FC } from "react";

export const TranscriptModeContext = createContext(false);

/**
 * The name on an answer, when it was not the model that wrote it.
 */
export function useSpeaker(): Speaker | null {
  return useAuiState((s) => {
    if (s.message.role !== "assistant") return null;

    const custom = s.message.metadata?.custom as { speaker?: unknown } | undefined;
    const speaker = custom?.speaker;

    if (
      typeof speaker === "object" &&
      speaker !== null &&
      typeof (speaker as Speaker).name === "string"
    ) {
      return speaker as Speaker;
    }

    // The stored object, not a fresh one: `useAuiState` compares by identity, and a new object per
    // call is an infinite render loop rather than a re-render.
    return null;
  });
}

const ICONS = {
  agent: BotIcon,
  human: HeadsetIcon,
  system: ServerIcon,
} as const;

export const MessageSpeaker: FC = () => {
  const speaker = useSpeaker();

  const isTranscript = useContext(TranscriptModeContext);

  const effective: Speaker | null = speaker ?? (isTranscript ? { kind: "agent", name: "Spirit" } : null);

  if (!effective || (effective.kind === "agent" && !isTranscript)) return null;

  const Icon = ICONS[effective.kind] ?? ICONS.agent;

  return (
    <div
      data-slot="aui_message-speaker"
      className="text-muted-foreground mb-1 flex items-center gap-1.5 text-xs"
    >
      <Icon aria-hidden className="size-3.5 shrink-0" />
      <span className="font-medium">{effective.name}</span>
      {effective.detail && <span className="text-foreground/40">· {effective.detail}</span>}
    </div>
  );
};
