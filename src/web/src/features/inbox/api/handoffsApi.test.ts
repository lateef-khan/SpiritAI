import { cleanup } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import type { HandoffSummary } from "@/api/types.gen";

/**
 * The handoffs api, one test per wire quirk.
 *
 * The generated client is mocked rather than the network under it, the same way
 * `UnitPanel.test.tsx` mocks `@/api/sdk.gen`: what is worth holding in place here is that three
 * wire strings become dates (or stay null), and that the status a caller asks for is the status
 * that reaches the query.
 */
vi.mock("@/api/sdk.gen", () => ({ listHandoffs: vi.fn() }));

const { listHandoffs } = await import("@/api/sdk.gen");
const { createHandoffsApi, callerKeyOf } = await import("./handoffsApi");

afterEach(cleanup);
beforeEach(() => vi.resetAllMocks());

const wire = (over: Partial<HandoffSummary> = {}): HandoffSummary => ({
  id: 1,
  callId: "call-1",
  status: "waiting",
  askedBy: "Ada",
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

describe("callerKeyOf", () => {
  it("prefixes a Neon user id with user:", () => {
    expect(callerKeyOf("abc-123")).toBe("user:abc-123");
  });
});
