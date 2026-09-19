import type { ReactNode } from "react";

import { act, renderHook } from "@testing-library/react";
import { describe, expect, it } from "vitest";

import * as Events from "@/features/handoff/events";
import { SocketProvider } from "@/lib/realtime/SocketProvider";
import type { Socket, SocketAuth } from "@/lib/realtime/socket";
import { useInboxSocket } from "./useInboxSocket";

/**
 * The inbox's socket hook, one test per rule: typing goes both ways for the open chat alone, and
 * the visitor's words landing ends their typing. The rows and the transcript are the cache's
 * business, under `handoffCache.test.ts`. The socket is the app's, so each test stands one up
 * the way the app does.
 */

/** A socket a test can open, raise events on, and count. */
function fakeOpen() {
  const handlers = new Map<string, (payload: unknown) => void>();
  let onOpen: (() => void) | null = null;
  const signals: unknown[][] = [];

  const open = (): Socket => {
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

  const staff: SocketAuth = { kind: "staff", token: async () => "token" };
  const wrapper = ({ children }: { children: ReactNode }) => (
    <SocketProvider auth={staff} events={Events.StaffEvents} open={open}>
      {children}
    </SocketProvider>
  );

  return {
    wrapper,
    signals,
    arrive: () => onOpen?.(),
    raise: (event: string, payload: unknown) => handlers.get(event)?.(payload),
  };
}

const message = (callId: string, role = "user") => ({
  callId,
  messageId: `m-${callId}`,
  role,
  text: "hi",
  speaker: null,
  at: "2026-09-16T09:00:00Z",
});

const typingFrom = (kind: string, callId: string, on: boolean) => ({
  sender: { key: "k", kind },
  group: "handoff:staff",
  name: "typing",
  payload: { callId, on },
});

describe("useInboxSocket", () => {
  it("shows the open chat's visitor typing, and no one else", () => {
    const socket = fakeOpen();

    const view = renderHook(() => useInboxSocket({ selectedCallId: "call-1" }), {
      wrapper: socket.wrapper,
    });

    act(() => socket.raise("signal", typingFrom("visitor", "call-2", true)));
    expect(view.result.current.typing).toBe(false);

    act(() => socket.raise("signal", typingFrom("staff", "call-1", true)));
    expect(view.result.current.typing).toBe(false);

    act(() => socket.raise("signal", typingFrom("visitor", "call-1", true)));
    expect(view.result.current.typing).toBe(true);

    act(() => socket.raise("message.created", message("call-1")));
    expect(view.result.current.typing).toBe(false);
  });

  it("tells the open chat's visitor whether the viewer is typing", async () => {
    const socket = fakeOpen();

    const view = renderHook(() => useInboxSocket({ selectedCallId: "call-1" }), {
      wrapper: socket.wrapper,
    });
    await act(async () => {
      view.result.current.sayTyping(true);
    });

    expect(socket.signals).toEqual([["call:call-1", "typing", { callId: "call-1", on: true }]]);
  });
});
