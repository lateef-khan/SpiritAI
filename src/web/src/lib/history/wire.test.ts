import assert from "node:assert/strict";
import { describe, it } from "vitest";
import { reviveHistory } from "./wire.ts";

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

  it("draws a kept picture inline and points the words at it", () => {
    const history = reviveHistory({
      headId: "call-1:1",
      messages: [
        {
          parentId: null,
          message: {
            id: "call-1:1",
            role: "assistant",
            content: [
              { type: "text", text: "See [chart](sandbox:/mnt/data/chart.png)" },
              {
                type: "file",
                name: "chart.png",
                mediaType: "image/png",
                length: 10,
                url: "https://files.test/chart.png?t=1",
              },
            ],
            createdAt: "2026-08-31T09:00:00Z",
            metadata: { custom: {} },
          },
        },
      ],
    });

    assert.deepEqual(history.messages[0]!.message.content, [
      { type: "text", text: "See [chart](https://files.test/chart.png?t=1)" },
      { type: "image", image: "https://files.test/chart.png?t=1", filename: "chart.png" },
    ]);
  });

  it("offers a kept spreadsheet as a download", () => {
    const history = reviveHistory({
      headId: "call-1:1",
      messages: [
        {
          parentId: null,
          message: {
            id: "call-1:1",
            role: "assistant",
            content: [
              {
                type: "file",
                name: "rows.csv",
                mediaType: "text/csv",
                length: 8,
                url: "https://files.test/rows.csv?t=1",
              },
            ],
            createdAt: "2026-08-31T09:00:00Z",
            metadata: { custom: {} },
          },
        },
      ],
    });

    assert.deepEqual(history.messages[0]!.message.content, [
      {
        type: "file",
        data: "https://files.test/rows.csv?t=1",
        mimeType: "text/csv",
        filename: "rows.csv",
        sourceType: "url",
      },
    ]);
  });

  it("answers with an empty conversation when the host sends none", () => {
    assert.deepEqual(reviveHistory({ headId: null, messages: [] }).messages, []);
  });
});
