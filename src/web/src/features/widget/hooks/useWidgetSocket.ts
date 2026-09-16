import { useCallback } from "react";

import * as Events from "@/features/handoff/events";
import { useTypingIndicator } from "@/features/handoff/useTypingIndicator";
import type { Presence, Signal } from "@/lib/realtime/socket";
import { useSocket } from "@/lib/realtime/useSocket";
import { readVisitorMemory } from "../api/visitorIdentity";
import type { WireHandoffMessage } from "../api/widgetApi";
import type { HandoffDesk } from "./useHandoffDesk";
import type { WidgetRuntime } from "./useWidgetRuntime";

/**
 * Keeps the socket open while a person is asked for or has the chat, and closed otherwise.
 *
 * With the bot there is nothing to hear: the bot answers on the turn's own stream. The socket is
 * for the human phase — a reply landing, the line moving, a person joining or leaving — and it
 * closes again once the chat is back with the bot, so a visitor who never asked for anyone costs
 * the host no connection.
 *
 * Every open, the first and each reconnect, reads the state and the history over REST before it
 * listens: a push lost while the socket was down is caught up, not missed.
 */

/** What the socket reaches into, and what it tells the screen. */
export type WidgetSocketOptions = {
  readonly desk: HandoffDesk;
  readonly widget: WidgetRuntime;
  /** Called for every message a person or the host wrote, after it is on screen. */
  readonly onMessage?: (message: WireHandoffMessage) => void;
  /** How the socket is opened. Defaults to the real hub. */
  readonly open?: Parameters<typeof useSocket>[2];
};

/** What the widget learns from the socket beyond the desk and the store. */
export type WidgetSocketState = {
  /** Whether the person holding the chat is typing right now. */
  readonly typing: boolean;
  /** Tells the person holding the chat whether the visitor is typing. */
  sayTyping(on: boolean): void;
};

/**
 * Opens and closes the socket as the chat's state and call id change.
 *
 * @param options The desk, the store, and what to tell the screen.
 * @returns Typing, in and out.
 */
export function useWidgetSocket({
  desk,
  widget,
  onMessage,
  open,
}: WidgetSocketOptions): WidgetSocketState {
  const { callId } = widget;
  const listening = desk.state.status === "waiting" || desk.state.status === "human";
  const [typing, showTyping] = useTypingIndicator();

  const handle = useSocket(
    callId !== null && listening
      ? { kind: "visitor", callId, visitorKey: readVisitorMemory().key }
      : null,
    {
      onOpen: () => {
        if (callId !== null) void desk.refresh(callId);
        void widget.reload();
      },
      on: {
        [Events.MessageCreated]: (message: WireHandoffMessage) => {
          widget.receive(message);
          // The visitor's own words are already on screen; the push is the host telling staff.
          if (message.role !== "user") {
            showTyping(false);
            onMessage?.(message);
          }
        },
        [Events.Queue]: (push: Events.QueuePush) => desk.apply({ position: push.position }),
        [Events.Claimed]: (push: Events.ClaimedPush) =>
          desk.apply({ status: "human", position: null, assigneeName: push.assignee.name }),
        [Events.Done]: () => {
          desk.apply({ status: "done", position: null, assigneeName: null });
          showTyping(false);
        },
        presence: (push: Presence) => {
          if (push.kind === Events.StaffKind) desk.apply({ staffOnline: push.online });
        },
        signal: (signal: Signal<Events.TypingSignal>) => {
          if (signal.name === Events.Typing && signal.sender.kind === Events.StaffKind) {
            showTyping(signal.payload.on);
          }
        },
      },
    },
    open,
  );

  const sayTyping = useCallback(
    (on: boolean) => {
      if (callId !== null) void handle.signal(Events.StaffGroup, Events.Typing, { callId, on });
    },
    [callId, handle],
  );

  return { typing, sayTyping };
}
