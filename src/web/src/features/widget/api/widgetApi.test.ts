import { beforeEach, describe, expect, it, vi } from "vitest";

/**
 * The widget's api, one test per wire quirk.
 *
 * The generated client is mocked rather than the network under it, the way
 * `features/inbox/api/handoffsApi.test.ts` does it. What is worth holding in place is that the
 * call id comes off `remoteId`, and that a history's `createdAt` comes back as a `Date`.
 */
vi.mock("@/api/sdk.gen", () => ({
  createPublicThread: vi.fn(),
  getPublicThreadMessages: vi.fn(),
  getHandoffState: vi.fn(),
  leaveEmail: vi.fn(),
  sendVisitorMessage: vi.fn(),
}));

const { createPublicThread, getPublicThreadMessages, getHandoffState, sendVisitorMessage } =
  await import("@/api/sdk.gen");
const { createWidgetApi } = await import("./widgetApi");

const send = async () => new Response();

beforeEach(() => vi.resetAllMocks());

describe("createWidgetApi", () => {
  it("answers the call id the host filed the thread under", async () => {
    vi.mocked(createPublicThread).mockResolvedValue({
      data: { remoteId: "call-9", externalId: null },
    } as never);

    expect(await createWidgetApi(send).createThread()).toBe("call-9");
  });

  it("revives createdAt into a Date", async () => {
    vi.mocked(getPublicThreadMessages).mockResolvedValue({
      data: {
        headId: "m1",
        messages: [
          {
            parentId: null,
            message: {
              id: "m1",
              role: "user",
              content: [{ type: "text", text: "hi" }],
              createdAt: "2026-09-16T09:00:00Z",
              metadata: { custom: {} },
            },
          },
        ],
      },
    } as never);

    const history = await createWidgetApi(send).history("call-9");

    expect(vi.mocked(getPublicThreadMessages).mock.calls[0]?.[0]).toMatchObject({
      path: { callId: "call-9" },
    });
    expect(history.headId).toBe("m1");
    expect(history.messages[0]?.message.createdAt).toEqual(new Date("2026-09-16T09:00:00Z"));
  });

  it("narrows the state's status and keeps the rest", async () => {
    vi.mocked(getHandoffState).mockResolvedValue({
      data: { status: "waiting", position: 2, assigneeName: null, staffOnline: false },
    } as never);

    expect(await createWidgetApi(send).handoffState("call-9")).toEqual({
      status: "waiting",
      position: 2,
      assigneeName: null,
      staffOnline: false,
    });
  });

  it("sends the visitor's words as text and answers the stored message", async () => {
    vi.mocked(sendVisitorMessage).mockResolvedValue({
      data: {
        callId: "call-9",
        messageId: "host-7",
        role: "user",
        text: "still there?",
        speaker: null,
        at: "2026-09-16T09:00:00Z",
      },
    } as never);

    const created = await createWidgetApi(send).say("call-9", "still there?");

    expect(vi.mocked(sendVisitorMessage).mock.calls[0]?.[0]).toMatchObject({
      path: { callId: "call-9" },
      body: { text: "still there?" },
    });
    expect(created.messageId).toBe("host-7");
  });
});
