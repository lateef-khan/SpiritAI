import assert from "node:assert/strict";
import { describe, it } from "vitest";
import type { ExportedMessageRepository } from "@assistant-ui/react";
import { mergeOlderPage } from "./mergeOlderPage.ts";

/** One text message, the shape both a live thread and a fetched page hold it in. */
function item(id: string, parentId: string | null): ExportedMessageRepository["messages"][number] {
  return {
    parentId,
    message: {
      id,
      role: id.startsWith("u") ? "user" : "assistant",
      content: [{ type: "text", text: id }],
      createdAt: new Date(0),
      metadata: { custom: {} },
    },
  } as unknown as ExportedMessageRepository["messages"][number];
}

describe("mergeOlderPage", () => {
  it("hangs the current root off the older page's last message", () => {
    const current: ExportedMessageRepository = {
      headId: "u2",
      messages: [item("u1", null), item("u2", "u1")],
    };
    const older: ExportedMessageRepository = {
      messages: [item("u-3", null), item("u-2", "u-3"), item("u-1", "u-2")],
    };

    const merged = mergeOlderPage(current, older);

    assert.equal(merged.headId, "u2");
    assert.deepEqual(
      merged.messages.map((m) => m.message.id),
      ["u-3", "u-2", "u-1", "u1", "u2"],
    );
    assert.equal(merged.messages.find((m) => m.message.id === "u1")!.parentId, "u-1");
    // The older page's own first item is the new root.
    assert.equal(merged.messages.find((m) => m.message.id === "u-3")!.parentId, null);
    // Everything else in the older page keeps the parent it already had.
    assert.equal(merged.messages.find((m) => m.message.id === "u-2")!.parentId, "u-3");
  });

  it("keeps every parentId that was not the root untouched", () => {
    const current: ExportedMessageRepository = {
      messages: [item("a", null), item("b", "a"), item("c", "b")],
    };
    const older: ExportedMessageRepository = { messages: [item("z", null)] };

    const merged = mergeOlderPage(current, older);

    assert.equal(merged.messages.find((m) => m.message.id === "b")!.parentId, "a");
    assert.equal(merged.messages.find((m) => m.message.id === "c")!.parentId, "b");
  });

  it("returns the repository unchanged when the older page is empty", () => {
    const current: ExportedMessageRepository = { messages: [item("a", null)] };

    const merged = mergeOlderPage(current, { messages: [] });

    assert.deepEqual(merged, current);
  });

  it("returns the repository unchanged when it already has no root to relink", () => {
    // Every item already has a parent — a second merge racing the first would find this.
    const current: ExportedMessageRepository = { messages: [item("a", "already-merged")] };

    const merged = mergeOlderPage(current, { messages: [item("z", null)] });

    assert.deepEqual(merged, current);
  });

  it("omits headId when the current repository never had one", () => {
    const current: ExportedMessageRepository = { messages: [item("a", null)] };

    const merged = mergeOlderPage(current, { messages: [item("z", null)] });

    assert.equal("headId" in merged, false);
  });
});
