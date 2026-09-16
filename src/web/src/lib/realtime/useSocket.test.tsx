import { act, renderHook } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";

import type { Socket, SocketAuth } from "./socket";
import { useSocket } from "./useSocket";

/**
 * The socket hook, one test per rule: a socket exists exactly while there is someone to speak
 * as, the handlers in force are the latest ones, and a signal goes through the open socket.
 */

/** A socket a test can open, raise events on, and count. */
function fakeOpen() {
  const opened: SocketAuth[] = [];
  let closed = 0;
  const handlers = new Map<string, (payload: unknown) => void>();
  let onOpen: (() => void) | null = null;
  const signals: unknown[][] = [];

  const open = (auth: SocketAuth): Socket => {
    opened.push(auth);
    return {
      on: (event, handler) => {
        handlers.set(event, handler as (payload: unknown) => void);
        return () => handlers.delete(event);
      },
      onOpen: (handler) => {
        onOpen = handler;
        return () => {
          onOpen = null;
        };
      },
      signal: async (...args) => {
        signals.push(args);
      },
      close: async () => {
        closed += 1;
      },
    };
  };

  return {
    open,
    opened,
    signals,
    closed: () => closed,
    arrive: () => onOpen?.(),
    raise: (event: string, payload: unknown) => handlers.get(event)?.(payload),
  };
}

const visitor: SocketAuth = { kind: "visitor", callId: "call-1", visitorKey: "key-1" };

describe("useSocket", () => {
  it("opens for who it is told to, and closes when there is nobody", () => {
    const socket = fakeOpen();

    const view = renderHook(({ auth }) => useSocket(auth, {}, socket.open), {
      initialProps: { auth: null as SocketAuth | null },
    });
    expect(socket.opened).toEqual([]);

    view.rerender({ auth: visitor });
    expect(socket.opened).toEqual([visitor]);

    view.rerender({ auth: { ...visitor } });
    expect(socket.opened).toHaveLength(1);

    view.rerender({ auth: null });
    expect(socket.closed()).toBe(1);
  });

  it("reopens for another chat", () => {
    const socket = fakeOpen();

    const view = renderHook(({ auth }) => useSocket(auth, {}, socket.open), {
      initialProps: { auth: visitor as SocketAuth },
    });
    view.rerender({ auth: { ...visitor, callId: "call-2" } });

    expect(socket.closed()).toBe(1);
    expect(socket.opened.map((a) => (a.kind === "visitor" ? a.callId : a.kind))).toEqual([
      "call-1",
      "call-2",
    ]);
  });

  it("hears through the latest handlers, and an open through onOpen", () => {
    const socket = fakeOpen();
    const first = vi.fn();
    const second = vi.fn();
    const opened = vi.fn();

    const view = renderHook(
      ({ handler }) =>
        useSocket(visitor, { onOpen: opened, on: { "handoff.done": handler } }, socket.open),
      { initialProps: { handler: first } },
    );
    view.rerender({ handler: second });

    act(() => {
      socket.arrive();
      socket.raise("handoff.done", { callId: "call-1" });
    });

    expect(opened).toHaveBeenCalledTimes(1);
    expect(first).not.toHaveBeenCalled();
    expect(second).toHaveBeenCalledWith({ callId: "call-1" });
  });

  it("signals through the open socket, and quietly not at all without one", async () => {
    const socket = fakeOpen();

    const view = renderHook(({ auth }) => useSocket(auth, {}, socket.open), {
      initialProps: { auth: null as SocketAuth | null },
    });
    await view.result.current.signal("handoff:staff", "typing", { on: true });
    expect(socket.signals).toEqual([]);

    view.rerender({ auth: visitor });
    await view.result.current.signal("handoff:staff", "typing", { on: true });
    expect(socket.signals).toEqual([["handoff:staff", "typing", { on: true }]]);
  });
});
