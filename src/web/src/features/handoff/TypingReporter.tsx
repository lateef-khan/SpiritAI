import { useAuiState } from "@assistant-ui/react";
import { useEffect, useRef } from "react";

/**
 * Tells the other side of a chat whether this side is typing, read off assistant-ui's composer.
 *
 * Rendered inside `AssistantRuntimeProvider` and drawing nothing: it exists to sit where the
 * composer's text can be read. "Typing" goes out when the box goes from empty to not, and again
 * every {@link TypingRepeatMs} while it stays so, since the receiver lets it fade on its own;
 * "stopped" goes out when the box empties — a send, or a delete.
 */

/** How often "typing" is said again while the box stays non-empty. */
export const TypingRepeatMs = 3000;

/** The one thing the reporter needs to know: how to say it. */
export type TypingReporterProps = {
  readonly sayTyping: (on: boolean) => void;
};

export function TypingReporter({ sayTyping }: TypingReporterProps) {
  const busy = useAuiState((s) => s.composer.text.length > 0);
  const say = useRef(sayTyping);

  useEffect(() => {
    say.current = sayTyping;
  });

  useEffect(() => {
    if (!busy) {
      say.current(false);
      return;
    }

    say.current(true);
    const repeat = setInterval(() => say.current(true), TypingRepeatMs);

    return () => clearInterval(repeat);
  }, [busy]);

  return null;
}
