import { useCallback, useEffect } from "react";

import { currentToken } from "@/features/auth";
import * as Events from "@/features/handoff/events";
import { useTypingIndicator } from "@/features/handoff/useTypingIndicator";
import type { Signal } from "@/lib/realtime/socket";
import { useSocket } from "@/lib/realtime/useSocket";

/**
 * Keeps the inbox current while it is open, off the staff socket.
 *
 * Every push is a reason to read again, not a thing to apply: the list is reloaded when a chat
 * joins the queue, is taken, is closed, or moves in the line, and the open transcript is reloaded
 * when a message lands in it. The socket is a hint; REST is the truth, and both are read on every
 * open too, so a push lost while the socket was down is caught up.
 */

/** What the inbox reaches into, and what it tells the screen. */
export type InboxSocketOptions = {
  /** The chat on screen, or `null` when none is picked. */
  readonly selectedCallId: string | null;
  readonly reloadList: () => void;
  readonly reloadTranscript: () => void;
  /** How the socket is opened. Defaults to the real hub. */
  readonly open?: Parameters<typeof useSocket>[2];
};

/** What the inbox learns from the socket beyond the list and the transcript. */
export type InboxSocketState = {
  /** Whether the visitor of the chat on screen is typing right now. */
  readonly typing: boolean;
  /** Tells the visitor of the chat on screen whether the viewer is typing. */
  sayTyping(on: boolean): void;
};

const staff = { kind: "staff", token: currentToken } as const;

/**
 * Opens the staff socket for the life of the inbox.
 *
 * @param options The list, the transcript, and which chat is on screen.
 * @returns Typing, in and out.
 */
export function useInboxSocket({
  selectedCallId,
  reloadList,
  reloadTranscript,
  open,
}: InboxSocketOptions): InboxSocketState {
  const [typing, showTyping] = useTypingIndicator();

  // A pick of another chat starts clean: whoever was typing was typing somewhere else.
  useEffect(() => {
    showTyping(false);
  }, [selectedCallId, showTyping]);

  const handle = useSocket(
    staff,
    {
      onOpen: () => {
        reloadList();
        if (selectedCallId !== null) reloadTranscript();
      },
      on: {
        [Events.Waiting]: () => reloadList(),
        [Events.Claimed]: () => reloadList(),
        [Events.Done]: () => reloadList(),
        [Events.Queue]: () => reloadList(),
        [Events.MessageCreated]: (message: Events.MessagePush) => {
          if (message.callId !== selectedCallId) return;
          if (message.role === "user") showTyping(false);
          reloadTranscript();
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
    },
    open,
  );

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
