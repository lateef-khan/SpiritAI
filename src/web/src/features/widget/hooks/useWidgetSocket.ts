import { useEffect, useRef } from "react";

import { useTypingIndicator } from "@/features/handoff/useTypingIndicator";
import { openChatwootSocket, type ChatwootSettings, type ChatwootSocket } from "@/lib/chatwoot";
import type { WidgetRuntime } from "./useWidgetRuntime";

/**
 * Keeps Chatwoot's socket open for the visitor's contact once the widget has started.
 *
 * It carries staff replies, typing, the status and the assignee, and staff presence. Every
 * reconnect reads the newest page again, so a push lost while the socket was down is late, not
 * missed. The first open needs no read: the start just made one.
 */

/** What the socket reaches into, and what it tells the screen. */
export type WidgetSocketOptions = {
  readonly widget: WidgetRuntime;
  /** Called for every staff reply, after it is on screen. */
  readonly onMessage?: () => void;
  /** How the socket is opened. Defaults to Chatwoot's. */
  readonly open?: (settings: ChatwootSettings, pubsubToken: string) => ChatwootSocket;
};

/**
 * @param options The store, and what to tell the screen.
 * @returns Whether a member of staff is typing right now.
 */
export function useWidgetSocket({
  widget,
  onMessage,
  open = openChatwootSocket,
}: WidgetSocketOptions): { typing: boolean } {
  const { chat } = widget;
  const [typing, showTyping] = useTypingIndicator();
  // The socket lives as long as the contact; the handlers read the newest render through these.
  const latest = useRef({ widget, onMessage });
  useEffect(() => {
    latest.current = { widget, onMessage };
  });

  useEffect(() => {
    if (chat === null) return;

    const socket = open(chat.settings, chat.contact.pubsub_token);
    let opened = false;

    socket.onOpen(() => {
      if (opened) void latest.current.widget.sync().catch(() => {});
      opened = true;
    });
    socket.on("message.created", (message) => {
      if (!latest.current.widget.receive(message)) return;
      showTyping(false);
      latest.current.onMessage?.();
    });
    socket.on("conversation.status_changed", (event) => latest.current.widget.move(event));
    socket.on("conversation.updated", (event) => latest.current.widget.move(event));
    socket.on("presence.update", (push) => latest.current.widget.presence(push));
    socket.on("conversation.typing_on", (push) => {
      if (push.user.type === "user" && !push.is_private) showTyping(true);
    });
    socket.on("conversation.typing_off", (push) => {
      if (push.user.type === "user" && !push.is_private) showTyping(false);
    });

    return () => socket.close();
  }, [chat, open, showTyping]);

  return { typing };
}
