import type { ReactNode } from "react";

import { act, renderHook } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";

import { SocketProvider, useSocketEvents } from "./SocketProvider";
import type { Socket, SocketAuth } from "./socket";

/**
 * The shared socket, one test per rule: one socket serves every listener, a listener hears only
 * while it is mounted, and a signal goes through the one socket.
 */

/** A socket a test can open, raise events on, and count. */
function fakeOpen() {
  let opened = 0;
  const handlers = new Map<string, (payload: unknown) => void>();
  let onOpen: (() => void) | null = null;
  const signals: unknown[][] = [];

  const open = (): Socket => {
    opened += 1;
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
      close: async () => {},
    };
  };

  return {
    open,
    opened: () => opened,
    signals,
    arrive: () => onOpen?.(),
    raise: (event: string, payload: unknown) => handlers.get(event)?.(payload),
  };
}

const staff: SocketAuth = { kind: "staff", token: async () => "token" };
const events = ["ping"];

describe("SocketProvider", () => {
  it("opens one socket and hands every push to every listener mounted", () => {
    const socket = fakeOpen();
    const wrapper = ({ children }: { children: ReactNode }) => (
      <SocketProvider auth={staff} events={events} open={socket.open}>
        {children}
      </SocketProvider>
    );
    const first = vi.fn();
    const second = vi.fn();
    const opens = vi.fn();

    renderHook(
      () => {
        useSocketEvents({ onOpen: opens, on: { ping: first } });
        useSocketEvents({ on: { ping: second } });
      },
      { wrapper },
    );

    act(() => socket.arrive());
    act(() => socket.raise("ping", 1));

    expect(socket.opened()).toBe(1);
    expect(opens).toHaveBeenCalledTimes(1);
    expect(first).toHaveBeenCalledWith(1);
    expect(second).toHaveBeenCalledWith(1);
  });

  it("stops a listener hearing once it unmounts, and keeps the socket", () => {
    const socket = fakeOpen();
    const wrapper = ({ children }: { children: ReactNode }) => (
      <SocketProvider auth={staff} events={events} open={socket.open}>
        {children}
      </SocketProvider>
    );
    const heard = vi.fn();

    const view = renderHook(() => useSocketEvents({ on: { ping: heard } }), { wrapper });
    view.unmount();

    act(() => socket.raise("ping", 1));

    expect(heard).not.toHaveBeenCalled();
    expect(socket.opened()).toBe(1);
  });

  it("speaks through the one socket", async () => {
    const socket = fakeOpen();
    const wrapper = ({ children }: { children: ReactNode }) => (
      <SocketProvider auth={staff} events={events} open={socket.open}>
        {children}
      </SocketProvider>
    );

    const view = renderHook(() => useSocketEvents({}), { wrapper });
    await act(async () => {
      await view.result.current.signal("group", "name", { on: true });
    });

    expect(socket.signals).toEqual([["group", "name", { on: true }]]);
  });
});
