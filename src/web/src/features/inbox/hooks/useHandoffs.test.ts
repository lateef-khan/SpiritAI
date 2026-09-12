import { renderHook, waitFor } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";

import type { HandoffSummary } from "@/api/types.gen";

import type { Handoff, HandoffsApi, HandoffStatus } from "../api/handoffsApi";

vi.mock("@/api/sdk.gen", () => ({ listHandoffs: vi.fn() }));

const { listHandoffs } = await import("@/api/sdk.gen");
const { countHandoffs, filterHandoffs, useHandoffs } = await import("./useHandoffs");

/**
 * `filterHandoffs` and `countHandoffs`, against a fixed set of rows.
 *
 * Pure functions, tested without a network or a render: the interesting behaviour here is the
 * three-way split by assignee, not anything React does with it.
 */

const MeKey = "user:ada";

function handoff(over: Partial<Handoff> = {}): Handoff {
  return {
    id: 1,
    callId: "call-1",
    status: "waiting",
    askedBy: "Ada",
    reason: null,
    askedAt: new Date("2026-09-10T09:00:00Z"),
    assignee: null,
    claimedAt: null,
    email: null,
    doneAt: null,
    title: null,
    firstLine: null,
    position: null,
    ...over,
  };
}

const Rows: Handoff[] = [
  handoff({
    id: 1,
    assignee: { key: MeKey, name: "Ada" },
    askedAt: new Date("2026-09-10T09:03:00Z"),
  }),
  handoff({ id: 2, assignee: null, askedAt: new Date("2026-09-10T09:00:00Z") }),
  handoff({ id: 3, assignee: null, askedAt: new Date("2026-09-10T09:02:00Z") }),
  handoff({ id: 4, assignee: null, askedAt: new Date("2026-09-10T09:01:00Z") }),
];

describe("countHandoffs", () => {
  it("counts mine, unassigned, and all", () => {
    expect(countHandoffs(Rows, MeKey)).toEqual({ mine: 1, unassigned: 3, all: 4 });
  });
});

describe("filterHandoffs", () => {
  it("keeps only the caller's own rows for mine", () => {
    const mine = filterHandoffs(Rows, "mine", MeKey);

    expect(mine.map((row) => row.id)).toEqual([1]);
  });

  it("keeps only rows with no assignee for unassigned", () => {
    const unassigned = filterHandoffs(Rows, "unassigned", MeKey);

    expect(unassigned.map((row) => row.id)).toEqual([2, 3, 4]);
  });

  it("keeps every row for all", () => {
    expect(filterHandoffs(Rows, "all", MeKey).map((row) => row.id)).toEqual([1, 2, 3, 4]);
  });
});

/** A `HandoffsApi` that answers from a fixed table, keyed by the status it was asked for. */
function fakeApi(byStatus: Partial<Record<HandoffStatus, Handoff[]>>): HandoffsApi {
  return { list: async (status) => byStatus[status] ?? [] };
}

describe("useHandoffs", () => {
  it("concatenates waiting and human for open, oldest ask first", async () => {
    const waiting = handoff({
      id: 1,
      status: "waiting",
      askedAt: new Date("2026-09-10T09:02:00Z"),
    });
    const human = handoff({ id: 2, status: "human", askedAt: new Date("2026-09-10T09:00:00Z") });
    const api = fakeApi({ waiting: [waiting], human: [human] });

    const view = renderHook(() => useHandoffs("open", MeKey, api));

    await waitFor(() => expect(view.result.current.loading).toBe(false));

    expect(view.result.current.rows.map((row) => row.id)).toEqual([2, 1]);
  });

  it("loads done newest doneAt first", async () => {
    const older = handoff({ id: 1, status: "done", doneAt: new Date("2026-09-10T09:00:00Z") });
    const newer = handoff({ id: 2, status: "done", doneAt: new Date("2026-09-10T09:05:00Z") });
    const api = fakeApi({ done: [older, newer] });

    const view = renderHook(() => useHandoffs("done", MeKey, api));

    await waitFor(() => expect(view.result.current.loading).toBe(false));

    expect(view.result.current.rows.map((row) => row.id)).toEqual([2, 1]);
  });

  it("reports the error from a refused request", async () => {
    const api: HandoffsApi = { list: async () => Promise.reject(new Error("nope")) };

    const view = renderHook(() => useHandoffs("open", MeKey, api));

    await waitFor(() => expect(view.result.current.error).not.toBeNull());

    expect(view.result.current.error?.message).toBe("nope");
  });

  it("loads once, not forever, when no api is passed", async () => {
    // A default parameter re-evaluated on every render would hand the load effect a fresh `api`
    // object each time, and since `api` sits in that effect's dependency list, a fresh object
    // refires it. That bug looks exactly like this: `listHandoffs` keeps growing past two calls
    // instead of settling at one call per status.
    const wire: HandoffSummary = {
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
    };
    vi.mocked(listHandoffs).mockResolvedValue({ data: { items: [wire] } } as never);

    const view = renderHook(() => useHandoffs("open", MeKey));

    await waitFor(() => expect(view.result.current.loading).toBe(false));

    expect(listHandoffs).toHaveBeenCalledTimes(2);
  });
});
