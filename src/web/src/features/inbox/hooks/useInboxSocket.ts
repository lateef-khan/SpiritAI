import { useCallback, useEffect } from "react";

import * as Events from "@/features/handoff/events";
import { useTypingIndicator } from "@/features/handoff/useTypingIndicator";
import { useSocketEvents } from "@/lib/realtime/SocketProvider";
import type { Signal } from "@/lib/realtime/socket";

/**
 * The chat on screen's live side: whether its visitor is typing, and telling them whether the
 * viewer is.
 *
 * The rows and the transcript are the cache's, kept current by `useHandoffPushes` from wherever
 * it is mounted; this hears the socket only for what is not stored anywhere. The socket itself
 * outlives the inbox: it is the session's, so staff count as online on every screen.
 */

/** What the inbox reaches into. */
export type InboxSocketOptions = {
  /** The chat on screen, or `null` when none is picked. */
  readonly selectedCallId: string | null;
};

/** What the inbox learns from the socket beyond the cache. */
export type InboxSocketState = {
  /** Whether the visitor of the chat on screen is typing right now. */
  readonly typing: boolean;
  /** Tells the visitor of the chat on screen whether the viewer is typing. */
  sayTyping(on: boolean): void;
};

/**
 * Hears the staff socket for the life of the inbox.
 *
 * @param options Which chat is on screen.
 * @returns Typing, in and out.
 */
export function useInboxSocket({ selectedCallId }: InboxSocketOptions): InboxSocketState {
  const [typing, showTyping] = useTypingIndicator();

  // A pick of another chat starts clean: whoever was typing was typing somewhere else.
  useEffect(() => {
    showTyping(false);
  }, [selectedCallId, showTyping]);

  const handle = useSocketEvents({
    on: {
      // The visitor's words landing means they stopped typing them.
      [Events.MessageCreated]: (message: Events.MessagePush) => {
        if (message.callId === selectedCallId && message.role === "user") showTyping(false);
      },
      signal: (signal: Signal<Events.TypingSignal>) => {
        if (
          signal.name === Events.Typing &&
          signal.sender.kind !== Events.StaffKind &&
          signal.payload.callId === selectedCallId
        ) {
          showTyping(signal.payload.on);
        }
      },
    },
  });

  const sayTyping = useCallback(
    (on: boolean) => {
      if (selectedCallId !== null) {
        void handle.signal(Events.callGroup(selectedCallId), Events.Typing, {
          callId: selectedCallId,
          on,
        });
      }
    },
    [handle, selectedCallId],
  );

  return { typing, sayTyping };
}
