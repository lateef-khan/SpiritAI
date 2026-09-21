import { createContext, useContext, useEffect, useMemo, useRef, type ReactNode } from "react";

import type { openSocket, SocketAuth } from "./socket";
import { useSocket, type SocketHandle, type SocketHandlers } from "./useSocket";

/**
 * One socket for the whole page, shared by every feature on it.
 *
 * The hub counts a caller online for as long as their socket is up, so the socket belongs to the
 * session, not to the screen that happens to be showing. The provider opens it once, for whoever
 * is signed in, and a feature hears it through {@link useSocketEvents} for as long as it is
 * mounted. Every event name a feature will ever hear must be in `events`: the hub only relays
 * what was subscribed before the socket came up.
 *
 * A feature that mounts after the socket is up hears no `onOpen` for that open, so it reads the
 * truth over REST on mount as it always did; `onOpen` is for the reconnects that follow.
 */

/** One feature's ear, as the provider holds it. */
type Listener = {
  onOpen(): void;
  on(event: string, payload: never): void;
};

type SocketContextValue = SocketHandle & {
  listen(listener: Listener): () => void;
};

const SocketContext = createContext<SocketContextValue | null>(null);

/**
 * Opens the one socket and hands it to everything below.
 *
 * @param auth Who the socket speaks as, or `null` for no socket.
 * @param events Every event name a feature below may hear. Keep it a module constant: a new array
 *   on every render is a new subscription set.
 * @param open How a socket is opened. Defaults to the real hub.
 */
export function SocketProvider({
  auth,
  events,
  open,
  children,
}: {
  auth: SocketAuth | null;
  events: readonly string[];
  open?: typeof openSocket;
  children: ReactNode;
}) {
  const listeners = useRef(new Set<Listener>());

  const handlers = useMemo<SocketHandlers>(
    () => ({
      onOpen: () => {
        for (const listener of listeners.current) listener.onOpen();
      },
      on: Object.fromEntries(
        events.map((event) => [
          event,
          (payload: never) => {
            for (const listener of listeners.current) listener.on(event, payload);
          },
        ]),
      ),
    }),
    [events],
  );

  const { signal } = useSocket(auth, handlers, open);

  const value = useMemo<SocketContextValue>(
    () => ({
      signal,
      listen: (listener) => {
        listeners.current.add(listener);
        return () => {
          listeners.current.delete(listener);
        };
      },
    }),
    [signal],
  );

  return <SocketContext.Provider value={value}>{children}</SocketContext.Provider>;
}

/**
 * Hears the shared socket for as long as the caller is mounted.
 *
 * The handlers are read fresh on every event, so a caller may build them on every render.
 *
 * @param handlers What to hear.
 * @returns A handle for speaking.
 */
export function useSocketEvents(handlers: SocketHandlers): SocketHandle {
  const context = useContext(SocketContext);
  if (context === null) throw new Error("useSocketEvents needs a SocketProvider above it");

  const latest = useRef(handlers);
  useEffect(() => {
    latest.current = handlers;
  });

  useEffect(
    () =>
      context.listen({
        onOpen: () => latest.current.onOpen?.(),
        on: (event, payload) => latest.current.on?.[event]?.(payload),
      }),
    [context],
  );

  return { signal: context.signal };
}
