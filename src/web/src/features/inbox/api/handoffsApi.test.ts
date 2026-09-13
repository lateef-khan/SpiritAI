import { beforeEach, describe, expect, it, vi } from "vitest";

import type { HandoffSummary } from "@/api/types.gen";

/**
 * The handoffs api, one test per wire quirk.
 *
 * The generated client is mocked rather than the network under it, the same way
 * `UnitPanel.test.tsx` mocks `@/api/sdk.gen`: what is worth holding in place here is that three
 * wire strings become dates (or stay null), and that the status a caller asks for is the status
 * that reaches the query.
 */
vi.mock("@/api/sdk.gen", () => ({
  listHandoffs: vi.fn(),
  getHandoffMessages: vi.fn(),
  claimHandoff: vi.fn(),
  finishHandoff: vi.fn(),
  replyToHandoff: vi.fn(),
}));

const { listHandoffs, getHandoffMessages, claimHandoff, finishHandoff, replyToHandoff } =
  await import("@/api/sdk.gen");
const { createHandoffsApi, callerKeyOf } = await import("./handoffsApi");

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
  ...over,
});

describe("createHandoffsApi", () => {
  it("turns askedAt into a Date equal to the wire string", async () => {
    vi.mocked(listHandoffs).mockResolvedValue({
      data: { items: [wire({ askedAt: "2026-09-10T09:00:00Z" })] },
    } as never);

    const rows = await createHandoffsApi().list("waiting");

    expect(rows[0]!.askedAt).toBeInstanceOf(Date);
    expect(rows[0]!.askedAt.toISOString()).toBe(new Date("2026-09-10T09:00:00Z").toISOString());
  });

  it("keeps a null claimedAt null", async () => {
    vi.mocked(listHandoffs).mockResolvedValue({
      data: { items: [wire({ claimedAt: null })] },
    } as never);

    const rows = await createHandoffsApi().list("waiting");

    expect(rows[0]!.claimedAt).toBeNull();
  });

  it("turns a claimedAt string into a Date", async () => {
    vi.mocked(listHandoffs).mockResolvedValue({
      data: { items: [wire({ claimedAt: "2026-09-10T09:05:00Z" })] },
    } as never);

    const rows = await createHandoffsApi().list("waiting");

    expect(rows[0]!.claimedAt).toBeInstanceOf(Date);
  });

  it("passes the status through as the query", async () => {
    vi.mocked(listHandoffs).mockResolvedValue({ data: { items: [] } } as never);

    await createHandoffsApi().list("done");

    expect(listHandoffs).toHaveBeenCalledWith(
      expect.objectContaining({ query: { status: "done" } }),
    );
  });
});

describe("createHandoffsApi messages", () => {
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

    const history = await createHandoffsApi().messages("call-1");
    const revived = history.messages[0]!.message;

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
      expect.objectContaining({ path: { callId: "call-1" } }),
    );
  });
});

describe("createHandoffsApi reply", () => {
  it("calls replyToHandoff with the callId and text", async () => {
    vi.mocked(replyToHandoff).mockResolvedValue({ data: undefined } as never);

    await createHandoffsApi().reply("call-1", "Hi from Dana");

    expect(replyToHandoff).toHaveBeenCalledWith(
      expect.objectContaining({ path: { callId: "call-1" }, body: { text: "Hi from Dana" } }),
    );
  });
});

describe("callerKeyOf", () => {
  it("prefixes a Neon user id with user:", () => {
    expect(callerKeyOf("abc-123")).toBe("user:abc-123");
  });
});
