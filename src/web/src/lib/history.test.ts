import assert from "node:assert/strict";
import { describe, it } from "vitest";
import { reviveHistory } from "./history.ts";

describe("reviveHistory", () => {
  it("turns the wire's dates back into dates", () => {
    const history = reviveHistory({
      headId: "call-1:0",
      messages: [
        {
          parentId: null,
          message: {
            id: "call-1:0",
            role: "user",
            content: [{ type: "text", text: "hello" }],
            createdAt: "2026-08-31T09:00:00Z",
            metadata: { custom: {} },
          },
        },
      ],
    });

    // JSON has no date type, and assistant-ui refuses a message whose createdAt is a string.
    assert.ok(history.messages[0]!.message.createdAt instanceof Date);
  });

  it("answers with an empty conversation when the host sends none", () => {
    assert.deepEqual(reviveHistory({ headId: null, messages: [] }).messages, []);
  });
});
