import { HubConnectionBuilder, HubConnectionState, LogLevel } from "@microsoft/signalr";

/**
 * The browser's end of the host's one socket, `/v1/realtime/hub`, with no React and no feature
 * in sight.
 *
 * The hub admits two kinds of caller. A member of staff sends the Neon token; a browser socket
 * cannot set a header, so it goes as `?access_token=`, which the host reads on this path alone.
 * A visitor names the chat and sends their key in the query string, and the hub checks they own
 * it. Once in, both hear named events and may signal a group the hub lets them address.
 *
 * What arrives is a hint. The truth is REST, and a feature that owns a socket reads it again on
 * every open — so a push lost while the socket was down is late, never missed.
 *
 * The hub keeps no timer of its own: the client says it is still here every
 * {@link HeartbeatSeconds}, and a socket silent for three of those is swept. Presence is what the
 * host counts staff by and what decides whether a reply also goes out by mail, so the heartbeat is
 * not optional.
 */

/** Where the host maps the hub. */
export const HubPath = "/v1/realtime/hub";

/** How often the client says it is still here. The host's window is three times this. */
export const HeartbeatSeconds = 30;

/** How long a start that failed waits before it is tried again. */
const RetrySeconds = 5;

/** Who the socket speaks as. */
export type SocketAuth =
  | { readonly kind: "staff"; readonly token: () => Promise<string | null> }
  | { readonly kind: "visitor"; readonly callId: string; readonly visitorKey: string };

/**
 * One socket's word to a group, as the hub relays it under the `signal` event: the sender is
 * stamped on by the hub, the payload passes through unread.
 */
export type Signal<T = unknown> = {
  readonly sender: { readonly key: string; readonly kind: string };
  readonly group: string;
  readonly name: string;
  readonly payload: T;
};

/** How many callers of one kind are online, as the `presence` event carries it. */
export type Presence = {
  readonly kind: string;
  readonly online: number;
};

/** The part of a hub connection this module uses, so a test can hand it one that reaches no network. */
export type HubLike = {
  readonly state: HubConnectionState;
  on(event: string, handler: (...args: unknown[]) => void): void;
  off(event: string, handler: (...args: unknown[]) => void): void;
  onreconnected(handler: () => void): void;
  onclose(handler: () => void): void;
  start(): Promise<void>;
  stop(): Promise<void>;
  invoke(method: string, ...args: unknown[]): Promise<unknown>;
};

/** An open socket, as a feature holds it. */
export type Socket = {
  /** Hears one named event. Answers how to stop hearing it. */
  on<T>(event: string, handler: (payload: T) => void): () => void;
  /** Fires on every open, the first and each reconnect, before the pushes that follow it. Answers how to stop. */
  onOpen(handler: () => void): () => void;
  /** Says something to a group, when the hub lets this caller address it. Nothing is stored. */
  signal(group: string, name: string, payload: unknown): Promise<void>;
  /** Ends the socket. Safe to call more than once. */
  close(): Promise<void>;
};

/** Builds the real connection to the hub for one caller. */
function connectTo(auth: SocketAuth): HubLike {
  const builder = new HubConnectionBuilder();

  if (auth.kind === "staff") {
    builder.withUrl(HubPath, {
      accessTokenFactory: async () => (await auth.token()) ?? "",
    });
  } else {
    const query = new URLSearchParams({ call: auth.callId, visitor: auth.visitorKey });
    builder.withUrl(`${HubPath}?${query}`);
  }

  return builder.withAutomaticReconnect().configureLogging(LogLevel.Warning).build();
}

/**
 * Opens the socket for one caller and keeps it open until closed.
 *
 * @param auth Who the socket speaks as.
 * @param connect How the connection is built. Defaults to the real hub.
 * @returns The socket. It starts at once; a failed start retries on its own.
 */
export function openSocket(
  auth: SocketAuth,
  connect: (auth: SocketAuth) => HubLike = connectTo,
): Socket {
  const hub = connect(auth);
  const opens = new Set<() => void>();
  let closed = false;

  // The hub only knows a socket is alive while it hears from it. Stopped with the socket, and
  // stopped while it is down: an invoke on a closed connection throws for nothing.
  let heartbeat: ReturnType<typeof setInterval> | null = null;

  const beat = () => {
    heartbeat ??= setInterval(() => {
      if (hub.state === HubConnectionState.Connected) {
        hub.invoke("Heartbeat").catch(() => {
          // A missed beat is nothing on its own; the next one, or the sweep, settles it.
        });
      }
    }, HeartbeatSeconds * 1000);
  };

  const rest = () => {
    if (heartbeat !== null) clearInterval(heartbeat);
    heartbeat = null;
  };

  const opened = () => {
    beat();
    for (const handler of opens) handler();
  };

  hub.onreconnected(opened);
  hub.onclose(rest);

  // A start that fails — the host asleep, the network down — is tried again until it takes or
  // the socket is closed. Automatic reconnect only covers a connection that was once up.
  const start = async () => {
    while (!closed) {
      try {
        await hub.start();
        if (!closed) opened();
        return;
      } catch {
        await new Promise((resolve) => setTimeout(resolve, RetrySeconds * 1000));
      }
    }
  };
  
  void start();

  return {
    on: (event, handler) => {
      const hear = (payload: unknown) => handler(payload as never);
      hub.on(event, hear);
      return () => hub.off(event, hear);
    },
    onOpen: (handler) => {
      opens.add(handler);
      return () => {
        opens.delete(handler);
      };
    },
    signal: async (group, name, payload) => {
      if (hub.state !== HubConnectionState.Connected) return;
      await hub.invoke("Signal", group, name, payload).catch(() => {
        // A signal is a hint between browsers; one that did not go is nothing.
      });
    },
    close: async () => {
      closed = true;
      rest();
      await hub.stop().catch(() => {
        // Already down is the state being asked for.
      });
    },
  };
}
