import { describe, expect, it } from "vitest";
import { ChatwootRefusedError, createChatwootClient } from "./client.ts";
import contactCreated from "./payloads/contact_created.json";
import conversationCreated from "./payloads/conversation_created.json";
import conversationMissing from "./payloads/conversation_missing.json";
import conversationsList from "./payloads/conversations_list.json";
import messagePosted from "./payloads/message_posted.json";
import messagesBefore from "./payloads/messages_before.json";
import messagesNewest from "./payloads/messages_newest.json";

/*
 * The paths are the Client API's, as Chatwoot's API reference names them. The bodies were recorded
 * from local Chatwoot 4.18.0 for the key below, whose conversation 27 had 31 messages.
 */

const Settings = {
  chatwootBaseUrl: "http://localhost:53000/",
  chatwootInboxIdentifier: "Rg7gynURvsdzcuMyBJHU5pwu",
};
const Key = "probe95d22c7aa7d8c48aa5698ac0";
const Contact = `http://localhost:53000/public/api/v1/inboxes/Rg7gynURvsdzcuMyBJHU5pwu/contacts/${Key}`;

type Sent = { url: string; method: string; body: unknown };

/** A Chatwoot that answers every request with one recorded body, and remembers what it was asked. */
function replaying(body: unknown, status = 200) {
  const sent: Sent[] = [];
  const client = createChatwootClient(Settings, Key, async (input, init) => {
    sent.push({
      url: String(input),
      method: init?.method ?? "GET",
      body: init?.body ? JSON.parse(String(init.body)) : undefined,
    });
    return new Response(body === "" ? "" : JSON.stringify(body), { status });
  });
  return { client, sent };
}

describe("createChatwootClient", () => {
  it("makes the contact with the visitor key as its source_id, and answers its pubsub token", async () => {
    const { client, sent } = replaying(contactCreated);

    const contact = await client.contact();

    expect(sent).toEqual([
      {
        url: "http://localhost:53000/public/api/v1/inboxes/Rg7gynURvsdzcuMyBJHU5pwu/contacts",
        method: "POST",
        body: { source_id: Key },
      },
    ]);
    expect(contact.source_id).toBe(Key);
    expect(contact.pubsub_token).toBe("77sXgnSogAvPv47snyXLFEgF");
  });

  it("lists the key's conversations", async () => {
    const { client, sent } = replaying(conversationsList);

    const conversations = await client.conversations();

    expect(sent).toEqual([{ url: `${Contact}/conversations`, method: "GET", body: undefined }]);
    expect(conversations.map((c) => [c.id, c.uuid])).toEqual([
      [27, "f1d95917-bc28-4e1c-b4ef-2587df3a0113"],
    ]);
  });

  it("makes a conversation, which starts pending", async () => {
    const { client, sent } = replaying(conversationCreated);

    const conversation = await client.createConversation();

    expect(sent).toEqual([{ url: `${Contact}/conversations`, method: "POST", body: {} }]);
    expect(conversation).toMatchObject({
      id: 27,
      uuid: "f1d95917-bc28-4e1c-b4ef-2587df3a0113",
      status: "pending",
    });
  });

  it("posts the visitor's words and answers the message id", async () => {
    const { client, sent } = replaying(messagePosted);

    const message = await client.post(27, "Do you have the XT485 in stock?");

    expect(sent).toEqual([
      {
        url: `${Contact}/conversations/27/messages`,
        method: "POST",
        body: { content: "Do you have the XT485 in stock?" },
      },
    ]);
    expect(message).toMatchObject({ id: 180, message_type: 0, sender: { type: "contact" } });
  });

  it("reads the newest page, oldest first", async () => {
    const { client, sent } = replaying(messagesNewest);

    const page = await client.messages(27);

    expect(sent).toEqual([
      { url: `${Contact}/conversations/27/messages`, method: "GET", body: undefined },
    ]);
    expect(page.map((m) => m.id)).toEqual(Array.from({ length: 20 }, (_, i) => 189 + i));
  });

  it("reads the page before a message id", async () => {
    const { client, sent } = replaying(messagesBefore);

    const page = await client.messages(27, 189);

    expect(sent).toEqual([
      { url: `${Contact}/conversations/27/messages?before=189`, method: "GET", body: undefined },
    ]);
    // 182, 183, and 185 are Chatwoot's activity messages, which it never sends the visitor.
    expect(page.map((m) => [m.id, m.sender?.type])).toEqual([
      [180, "contact"],
      [181, "agent_bot"],
      [184, "user"],
      [186, "contact"],
      [187, "contact"],
      [188, "contact"],
    ]);
  });

  it("turns the visitor's typing on and off", async () => {
    const { client, sent } = replaying("");

    await client.typing(27, true);
    await client.typing(27, false);

    expect(sent).toEqual([
      {
        url: `${Contact}/conversations/27/toggle_typing`,
        method: "POST",
        body: { typing_status: "on" },
      },
      {
        url: `${Contact}/conversations/27/toggle_typing`,
        method: "POST",
        body: { typing_status: "off" },
      },
    ]);
  });

  it("throws the status when Chatwoot refuses", async () => {
    const { client } = replaying(conversationMissing, 404);

    const refusal = await client.messages(999999).catch((error: unknown) => error);

    expect(refusal).toBeInstanceOf(ChatwootRefusedError);
    expect((refusal as ChatwootRefusedError).status).toBe(404);
  });
});
