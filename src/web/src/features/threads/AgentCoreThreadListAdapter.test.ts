import assert from "node:assert/strict";
import { describe, it } from "vitest";
import { AssistantMessageStream, type AssistantStream } from "assistant-stream";
import { createAgentCoreThreadListAdapter } from "./AgentCoreThreadListAdapter.ts";
import type { ThreadsApi, WireThread } from "./threadsApi.ts";

/**
 * One thread as the host writes it.
 *
 * Every field is present because the host's record has every field; the ones nobody set come over
 * as null rather than missing. A fixture that left them out would be testing a shape the wire
 * cannot produce.
 */
function wireThread(fields: Partial<WireThread> & { remoteId: string }): WireThread {
  return {
    status: "regular",
    externalId: null,
    title: null,
    lastMessageAt: null,
    custom: null,
    ...fields,
  };
}

/** A stand-in for the host, recording what the adapter asked of it. */
function fakeApi(overrides: Partial<ThreadsApi> = {}) {
  const patches: { remoteId: string; body: Record<string, unknown> }[] = [];
  const removed: string[] = [];

  const api: ThreadsApi = {
    list: async () => ({ threads: [], nextCursor: null }),
    create: async () => ({ remoteId: "call-new", externalId: null }),
    fetch: async () => wireThread({ remoteId: "call-1" }),
    patch: async (remoteId, body) => void patches.push({ remoteId, body }),
    remove: async (remoteId) => void removed.push(remoteId),
    history: async () => ({ messages: [] }),
    title: async function* () {},
    ...overrides,
  };

  return { api, patches, removed };
}

describe("createAgentCoreThreadListAdapter", () => {
  it("reads a thread's last activity as a date", async () => {
    const { api } = fakeApi({
      list: async () => ({
        threads: [
          wireThread({
            remoteId: "call-1",
            title: "Belt slips",
            lastMessageAt: "2026-08-31T09:00:00Z",
          }),
        ],
        nextCursor: null,
      }),
    });

    const page = await createAgentCoreThreadListAdapter(api).list();

    assert.ok(page.threads[0]!.lastMessageAt instanceof Date);
    assert.equal(page.threads[0]!.title, "Belt slips");
  });

  it("drops a null cursor rather than passing it on", async () => {
    const { api } = fakeApi();

    const page = await createAgentCoreThreadListAdapter(api).list();

    // assistant-ui reads any non-undefined cursor as "there is more", and asks again forever.
    assert.equal(page.nextCursor, undefined);
  });

  it("takes the id the host minted rather than the one assistant-ui guessed", async () => {
    const { api } = fakeApi();

    const made = await createAgentCoreThreadListAdapter(api).initialize("__LOCALID_abc");

    assert.equal(made.remoteId, "call-new");
  });

  it("renames a thread", async () => {
    const { api, patches } = fakeApi();

    await createAgentCoreThreadListAdapter(api).rename("call-1", "Belt slips");

    assert.deepEqual(patches, [{ remoteId: "call-1", body: { title: "Belt slips" } }]);
  });

  it("archives and unarchives by status", async () => {
    const { api, patches } = fakeApi();
    const adapter = createAgentCoreThreadListAdapter(api);

    await adapter.archive("call-1");
    await adapter.unarchive("call-1");

    assert.deepEqual(
      patches.map((p) => p.body),
      [{ status: "archived" }, { status: "regular" }],
    );
  });

  it("clears custom fields with an explicit null", async () => {
    const { api, patches } = fakeApi();

    await createAgentCoreThreadListAdapter(api).updateCustom?.("call-1", undefined);

    // An omitted key means "leave it alone" to the host, which is not what clearing means.
    assert.deepEqual(patches[0]!.body, { custom: null });
  });

  it("deletes a thread", async () => {
    const { api, removed } = fakeApi();

    await createAgentCoreThreadListAdapter(api).delete("call-1");

    assert.deepEqual(removed, ["call-1"]);
  });

  it("streams the title the host writes, piece by piece", async () => {
    const { api, patches } = fakeApi({
      title: async function* () {
        yield "Belt";
        yield " slips";
      },
    });

    const stream = await createAgentCoreThreadListAdapter(api).generateTitle("call-1", [] as never);

    assert.equal(await textOf(stream), "Belt slips");

    // The host renamed the row as it generated. A PATCH here would be a second write of one title.
    assert.deepEqual(patches, []);
  });

  it("sends the words the caller has already typed", async () => {
    const sent: unknown[] = [];
    const { api } = fakeApi({
      title: async function* (_remoteId, messages) {
        sent.push(messages);
      },
    });

    await createAgentCoreThreadListAdapter(api).generateTitle("call-1", [
      {
        role: "user",
        content: [{ type: "text", text: "the belt keeps slipping" }],
        attachments: [],
      },
    ] as never);

    assert.deepEqual(sent, [[{ role: "user", content: "the belt keeps slipping" }]]);
  });

  it("leaves out the parts of a message that are not words", async () => {
    const sent: unknown[] = [];
    const { api } = fakeApi({
      title: async function* (_remoteId, messages) {
        sent.push(messages);
      },
    });

    await createAgentCoreThreadListAdapter(api).generateTitle("call-1", [
      {
        role: "assistant",
        content: [
          { type: "tool-call", toolName: "search", args: {} },
          { type: "text", text: "Try the tension bolt." },
        ],
      },
    ] as never);

    assert.deepEqual(sent, [[{ role: "assistant", content: "Try the tension bolt." }]]);
  });

  it("asks the host to title the thread the caller opened", async () => {
    const asked: string[] = [];
    const { api } = fakeApi({
      title: async function* (remoteId) {
        asked.push(remoteId);
        yield "Belt slips";
      },
    });

    await createAgentCoreThreadListAdapter(api).generateTitle("call-7", [] as never);

    assert.deepEqual(asked, ["call-7"]);
  });
});

/**
 * Reads the title out of a stream the way assistant-ui reads it.
 *
 * Deliberately the same two lines `RemoteThreadListThreadListRuntimeCore` uses, so a stream that
 * passes here is one the sidebar can actually draw.
 */
async function textOf(stream: AssistantStream): Promise<string> {
  let text = "";

  for await (const message of AssistantMessageStream.fromAssistantStream(stream)) {
    text = message.parts.filter((part) => part.type === "text")[0]?.text ?? text;
  }

  return text;
}
