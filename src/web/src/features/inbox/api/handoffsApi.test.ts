import { beforeEach, describe, expect, it, vi } from "vitest";

import type { HandoffSummary } from "@/api/types.gen";

/**
 * The handoffs api, one test per wire quirk.
 *
 * The generated client is mocked rather than the network under it, the same way
 * `UnitPanel.test.tsx` mocks `@/api/sdk.gen`: what is worth holding in place here is that three
 * wire strings become dates (or stay null), and that the filter a caller asks for is the query
 * that reaches the wire.
 */
vi.mock("@/api/sdk.gen", () => ({
  listHandoffs: vi.fn(),
  countHandoffs: vi.fn(),
  getHandoffMessages: vi.fn(),
  claimHandoff: vi.fn(),
  finishHandoff: vi.fn(),
  replyToHandoff: vi.fn(),
}));

const { listHandoffs, getHandoffMessages, claimHandoff, finishHandoff, replyToHandoff } =
  await import("@/api/sdk.gen");
const { createHandoffsApi, callerKeyOf } = await import("./handoffsApi");

const Open = { view: "open", owner: "all", order: "oldest" } as const;

beforeEach(() => vi.resetAllMocks());

const wire = (over: Partial<HandoffSummary> = {}): HandoffSummary => ({
  id: 1,
  callId: "call-1",
  status: "waiting",
  askedBy: "visitor",
  reason: null,
  askedAt: "2026-09-10T09:00:00Z",
  assignee: null,
  claimedAt: null,
  email: null,
  doneAt: null,
  title: null,
  firstLine: null,
  position: null,
  awaitingReply: false,
  unread: false,
  ...over,
});

describe("createHandoffsApi", () => {
  it("turns askedAt into a Date equal to the wire string", async () => {
    vi.mocked(listHandoffs).mockResolvedValue({
      data: { items: [wire({ askedAt: "2026-09-10T09:00:00Z" })], nextCursor: null },
    } as never);

    const { items: rows } = await createHandoffsApi().list(Open, null);

    expect(rows[0]!.askedAt).toBeInstanceOf(Date);
    expect(rows[0]!.askedAt.toISOString()).toBe(new Date("2026-09-10T09:00:00Z").toISOString());
  });

  it("keeps a null claimedAt null", async () => {
    vi.mocked(listHandoffs).mockResolvedValue({
      data: { items: [wire({ claimedAt: null })], nextCursor: null },
    } as never);

    const { items: rows } = await createHandoffsApi().list(Open, null);

    expect(rows[0]!.claimedAt).toBeNull();
  });

  it("turns a claimedAt string into a Date", async () => {
    vi.mocked(listHandoffs).mockResolvedValue({
      data: { items: [wire({ claimedAt: "2026-09-10T09:05:00Z" })], nextCursor: null },
    } as never);

    const { items: rows } = await createHandoffsApi().list(Open, null);

    expect(rows[0]!.claimedAt).toBeInstanceOf(Date);
  });

  it("sends the filter and the cursor as the query, leaving owner out for everyone's rows", async () => {
    vi.mocked(listHandoffs).mockResolvedValue({ data: { items: [], nextCursor: null } } as never);

    await createHandoffsApi().list({ view: "done", owner: "all", order: "newest" }, "abc");

    expect(listHandoffs).toHaveBeenCalledWith(
      expect.objectContaining({ query: { view: "done", order: "newest", cursor: "abc" } }),
    );
  });

  it("names the owner when the filter is narrower than everyone", async () => {
    vi.mocked(listHandoffs).mockResolvedValue({ data: { items: [], nextCursor: null } } as never);

    await createHandoffsApi().list({ view: "open", owner: "me", order: "oldest" }, null);

    expect(listHandoffs).toHaveBeenCalledWith(
      expect.objectContaining({ query: { view: "open", owner: "me", order: "oldest" } }),
    );
  });

  it("hands the next cursor back beside the page", async () => {
    vi.mocked(listHandoffs).mockResolvedValue({
      data: { items: [wire()], nextCursor: "after-1" },
    } as never);

    const page = await createHandoffsApi().list(Open, null);

    expect(page.nextCursor).toBe("after-1");
  });
});

