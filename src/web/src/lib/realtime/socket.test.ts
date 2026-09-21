import { HubConnectionState } from "@microsoft/signalr";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import { HeartbeatSeconds, openSocket, type HubLike, type SocketAuth } from "./socket";

/**
 * The socket, one test per promise a feature relies on: that an open is reported on the first
 * start and on every reconnect, that the heartbeat runs while the connection is up and stops with
 * it, that an event stops being heard once unsubscribed, and that a signal goes only while up.
 */

/** A hub that reaches no network and lets a test raise its events. */
function fakeHub() {
  const handlers = new Map<string, Set<(...args: unknown[]) => void>>();
  let reconnected: (() => void) | null = null;
  let closed: (() => void) | null = null;

  const hub = {
    state: HubConnectionState.Disconnected as HubConnectionState,
    invoked: [] as unknown[][],
    on: (event: string, handler: (...args: unknown[]) => void) => {
      handlers.set(event, (handlers.get(event) ?? new Set()).add(handler));
    },
    off: (event: string, handler: (...args: unknown[]) => void) => {
      handlers.get(event)?.delete(handler);
    },
    onreconnected: (handler: () => void) => {
      reconnected = handler;
    },
    onclose: (handler: () => void) => {
      closed = handler;
    },
    start: async () => {
      // A real start takes a round trip; nothing is connected the moment it is called.
      await Promise.resolve();
      hub.state = HubConnectionState.Connected;
    },
    stop: async () => {
      hub.state = HubConnectionState.Disconnected;
      closed?.();
    },
    invoke: async (method: string, ...args: unknown[]) => {
      hub.invoked.push([method, ...args]);
      return undefined;
    },
    raise: (event: string, payload?: unknown) => {
      for (const handler of handlers.get(event) ?? []) handler(payload);
    },
    reconnect: () => {
      hub.state = HubConnectionState.Connected;
      reconnected?.();
    },
    drop: () => {
      hub.state = HubConnectionState.Reconnecting;
      closed?.();
    },
  };

  return hub satisfies HubLike;
}

const visitor: SocketAuth = { kind: "visitor", callId: "call-1", visitorKey: "key-1" };

const flush = () => new Promise((resolve) => setTimeout(resolve, 0));

beforeEach(() => vi.useFakeTimers({ shouldAdvanceTime: true }));
afterEach(() => vi.useRealTimers());

describe("openSocket", () => {
  it("connects as who it was told to", () => {
    const hub = fakeHub();
    const connect = vi.fn(() => hub);

    openSocket(visitor, connect);

    expect(connect).toHaveBeenCalledWith(visitor);
  });

  it("hears an event until told to stop", async () => {
    const hub = fakeHub();
    const socket = openSocket(visitor, () => hub);
    await flush();
    const heard: unknown[] = [];

    const stop = socket.on("handoff.done", (payload) => heard.push(payload));
    hub.raise("handoff.done", { callId: "call-1" });
    stop();
    hub.raise("handoff.done", { callId: "call-1" });

    expect(heard).toEqual([{ callId: "call-1" }]);
  });

  it("reports an open on the first start and on every reconnect", async () => {
    const hub = fakeHub();
    const socket = openSocket(visitor, () => hub);
    let opens = 0;
    socket.onOpen(() => (opens += 1));

    await flush();
    expect(opens).toBe(1);

    hub.drop();
    hub.reconnect();
    expect(opens).toBe(2);
  });

  it("beats while connected and rests while down or closed", async () => {
    const hub = fakeHub();
    const socket = openSocket(visitor, () => hub);
    await flush();

    await vi.advanceTimersByTimeAsync(HeartbeatSeconds * 1000 * 2 + 10);
    expect(hub.invoked).toEqual([["Heartbeat"], ["Heartbeat"]]);

    hub.drop();
    await vi.advanceTimersByTimeAsync(HeartbeatSeconds * 1000 * 2);
    expect(hub.invoked).toHaveLength(2);

    hub.reconnect();
    await vi.advanceTimersByTimeAsync(HeartbeatSeconds * 1000 + 10);
    expect(hub.invoked).toHaveLength(3);

    await socket.close();
    await vi.advanceTimersByTimeAsync(HeartbeatSeconds * 1000 * 2);
    expect(hub.invoked).toHaveLength(3);
    expect(hub.state).toBe(HubConnectionState.Disconnected);
  });

  it("signals a group only while connected", async () => {
    const hub = fakeHub();
    const socket = openSocket(visitor, () => hub);

    await socket.signal("handoff:staff", "typing", { callId: "call-1", on: true });
    expect(hub.invoked).toEqual([]);

    await flush();
    await socket.signal("handoff:staff", "typing", { callId: "call-1", on: true });
    expect(hub.invoked).toEqual([
      ["Signal", "handoff:staff", "typing", { callId: "call-1", on: true }],
    ]);
  });
});
