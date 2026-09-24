import { createConsumer, type Consumer } from "@rails/actioncable";
import type { ChatwootSettings } from "./settings.ts";
import type { ChatwootEvents } from "./wire.ts";

/**
 * The widget's end of Chatwoot's socket: ActionCable's `RoomChannel`, subscribed with the
 * contact's `pubsub_token`. It carries staff replies, typing, the assignee, the status, and staff
 * presence. `@rails/actioncable` does the reconnecting.
 *
 * What arrives is a hint. The truth is the Client API, and a feature re-reads it on every open,
 * so a push lost while the socket was down is late, never missed.
 */

/**
 * How often the socket says the visitor is still here. Chatwoot forgets a presence after 20
 * seconds, and each refresh is answered with a `presence.update` for staff.
 */
export const PresenceSeconds = 20;

/** An open socket, as a feature holds it. */
export type ChatwootSocket = {
  /** Hears one event. Answers how to stop hearing it. */
  on<E extends keyof ChatwootEvents>(
    event: E,
    handler: (data: ChatwootEvents[E]) => void,
  ): () => void;
  /** Fires on every open, the first and each reconnect. Answers how to stop. */
  onOpen(handler: () => void): () => void;
  /** Ends the socket. Safe to call more than once. */
  close(): void;
};

/** One frame's payload, as Chatwoot broadcasts it on the channel. */
type Broadcast = { event: string; data: unknown };

/** The socket's address: the base URL's `/cable`, over `ws` or `wss`. */
export function cableUrl(settings: ChatwootSettings): string {
  const url = new URL("/cable", settings.chatwootBaseUrl);
  url.protocol = url.protocol === "https:" ? "wss:" : "ws:";
  return url.toString();
}

/**
 * Opens the socket for one contact and keeps it open until closed.
 *
 * @param settings Where Chatwoot is.
 * @param pubsubToken The contact's `pubsub_token`, from `ChatwootClient.contact`.
 * @param consumer The ActionCable consumer. Defaults to one on {@link cableUrl}.
 * @returns The socket. It connects at once.
 */
export function openChatwootSocket(
  settings: ChatwootSettings,
  pubsubToken: string,
  consumer: Consumer = createConsumer(cableUrl(settings)),
): ChatwootSocket {
  const handlers = new Map<string, Set<(data: never) => void>>();
  const opens = new Set<() => void>();
  let presence: ReturnType<typeof setInterval> | null = null;
  let closed = false;

  const stopPresence = () => {
    if (presence !== null) clearInterval(presence);
    presence = null;
  };

  const subscription = consumer.subscriptions.create(
    { channel: "RoomChannel", pubsub_token: pubsubToken },
    {
      connected() {
        this.perform("update_presence");
        stopPresence();
        presence = setInterval(() => this.perform("update_presence"), PresenceSeconds * 1000);
        for (const handler of opens) handler();
      },
      disconnected() {
        stopPresence();
      },
      received(frame: Broadcast) {
        for (const handler of handlers.get(frame.event) ?? []) handler(frame.data as never);
      },
    },
  );

  return {
    on: (event, handler) => {
      const set = handlers.get(event) ?? new Set();
      set.add(handler as (data: never) => void);
      handlers.set(event, set);
      return () => {
        set.delete(handler as (data: never) => void);
      };
    },
    onOpen: (handler) => {
      opens.add(handler);
      return () => {
        opens.delete(handler);
      };
    },
    close: () => {
      if (closed) return;
      closed = true;
      stopPresence();
      subscription.unsubscribe();
      consumer.disconnect();
    },
  };
}
