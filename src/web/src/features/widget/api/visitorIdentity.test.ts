import { beforeEach, describe, expect, it, vi } from "vitest";

import {
  readVisitorKey,
  shareVisitorKey,
  VisitorHeader,
  visitorFetch,
} from "./visitorIdentity";

/**
 * The visitor's identity, one test per promise the host relies on.
 *
 * The key's shape is the server's rule (`VisitorPrincipal.IsWellFormed`): letters, digits, `_`
 * and `-`, at most 128 characters. A key the host refuses would turn every public request into
 * a 400 with nothing in any log, so the rule is held here.
 */
const WellFormed = /^[A-Za-z0-9_-]{1,128}$/;

beforeEach(() => {
  localStorage.clear();
});

describe("readVisitorKey", () => {
  it("mints a key the host accepts and answers the same key on the next read", () => {
    const first = readVisitorKey();
    const second = readVisitorKey();

    expect(first).toMatch(WellFormed);
    expect(second).toBe(first);
  });

  it("takes the key the embedding page hands in over the frame's own copy, and keeps it", () => {
    localStorage.setItem("spirit.visitor", "frameKey");

    expect(readVisitorKey(localStorage, "#visitor=pageKey")).toBe("pageKey");
    expect(readVisitorKey(localStorage, "")).toBe("pageKey");
  });

  it("keeps the frame's own key when the page hands in none", () => {
    localStorage.setItem("spirit.visitor", "frameKey");

    expect(readVisitorKey(localStorage, "")).toBe("frameKey");
  });

  it("ignores a handed-in key the host would refuse", () => {
    localStorage.setItem("spirit.visitor", "frameKey");

    expect(readVisitorKey(localStorage, "#visitor=a%20b")).toBe("frameKey");
  });
});

describe("shareVisitorKey", () => {
  it("hands the key to the embedding page", () => {
    const parent = { postMessage: vi.fn() };

    shareVisitorKey("abc123", parent as unknown as Window);

    expect(parent.postMessage).toHaveBeenCalledWith(
      { source: "agentcore-widget", type: "visitor", key: "abc123" },
      "*",
    );
  });
});

describe("visitorFetch", () => {
  it("sends the key on a request that had no headers", async () => {
    const send = vi.fn(async () => new Response());
    const key = () => "abc123";

    await visitorFetch(key, send)("/v1/public/threads", { method: "POST" });

    const [, init] = send.mock.calls[0] as unknown as [string, RequestInit];
    expect(new Headers(init.headers).get(VisitorHeader)).toBe("abc123");
    expect(init.method).toBe("POST");
  });

  it("keeps the headers the request already had", async () => {
    const send = vi.fn(async () => new Response());
    const key = () => "abc123";

    await visitorFetch(key, send)("/v1/public/responses", {
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
    const key = () => "abc123";
    const request = new Request("http://host/v1/public/handoff/c1/email", {
      method: "PATCH",
      headers: { "Content-Type": "application/json" },
      body: "{}",
    });

    await visitorFetch(key, send)(request);

    const [, init] = send.mock.calls[0] as unknown as [Request, RequestInit];
    const headers = new Headers(init.headers);
    expect(headers.get("Content-Type")).toBe("application/json");
    expect(headers.get(VisitorHeader)).toBe("abc123");
  });
});
