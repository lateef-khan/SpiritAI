"use client";

import { useAssistantDataUI, type DataMessagePartProps } from "@assistant-ui/react";

/**
 * What one compaction notice carries, as {@link ../../../features/threads/AgentCoreRuntime.ts
 * `noteContent`} writes it onto the `data` part.
 */
type CompactionNoteData = {
  readonly phase: "start" | "end";
  readonly outcome?: string;
};

/** The line shown for one phase of a compaction pass, or `null` to show nothing. */
function textFor(data: CompactionNoteData): string | null {
  if (data.phase === "start") {
    return "Tidying up this conversation…";
  }
  switch (data.outcome) {
    case "compacted":
      return "This conversation was tidied up.";
    case "failed":
      return "Couldn't tidy up this conversation.";
    default:
      return null;
  }
}

/**
 * One compaction notice, drawn the same way as the host's own event lines ("Dana joined"): centred,
 * muted, no bubble.
 */
function CompactionNotePart({ data }: DataMessagePartProps<CompactionNoteData>) {
  const text = textFor(data);
  if (!text) return null;

  return (
    <div data-slot="aui_system-note" className="flex justify-center px-2">
      <div className="text-muted-foreground text-center text-[13px] leading-relaxed">{text}</div>
    </div>
  );
}

/**
 * Registers the renderer above for `data` parts named `"compaction"`, for as long as it stays
 * mounted. Mount once per thread — see {@link ../thread.tsx}.
 */
export function CompactionNoteUI() {
  useAssistantDataUI({ name: "compaction", render: CompactionNotePart });
  return null;
}
