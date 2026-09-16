import { useCallback, useEffect, useRef, useState } from "react";

/**
 * Holds whether the other side of a chat is typing, as the socket says.
 *
 * "Typing" is a hint that may never be followed by "stopped" — a tab closed mid-word — so it
 * fades on its own after {@link TypingFadeMs}. The sender says it again every few seconds while
 * it holds, which keeps a long message from fading early.
 */

/** How long "typing" is shown after the last word of it, when no "stopped" arrives. */
export const TypingFadeMs = 4000;

/**
 * @returns Whether they are typing, and how to say so.
 */
export function useTypingIndicator(): [typing: boolean, show: (on: boolean) => void] {
  const [typing, setTyping] = useState(false);
  const fade = useRef<ReturnType<typeof setTimeout> | null>(null);

  const show = useCallback((on: boolean) => {
    if (fade.current !== null) clearTimeout(fade.current);
    fade.current = on ? setTimeout(() => setTyping(false), TypingFadeMs) : null;
    setTyping(on);
  }, []);

  useEffect(
    () => () => {
      if (fade.current !== null) clearTimeout(fade.current);
    },
    [],
  );

  return [typing, show];
}
