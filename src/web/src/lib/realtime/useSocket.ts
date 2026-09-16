import { useCallback, useEffect, useRef } from "react";

import { openSocket, type SocketAuth, type Socket } from "./socket";

/**
 * Holds one socket open for as long as there is someone to speak as, and routes what it hears
 * to the handlers given.
 *
 * `auth` of `null` means no socket: the feature has nothing to hear right now. A change of who
 * the socket speaks as — another chat, a sign-out — closes the old one and opens a new one. The
 * auth is compared by what it names, not by identity, so a caller may build it on every render;
 * so may the handlers, which are read fresh on every event rather than closed over at open.
 */

/** What a feature wants to hear. */
export type SocketHandlers = {
  /** Fires on every open, the first and each reconnect: the moment to read the truth over REST. */
  readonly onOpen?: () => void;
  /**
   * One handler per event name. `never` is the widest parameter a handler may declare: a handler
   * written for the event's own payload type is assignable here, and nothing here reads it.
   */
  readonly on?: { readonly [event: string]: (payload: never) => void };
};

/** What the hook hands back: a way to speak, that is quiet while there is no socket. */
export type SocketHandle = {
  /** Says something to a group, when a socket is open and the hub lets this caller address it. */
  signal(group: string, name: string, payload: unknown): Promise<void>;
};

/**
 * Opens and closes the socket as `auth` changes, and hears through `handlers`.
 *
 * @param auth Who to speak as, or `null` for no socket.
 * @param handlers What to hear.
 * @param open How a socket is opened. Defaults to the real hub.
 * @returns A handle for speaking.
 */
export function useSocket(
  auth: SocketAuth | null,
  handlers: SocketHandlers,
  open: typeof openSocket = openSocket,
): SocketHandle {
  const key = auth === null ? null : auth.kind === "staff" ? "staff" : `visitor:${auth.callId}`;

  // The latest of each, read by the socket's callbacks, which outlive the render they were made
  // in. Reopening the socket on every render would drop pushes for nothing.
  const latest = useRef({ auth, handlers });
  useEffect(() => {
    latest.current = { auth, handlers };
  });

  const socketRef = useRef<Socket | null>(null);

  useEffect(() => {
    const { auth: who } = latest.current;
    if (key === null || who === null) return;

    const socket = open(who);
    socketRef.current = socket;

    // Every event name a feature will ever hear must be subscribed before the socket is up. The
    // handler under the name is looked up on each event, so the feature may change it freely.
    const names = Object.keys(latest.current.handlers.on ?? {});
    socket.onOpen(() => latest.current.handlers.onOpen?.());
    for (const name of names) {
      socket.on<never>(name, (payload) => latest.current.handlers.on?.[name]?.(payload));
    }

    return () => {
      if (socketRef.current === socket) socketRef.current = null;
      void socket.close();
    };
    // Handlers are read through the ref; only who the socket speaks as reopens it.
  }, [key, open]);

  const signal = useCallback(
    (group: string, name: string, payload: unknown) =>
      socketRef.current?.signal(group, name, payload) ?? Promise.resolve(),
    [],
  );

  return { signal };
}
