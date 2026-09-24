import { adapters } from "@rails/actioncable";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import presenceFrames from "./payloads/presence_frames.json";
import socketFrames from "./payloads/socket_frames.json";
import { openChatwootSocket, type ChatwootSocket } from "./socket.ts";
import type { ChatwootEvents } from "./wire.ts";

/*
 * The frames were recorded from local Chatwoot 4.18.0 on one contact's RoomChannel while: the
 * visitor wrote, the agent bot answered, staff took the chat, typed, replied, and resolved it, and
 * the visitor wrote again. `presence_frames.json` was recorded with a member of staff online.
 * They are replayed through the real `@rails/actioncable` over a stand-in WebSocket.
 */

const Settings = {
  chatwootBaseUrl: "http://localhost:53000",
  chatwootInboxIdentifier: "Rg7gynURvsdzcuMyBJHU5pwu",
};
const PubsubToken = "77sXgnSogAvPv47snyXLFEgF";
const Identifier = '{"channel":"RoomChannel","pubsub_token":"77sXgnSogAvPv47snyXLFEgF"}';
const UpdatePresence = {
  command: "message",
  identifier: Identifier,
  data: '{"action":"update_presence"}',
};

/** A WebSocket that reaches no network: a test opens it, hands it frames, and reads what was sent. */
class FakeSocket {
  static readonly CONNECTING = 0;
  static readonly OPEN = 1;
  static readonly CLOSING = 2;
  static readonly CLOSED = 3;
  static made: FakeSocket[] = [];

  readyState = FakeSocket.CONNECTING;
  protocol = "";
  readonly sent: unknown[] = [];
  onopen: ((event: object) => void) | null = null;
  onmessage: ((event: { data: string }) => void) | null = null;
  onclose: ((event: object) => void) | null = null;
  onerror: ((event: object) => void) | null = null;

  constructor(readonly url: string) {
    FakeSocket.made.push(this);
  }

  send(data: string) {
    this.sent.push(JSON.parse(data));
  }

  close() {
    this.readyState = FakeSocket.CLOSED;
    this.onclose?.({});
  }

  open() {
    this.readyState = FakeSocket.OPEN;
    this.protocol = "actioncable-v1-json";
    this.onopen?.({});
  }

  receive(frame: unknown) {
    this.onmessage?.({ data: JSON.stringify(frame) });
  }
}

const RealWebSocket = adapters.WebSocket;
let socket: ChatwootSocket | null = null;

/** Opens the socket and hands it Chatwoot's `welcome` and `confirm_subscription`. */
function connect(): FakeSocket {
  socket = openChatwootSocket(Settings, PubsubToken);
  const wire = FakeSocket.made.at(-1)!;
  wire.open();
  wire.receive(socketFrames[0]);
  wire.receive(socketFrames[1]);
  return wire;
}

/** Everything one event's handler was given. */
function heard<E extends keyof ChatwootEvents>(event: E): ChatwootEvents[E][] {
  const seen: ChatwootEvents[E][] = [];
  socket!.on(event, (data) => seen.push(data));
  return seen;
}

beforeEach(() => {
  FakeSocket.made = [];
  adapters.WebSocket = FakeSocket as unknown as typeof WebSocket;
});

afterEach(() => {
  socket?.close();
  socket = null;
  adapters.WebSocket = RealWebSocket;
  vi.useRealTimers();
});

describe("openChatwootSocket", () => {
  it("subscribes to RoomChannel on /cable with the contact's pubsub token", () => {
    const wire = connect();

    expect(wire.url).toBe("ws://localhost:53000/cable");
    expect(wire.sent).toContainEqual({ command: "subscribe", identifier: Identifier });
  });

  it("hands each event to its handlers, in the order Chatwoot sent them", () => {
    const wire = connect();
    const messages = heard("message.created");
    const statuses = heard("conversation.status_changed");
    const updates = heard("conversation.updated");
    const typingOn = heard("conversation.typing_on");
    const typingOff = heard("conversation.typing_off");

    for (const frame of socketFrames.slice(2)) wire.receive(frame);

    expect(messages.map((m) => [m.sender?.type, m.content])).toEqual([
      ["contact", "Do you have the XT485 in stock?"],
      ["agent_bot", "Yes, the **XT485** is in stock."],
      ["user", "Hi, this is Matthew. I can help."],
      ["contact", "One more question."],
    ]);
    expect(statuses.map((c) => c.status)).toEqual(["open", "resolved", "pending"]);
    expect(updates.map((c) => [c.status, c.meta.assignee?.type, c.meta.assignee?.name])).toEqual([
      ["pending", "agent_bot", "Spirit AI"],
      ["open", "user", "Matthew Hsu"],
      ["open", "user", "Matthew Hsu"],
      ["resolved", "user", "Matthew Hsu"],
      ["pending", "user", "Matthew Hsu"],
      ["pending", "user", "Matthew Hsu"],
    ]);
    expect(typingOn.map((t) => [t.conversation.id, t.user.name])).toEqual([[27, "Matthew Hsu"]]);
    expect(typingOff.map((t) => [t.conversation.id, t.user.name])).toEqual([[27, "Matthew Hsu"]]);
  });

  it("says the visitor is here on every open and every 20 seconds, and hears who among staff is online", () => {
    vi.useFakeTimers();
    const opened = vi.fn();
    socket = openChatwootSocket(Settings, PubsubToken);
    socket.onOpen(opened);
    const presence = heard("presence.update");
    const wire = FakeSocket.made.at(-1)!;
    wire.open();
    wire.receive(presenceFrames[0]);
    wire.receive(presenceFrames[1]);

    expect(opened).toHaveBeenCalledOnce();
    expect(
      wire.sent.filter((s) => JSON.stringify(s) === JSON.stringify(UpdatePresence)),
    ).toHaveLength(1);

    // Chatwoot pings every 3 seconds; without them ActionCable reads the socket as stale.
    for (let second = 0; second < 20; second += 2) {
      vi.advanceTimersByTime(2000);
      wire.receive({ type: "ping", message: second });
    }
    wire.receive(presenceFrames[2]);

    expect(
      wire.sent.filter((s) => JSON.stringify(s) === JSON.stringify(UpdatePresence)),
    ).toHaveLength(2);
    expect(presence).toEqual([{ account_id: 2, users: { "2": "online" } }]);
  });

  it("hears nothing once closed", () => {
    const wire = connect();
    const messages = heard("message.created");

    socket!.close();
    socket!.close();
    wire.receive(socketFrames[3]);

    expect(wire.sent).toContainEqual({ command: "unsubscribe", identifier: Identifier });
    expect(wire.readyState).toBe(FakeSocket.CLOSED);
    expect(messages).toEqual([]);
  });
});
