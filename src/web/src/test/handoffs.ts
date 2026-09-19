import { vi } from "vitest";

import type { Handoff, HandoffsApi } from "@/features/inbox/api/handoffsApi";

/** A handoffs api that answers nothing, with the parts a test cares about swapped in. */
export function stubHandoffsApi(over: Partial<HandoffsApi> = {}): HandoffsApi {
  return {
    list: vi.fn(),
    counts: vi.fn(),
    messages: vi.fn(),
    claim: vi.fn(),
    finish: vi.fn(),
    reply: vi.fn(),
    ...over,
  };
}

/** One waiting row, first in line, with the parts a test cares about swapped in. */
export function handoffOf(over: Partial<Handoff> = {}): Handoff {
  return {
    id: 1,
    callId: "call-1",
    status: "waiting",
    askedBy: "visitor",
    reason: null,
    askedAt: new Date("2026-09-10T09:00:00Z"),
    assignee: null,
    claimedAt: null,
    email: null,
    doneAt: null,
    title: null,
    firstLine: null,
    position: 1,
    awaitingReply: false,
    ...over,
  };
}