describe("createHandoffsApi history", () => {
  it("asks for the newest page with no cursor", async () => {
    vi.mocked(getHandoffMessages).mockResolvedValue({
      data: { headId: null, messages: [] },
    } as never);

    const page = await createHandoffsApi().history("call-1");

    expect(vi.mocked(getHandoffMessages).mock.calls[0]?.[0]).toMatchObject({
      path: { conversationId: "call-1" },
      query: {},
    });
    expect(page.nextCursor).toBeNull();
  });

  it("carries a page's before cursor and limit to the host, and its nextCursor back", async () => {
    vi.mocked(getHandoffMessages).mockResolvedValue({
      data: { headId: null, messages: [], nextCursor: "90" },
    } as never);

    const page = await createHandoffsApi().history("call-1", "120", 50);

    expect(vi.mocked(getHandoffMessages).mock.calls[0]?.[0]).toMatchObject({
      path: { conversationId: "call-1" },
      query: { before: "120", limit: 50 },
    });
    expect(page.nextCursor).toBe("90");
  });

  it("turns a message's createdAt into a Date, keeping its id, role, and text", async () => {
    vi.mocked(getHandoffMessages).mockResolvedValue({
      data: {
        headId: "call-1:0",
        messages: [
          {
            parentId: null,
            message: {
              id: "call-1:0",
              role: "assistant",
              content: [{ type: "text", text: "on my way" }],
              createdAt: "2026-09-12T12:31:00",
              metadata: { custom: {} },
            },
          },
        ],
      },
    } as never);

    const { repository } = await createHandoffsApi().history("call-1");
    const revived = repository.messages[0]!.message;

    expect(revived.createdAt).toEqual(new Date("2026-09-12T12:31:00"));
    expect(revived.id).toBe("call-1:0");
    expect(revived.role).toBe("assistant");
    expect(revived.content).toEqual([{ type: "text", text: "on my way" }]);
  });
});

describe("createHandoffsApi claim", () => {
  it("revives the claimed handoff, dates and all", async () => {
    vi.mocked(claimHandoff).mockResolvedValue({
      data: wire({
        status: "human",
        assignee: { key: "user:dana", name: "Dana" },
        claimedAt: "2026-09-12T12:50:00",
      }),
    } as never);

    const handoff = await createHandoffsApi().claim("call-1");

    expect(handoff.status).toBe("human");
    expect(handoff.assignee?.name).toBe("Dana");
    expect(handoff.claimedAt).toEqual(new Date("2026-09-12T12:50:00"));
  });
});

describe("createHandoffsApi finish", () => {
  it("calls finishHandoff with the callId", async () => {
    vi.mocked(finishHandoff).mockResolvedValue({ data: undefined } as never);

    await createHandoffsApi().finish("call-1");

    expect(finishHandoff).toHaveBeenCalledWith(
      expect.objectContaining({ path: { conversationId: "call-1" } }),
    );
  });
});

describe("createHandoffsApi reply", () => {
  it("calls replyToHandoff with the callId and text", async () => {
    vi.mocked(replyToHandoff).mockResolvedValue({ data: undefined } as never);

    await createHandoffsApi().reply("call-1", "Hi from Dana");

    expect(replyToHandoff).toHaveBeenCalledWith(
      expect.objectContaining({
        path: { conversationId: "call-1" },
        body: { text: "Hi from Dana" },
      }),
    );
  });
});

describe("callerKeyOf", () => {
  it("prefixes a Neon user id with user:", () => {
    expect(callerKeyOf("abc-123")).toBe("user:abc-123");
  });
});
