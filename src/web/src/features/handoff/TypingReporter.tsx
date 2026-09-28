import { useAuiState } from "@assistant-ui/react";
import { useEffect, useRef } from "react";

/**
 * Tells the other side of a chat whether this side is typing, read off assistant-ui's composer.
 *
 * Rendered inside `AssistantRuntimeProvider` and drawing nothing: it exists to sit where the
 * composer's text can be read. It speaks only when the state changes: "typing" when the box goes
 * from empty to not, "stopped" when it empties again — a send, or a delete.
 */

/** The one thing the reporter needs to know: how to say it. */
export type TypingReporterProps = {
  readonly sayTyping: (on: boolean) => void;
};

export function TypingReporter({ sayTyping }: TypingReporterProps) {
  const busy = useAuiState((s) => s.composer.text.length > 0);
  const say = useRef(sayTyping);
  const said = useRef(false);

  useEffect(() => {
    say.current = sayTyping;
  });

  useEffect(() => {
    if (said.current === busy) return;
    said.current = busy;
    say.current(busy);
  }, [busy]);

  return null;
}
