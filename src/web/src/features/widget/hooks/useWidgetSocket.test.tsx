import { act, renderHook } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import type { Socket, SocketAuth } from "@/lib/realtime/socket";
import { rememberCall } from "../api/visitorIdentity";
import type { HandoffState, WireHandoffMessage } from "../api/widgetApi";
import type { HandoffDesk } from "./useHandoffDesk";
import type { useWidgetRuntime } from "./useWidgetRuntime";
import { TypingFadeMs } from "@/features/handoff/useTypingIndicator";
import { useWidgetSocket } from "./useWidgetSocket";

/**
 * The widget's socket hook, one test per rule: when the socket is open, what an open reads,
 * where each push lands, and typing both ways.
 */

const waiting: HandoffState = {
  status: "waiting",
  assigneeName: null,
  staffOnline: true,
  email: null,
};

/** A desk that records what was applied and read. */
function fakeDesk(state: HandoffState) {
  const desk = {
    state,
    applied: [] as Partial<HandoffState>[],
    refreshed: 0,
    refresh: async () => {
      desk.refreshed += 1;
      return desk.state;
    },
    leaveEmail: async () => {},
    apply: (change: Partial<HandoffState>) => {
      desk.applied.push(change);
      desk.state = { ...desk.state, ...change };
    },
  };
  return desk satisfies HandoffDesk;
}

/** A store that records what was received and reloaded. */
function fakeWidget(callId: string | null) {
  const widget = {
    runtime: null as never,
    callId,
    received: [] as WireHandoffMessage[],
    reloaded: 0,
    receive: (message: WireHandoffMessage) => {
      widget.received.push(message);
    },
    reload: async () => {
      widget.reloaded += 1;
    },
  };
  return widget satisfies ReturnType<typeof useWidgetRuntime>;
}

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

const reply: WireHandoffMessage = {
  callId: "call-1",
  messageId: "m-1",
  role: "assistant",
  text: "Dana here.",
  speaker: { kind: "human", name: "Dana R.", detail: null },
  at: "2026-09-16T09:00:00Z",
};

beforeEach(() => localStorage.clear());

describe("useWidgetSocket", () => {
  it("stays closed with the bot, and opens for the chat once a person is asked for", () => {
    rememberCall("call-1");
    const socket = fakeOpen();
    const desk = fakeDesk({ ...waiting, status: "bot" });
    const widget = fakeWidget("call-1");

    const view = renderHook(({ d }) => useWidgetSocket({ desk: d, widget, open: socket.open }), {
      initialProps: { d: desk as HandoffDesk },
    });
    expect(socket.opened).toEqual([]);

    view.rerender({ d: { ...desk, state: waiting } });

    expect(socket.opened).toHaveLength(1);
    expect(socket.opened[0]).toMatchObject({ kind: "visitor", callId: "call-1" });
  });

  it("reads the state and the history on every open", () => {
    const socket = fakeOpen();
    const desk = fakeDesk(waiting);
    const widget = fakeWidget("call-1");

    renderHook(() => useWidgetSocket({ desk, widget, open: socket.open }));

    socket.arrive();
    socket.arrive();

    expect(desk.refreshed).toBe(2);
    expect(widget.reloaded).toBe(2);
  });

  it("lands each push on the desk or the store, and tells the screen of a reply", () => {
    const socket = fakeOpen();
    const desk = fakeDesk(waiting);
    const widget = fakeWidget("call-1");
    const told = vi.fn();

    renderHook(() => useWidgetSocket({ desk, widget, onMessage: told, open: socket.open }));

    act(() => {
      socket.raise("presence", { kind: "staff", online: 2 });
      socket.raise("presence", { kind: "visitor", online: 9 });
      socket.raise("handoff.claimed", {
        callId: "call-1",
        assignee: { key: "u1", name: "Dana R." },
      });
      socket.raise("message.created", reply);
      socket.raise("message.created", { ...reply, messageId: "m-2", role: "user" });
    });

    expect(desk.applied).toEqual([
      { staffOnline: true },
      { status: "human", assigneeName: "Dana R." },
    ]);
    expect(widget.received.map((m) => m.messageId)).toEqual(["m-1", "m-2"]);
    expect(told).toHaveBeenCalledTimes(1);
    expect(told).toHaveBeenCalledWith(reply);
  });

  it("closes once the chat is done", () => {
    const socket = fakeOpen();
    const desk = fakeDesk(waiting);
    const widget = fakeWidget("call-1");

    const view = renderHook(({ d }) => useWidgetSocket({ desk: d, widget, open: socket.open }), {
      initialProps: { d: desk as HandoffDesk },
    });
    expect(socket.opened).toHaveLength(1);

    view.rerender({ d: { ...desk, state: { ...waiting, status: "done" } } });

    expect(socket.closed()).toBe(1);
    expect(socket.opened).toHaveLength(1);
  });

  it("shows staff typing until they stop, a reply lands, or it fades", () => {
    vi.useFakeTimers();
    try {
      const socket = fakeOpen();
      const desk = fakeDesk({ ...waiting, status: "human", assigneeName: "Dana R." });
      const widget = fakeWidget("call-1");

      const view = renderHook(() => useWidgetSocket({ desk, widget, open: socket.open }));
      const typing = (on: boolean, kind = "staff") =>
        socket.raise("signal", {
          sender: { key: "u1", kind },
          group: "call:call-1",
          name: "typing",
          payload: { callId: "call-1", on },
        });

      act(() => typing(true, "visitor"));
      expect(view.result.current.typing).toBe(false);

      act(() => typing(true));
      expect(view.result.current.typing).toBe(true);

      act(() => typing(false));
      expect(view.result.current.typing).toBe(false);

      act(() => typing(true));
      act(() => socket.raise("message.created", reply));
      expect(view.result.current.typing).toBe(false);

      act(() => typing(true));
      act(() => vi.advanceTimersByTime(TypingFadeMs + 1));
      expect(view.result.current.typing).toBe(false);
    } finally {
      vi.useRealTimers();
    }
  });

  it("tells staff whether the visitor is typing", async () => {
    const socket = fakeOpen();
    const desk = fakeDesk(waiting);
    const widget = fakeWidget("call-1");

    const view = renderHook(() => useWidgetSocket({ desk, widget, open: socket.open }));
    await act(async () => {
      view.result.current.sayTyping(true);
    });

    expect(socket.signals).toEqual([["handoff:staff", "typing", { callId: "call-1", on: true }]]);
  });
});
