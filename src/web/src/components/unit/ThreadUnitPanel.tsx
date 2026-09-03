import { useAui, useAuiState } from "@assistant-ui/react";
import { useMemo } from "react";

import { flatten } from "@/runtime/AgentCoreRuntime";

import { UnitPanel } from "./UnitPanel";

/**
 * The panel, wired to the open thread.
 *
 * Separate from {@link UnitPanel} for the same reason `transport.ts` is separate from the runtime:
 * everything worth testing about the panel is what it does with a list of turns, and a list of
 * turns needs no React tree with an assistant runtime in it.
 */
export function ThreadUnitPanel({ className }: { className?: string }) {
  const aui = useAui();
  const messages = useAuiState((state) => state.thread.messages);
  const isRunning = useAuiState((state) => state.thread.isRunning);

  const said = useMemo(
    () => messages.map((message) => ({ role: message.role, text: flatten(message).content })),
    [messages],
  );

  return (
    <UnitPanel
      said={said}
      isRunning={isRunning}
      className={className}
      onAsk={(question) => {
        // Appending mid-stream aborts the running turn rather than queueing behind it, which is why
        // the rows are inert above as well as guarded here.
        if (aui.thread.getState().isRunning) return;

        aui.thread.append({ role: "user", content: [{ type: "text", text: question }] });
      }}
    />
  );
}
