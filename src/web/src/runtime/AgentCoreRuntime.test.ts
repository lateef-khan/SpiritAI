import { describe, expect, it } from "vitest";
import type { CompleteAttachment, ThreadMessage, ThreadUserMessage } from "@assistant-ui/react";
import { flatten, sourceContent } from "./AgentCoreRuntime.ts";
import type { SourcePart } from "./transport.ts";

const document: SourcePart = {
  id: "card-42",
  sourceType: "document",
  title: "Spirit CT900 owner's manual, p.27",
  locator: "p.27",
  url: null,
  mediaType: "text/plain",
  origin: "knowledge",
  callId: "call-1",
};

describe("sourceContent", () => {
  it("maps a document source onto assistant-ui's document variant", () => {
    // A card has no URL, so the document variant is the only honest one: the url variant would
    // draw a link the caller could click, and there is nothing behind it.
    expect(sourceContent(document)).toEqual({
      type: "source",
      sourceType: "document",
      id: "card-42",
      title: "Spirit CT900 owner's manual, p.27",
      mediaType: "text/plain",
      parentId: "call-1",
      providerMetadata: { agentcore: { origin: "knowledge", locator: "p.27" } },
    });
  });

  it("maps a url source onto the url variant", () => {
    const part = sourceContent({
      ...document,
      sourceType: "url",
      url: "https://example.com/support",
      title: "Spirit Fitness support",
      origin: "web-search",
    });

    expect(part).toMatchObject({
      type: "source",
      sourceType: "url",
      url: "https://example.com/support",
      title: "Spirit Fitness support",
    });
  });

  it("falls back to the document variant when a url source arrives with no link", () => {
    // The host promises a link on a url source. A missing one is a host bug, and a url part with an
    // empty href renders a dead chip, so this degrades to the shape that needs no link.
    const part = sourceContent({ ...document, sourceType: "url", url: null });

    expect(part).toMatchObject({ type: "source", sourceType: "document" });
  });
});

const userMessage = (
  content: ThreadUserMessage["content"],
  attachments: ThreadUserMessage["attachments"] = [],
): ThreadMessage => ({
  id: "m-1",
  createdAt: new Date(0),
  role: "user",
  content,
  attachments,
  metadata: { custom: {} },
});

const attachment = (
  name: string,
  type: CompleteAttachment["type"],
  content: CompleteAttachment["content"],
): CompleteAttachment => ({
  id: `att-${name}`,
  type,
  name,
  content,
  status: { type: "complete" },
});

describe("flatten", () => {
  it("sends the typed text of a message that carries nothing else", () => {
    const wire = flatten(userMessage([{ type: "text", text: "how tall is a CT900?" }]));

    expect(wire).toEqual({ role: "user", content: "how tall is a CT900?" });
  });

  it("inlines the text an attachment carries, ahead of the question about it", () => {
    // A text file already arrives as a text part, wrapped in a tag naming the file. Putting it
    // first means the model reads the material before the question asked about it.
    const wire = flatten(
      userMessage(
        [{ type: "text", text: "what is wrong here?" }],
        [
          attachment("log.txt", "document", [
            { type: "text", text: "<attachment name=log.txt>\nboom\n</attachment>" },
          ]),
        ],
      ),
    );

    expect(wire.content).toBe(
      "<attachment name=log.txt>\nboom\n</attachment>\nwhat is wrong here?",
    );
  });

  it("names an attachment that carries no text", () => {
    // An image flattens to nothing, and a turn with no user text at all is a 400. Naming the file
    // keeps the turn sendable and tells the model a file it cannot read was attached.
    const wire = flatten(
      userMessage(
        [],
        [attachment("belt.png", "image", [{ type: "image", image: "data:image/png;base64,AA" }])],
      ),
    );

    expect(wire.content).toBe("[attachment: belt.png]");
  });

  it("leaves an assistant message alone", () => {
    const wire = flatten({
      id: "m-2",
      createdAt: new Date(0),
      role: "assistant",
      content: [{ type: "text", text: "about 84 inches." }],
      status: { type: "complete", reason: "stop" },
      metadata: {
        unstable_state: null,
        unstable_annotations: [],
        unstable_data: [],
        steps: [],
        custom: {},
      },
    });

    expect(wire).toEqual({ role: "assistant", content: "about 84 inches." });
  });
});
