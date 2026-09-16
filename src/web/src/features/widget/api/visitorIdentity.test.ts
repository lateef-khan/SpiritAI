import { beforeEach, describe, expect, it, vi } from "vitest";

import { readVisitorMemory, rememberCall, VisitorHeader, visitorFetch } from "./visitorIdentity";

/**
 * The visitor's identity, one test per promise the host relies on.
 *
 * The key's shape is the server's rule (`VisitorPrincipal.IsWellFormed`): letters, digits, `_`
 * and `-`, at most 128 characters. A key the host refuses would turn every public request into
 * a 400 with nothing in any log, so the rule is held here.
 */
const WellFormed = /^[A-Za-z0-9_-]{1,128}$/;

beforeEach(() => localStorage.clear());

describe("readVisitorMemory", () => {
  it("mints a key the host accepts and answers the same key on the next read", () => {
    const first = readVisitorMemory();
    const second = readVisitorMemory();

    expect(first.key).toMatch(WellFormed);
    expect(second.key).toBe(first.key);
    expect(first.callId).toBeNull();
  });
});

describe("rememberCall", () => {
  it("keeps the call beside the key, and forgets it on null", () => {
    rememberCall("call-1");
    expect(readVisitorMemory().callId).toBe("call-1");

    rememberCall(null);
    expect(readVisitorMemory().callId).toBeNull();
  });
});

describe("visitorFetch", () => {
  it("sends the key on a request that had no headers", async () => {
    const send = vi.fn(async () => new Response());
    const memory = () => ({ key: "abc123", callId: null });

    await visitorFetch(memory, send)("/v1/public/threads", { method: "POST" });

    const [, init] = send.mock.calls[0] as unknown as [string, RequestInit];
    expect(new Headers(init.headers).get(VisitorHeader)).toBe("abc123");
    expect(init.method).toBe("POST");
  });

  it("keeps the headers the request already had", async () => {
    const send = vi.fn(async () => new Response());
    const memory = () => ({ key: "abc123", callId: null });

    await visitorFetch(memory, send)("/v1/public/responses", {
      headers: { "Content-Type": "application/json" },
    });

    const [, init] = send.mock.calls[0] as unknown as [string, RequestInit];
    const headers = new Headers(init.headers);
    expect(headers.get("Content-Type")).toBe("application/json");
    expect(headers.get(VisitorHeader)).toBe("abc123");
  });

  it("keeps the headers of a built Request", async () => {
    // The generated client sends a `Request` and no init. Its content type must survive.
    const send = vi.fn(async () => new Response());
    const memory = () => ({ key: "abc123", callId: null });
    const request = new Request("http://host/v1/public/handoff/c1/email", {
      method: "PATCH",
      headers: { "Content-Type": "application/json" },
      body: "{}",
    });

    await visitorFetch(memory, send)(request);

    const [, init] = send.mock.calls[0] as unknown as [Request, RequestInit];
    const headers = new Headers(init.headers);
    expect(headers.get("Content-Type")).toBe("application/json");
    expect(headers.get(VisitorHeader)).toBe("abc123");
  });
});
