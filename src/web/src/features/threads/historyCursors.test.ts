import assert from "node:assert/strict";
import { describe, it } from "vitest";
import {
  getInitialOlderCursor,
  setInitialOlderCursor,
  subscribeInitialOlderCursor,
} from "./historyCursors.ts";

describe("historyCursors", () => {
  it("reads undefined for a thread load() has not recorded yet", () => {
    assert.equal(getInitialOlderCursor("call-never-seen"), undefined);
  });

  it("reads back exactly what load() recorded, including a real cursor", () => {
    setInitialOlderCursor("call-with-more", "cursor-1");

    assert.equal(getInitialOlderCursor("call-with-more"), "cursor-1");
  });

  it("reads back null when the newest page was already the oldest", () => {
    setInitialOlderCursor("call-only-page", null);

    assert.equal(getInitialOlderCursor("call-only-page"), null);
  });

  it("notifies every subscriber when a cursor is recorded", () => {
    const seen: string[] = [];
    const unsubscribe = subscribeInitialOlderCursor(() => seen.push("notified"));

    setInitialOlderCursor("call-notify", "cursor-2");
    unsubscribe();
    setInitialOlderCursor("call-notify", "cursor-3");

    assert.deepEqual(seen, ["notified"]);
  });
});
