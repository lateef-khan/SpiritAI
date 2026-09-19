import { QueryClient } from "@tanstack/react-query";
import { beforeEach, describe, expect, it } from "vitest";

import type { HandoffCounts } from "@/api/types.gen";
import type { MessagePush } from "@/features/handoff/events";
import { handoffOf } from "@/test/handoffs";

import type { Handoff, HandoffFilter } from "../api/handoffsApi";
import {
  applyClaimed,
  applyDone,
  applyMessage,
  applyWaiting,
  findRow,
  type HandoffPages,
} from "./handoffCache";
import { handoffKeys } from "./handoffKeys";

/**
 * Each push, against a cache seeded the way the inbox would leave it: the open listing in its
 * three tabs, and the open counts. What is worth holding in place is what a row reads as after
 * the push, which listing still shows it, and what the counts moved by.
 */

const Me = "user:dana";
const Sam = { key: "user:sam", name: "Sam" };

const All: HandoffFilter = { view: "open", owner: "all", order: "oldest" };
const Nobodys: HandoffFilter = { view: "open", owner: "none", order: "oldest" };
const Mine: HandoffFilter = { view: "open", owner: "me", order: "oldest" };
const Newest: HandoffFilter = { view: "open", owner: "all", order: "newest" };

const first = handoffOf({ id: 1, callId: "call-1", position: 1 });
const second = handoffOf({ id: 2, callId: "call-2", position: 2 });
const third = handoffOf({ id: 3, callId: "call-3", position: 3 });

function pagesOf(items: Handoff[], nextCursor: string | null = null): HandoffPages {
  return { pages: [{ items, nextCursor }], pageParams: [null] };
}

function rowsIn(cache: QueryClient, filter: HandoffFilter): string[] {
  return (
    cache
      .getQueryData<HandoffPages>(handoffKeys.list(filter))
      ?.pages.flatMap((page) => page.items.map((row) => row.callId)) ?? []
  );
}

function countsIn(cache: QueryClient): HandoffCounts | undefined {
  return cache.getQueryData<HandoffCounts>(handoffKeys.counts("open"));
}

let cache: QueryClient;

beforeEach(() => {
  cache = new QueryClient();
  cache.setQueryData(handoffKeys.list(All), pagesOf([first, second, third]));
  cache.setQueryData(handoffKeys.list(Nobodys), pagesOf([first, second, third]));
  cache.setQueryData(handoffKeys.list(Mine), pagesOf([]));
  cache.setQueryData(handoffKeys.counts("open"), {
    mine: 0,
    unassigned: 3,
    all: 3,
    awaitingReply: 0,
  });
});

const Dana = { key: Me, name: "Dana" };

function said(
  callId: string,
  role: "user" | "assistant",
  kind: "human" | "system" | null,
): MessagePush {
  return {
    callId,
    messageId: `${callId}:9`,
    role,
    text: "…",
    speaker:
      kind === null ? null : { kind, name: kind === "human" ? "Dana" : "Spirit", detail: null },
    at: "2026-09-19T09:00:00Z",
  };
}

describe("applyClaimed", () => {
  it("moves the row to its holder, out of the queue, and closes the line up behind it", () => {
    applyClaimed(cache, { callId: "call-2", assignee: Sam }, Me);

    const row = findRow(cache, "call-2");
    expect(row?.status).toBe("human");
    expect(row?.assignee).toEqual(Sam);
    expect(row?.position).toBeNull();
    expect(rowsIn(cache, Nobodys)).toEqual(["call-1", "call-3"]);
    expect(findRow(cache, "call-3")?.position).toBe(2);
    expect(countsIn(cache)).toEqual({ mine: 0, unassigned: 2, all: 3, awaitingReply: 0 });
  });

  it("counts a claim by the caller as theirs, and marks their own listing stale", () => {
    applyClaimed(cache, { callId: "call-1", assignee: Dana }, Me);

    expect(countsIn(cache)).toEqual({ mine: 1, unassigned: 2, all: 3, awaitingReply: 0 });
    expect(cache.getQueryState(handoffKeys.list(Mine))?.isInvalidated).toBe(true);
    expect(cache.getQueryState(handoffKeys.list(All))?.isInvalidated).toBe(false);
  });

  it("does nothing the second time the same claim is heard", () => {
    applyClaimed(cache, { callId: "call-2", assignee: Sam }, Me);
    applyClaimed(cache, { callId: "call-2", assignee: Sam }, Me);

    expect(countsIn(cache)).toEqual({ mine: 0, unassigned: 2, all: 3, awaitingReply: 0 });
  });

  it("marks the open answers stale when it has never seen the row", () => {
    applyClaimed(cache, { callId: "call-9", assignee: Sam }, Me);

    expect(cache.getQueryState(handoffKeys.list(All))?.isInvalidated).toBe(true);
    expect(cache.getQueryState(handoffKeys.counts("open"))?.isInvalidated).toBe(true);
    expect(countsIn(cache)).toEqual({ mine: 0, unassigned: 3, all: 3, awaitingReply: 0 });
  });

  it("puts a chat whose visitor spoke last on the claimant's waiting-for-you count", () => {
    cache.setQueryData(
      handoffKeys.list(All),
      pagesOf([{ ...first, awaitingReply: true }, second, third]),
    );

    applyClaimed(cache, { callId: "call-1", assignee: Dana }, Me);

    expect(countsIn(cache)?.awaitingReply).toBe(1);
  });
});

describe("applyDone", () => {
  it("takes a waiting row out of every open listing and off the queue count", () => {
    applyDone(cache, { callId: "call-1" }, Me);

    expect(rowsIn(cache, All)).toEqual(["call-2", "call-3"]);
    expect(rowsIn(cache, Nobodys)).toEqual(["call-2", "call-3"]);
    expect(findRow(cache, "call-2")?.position).toBe(1);
    expect(countsIn(cache)).toEqual({ mine: 0, unassigned: 2, all: 2, awaitingReply: 0 });
    expect(cache.getQueryState(handoffKeys.list(All))?.isInvalidated).toBe(false);
  });

  it("takes the caller's own row off their counts, the wait included", () => {
    applyClaimed(cache, { callId: "call-2", assignee: Dana }, Me);
    applyMessage(cache, said("call-2", "user", null), Me);
    applyDone(cache, { callId: "call-2" }, Me);

    expect(countsIn(cache)).toEqual({ mine: 0, unassigned: 2, all: 2, awaitingReply: 0 });
  });

  it("marks the open counts stale when it has never seen the row", () => {
    applyDone(cache, { callId: "call-9" }, Me);

    expect(cache.getQueryState(handoffKeys.counts("open"))?.isInvalidated).toBe(true);
    expect(rowsIn(cache, All)).toEqual(["call-1", "call-2", "call-3"]);
  });
});

describe("applyWaiting", () => {
  const fourth = handoffOf({ id: 4, callId: "call-4", position: 4 });

  it("joins the back of an oldest-first listing that has read its last page", () => {
    applyWaiting(cache, fourth);

    expect(rowsIn(cache, All)).toEqual(["call-1", "call-2", "call-3", "call-4"]);
    expect(rowsIn(cache, Nobodys)).toEqual(["call-1", "call-2", "call-3", "call-4"]);
    expect(rowsIn(cache, Mine)).toEqual([]);
    expect(countsIn(cache)).toEqual({ mine: 0, unassigned: 4, all: 4, awaitingReply: 0 });
    expect(cache.getQueryState(handoffKeys.list(All))?.isInvalidated).toBe(false);
  });

  it("joins the front of a newest-first listing", () => {
    cache.setQueryData(handoffKeys.list(Newest), pagesOf([third, second, first], "more"));

    applyWaiting(cache, fourth);

    expect(rowsIn(cache, Newest)).toEqual(["call-4", "call-3", "call-2", "call-1"]);
  });

  it("marks an oldest-first listing stale when its back is still unread", () => {
    cache.setQueryData(handoffKeys.list(All), pagesOf([first, second], "more"));

    applyWaiting(cache, fourth);

    expect(rowsIn(cache, All)).toEqual(["call-1", "call-2"]);
    expect(cache.getQueryState(handoffKeys.list(All))?.isInvalidated).toBe(true);
  });

  it("does nothing the second time the same row is heard", () => {
    applyWaiting(cache, fourth);
    applyWaiting(cache, fourth);

    expect(rowsIn(cache, All)).toEqual(["call-1", "call-2", "call-3", "call-4"]);
    expect(countsIn(cache)).toEqual({ mine: 0, unassigned: 4, all: 4, awaitingReply: 0 });
  });
});

describe("applyMessage", () => {
  beforeEach(() => applyClaimed(cache, { callId: "call-2", assignee: Dana }, Me));

  it("marks the chat waiting when its visitor speaks, and clears it when a person replies", () => {
    applyMessage(cache, said("call-2", "user", null), Me);

    expect(findRow(cache, "call-2")?.awaitingReply).toBe(true);
    expect(countsIn(cache)?.awaitingReply).toBe(1);

    applyMessage(cache, said("call-2", "assistant", "human"), Me);

    expect(findRow(cache, "call-2")?.awaitingReply).toBe(false);
    expect(countsIn(cache)?.awaitingReply).toBe(0);
  });

  it("moves nothing for a note from the host, or for the same speaker twice", () => {
    applyMessage(cache, said("call-2", "user", null), Me);
    applyMessage(cache, said("call-2", "assistant", "system"), Me);
    applyMessage(cache, said("call-2", "user", null), Me);

    expect(findRow(cache, "call-2")?.awaitingReply).toBe(true);
    expect(countsIn(cache)?.awaitingReply).toBe(1);
  });

  it("counts only chats the caller holds", () => {
    applyClaimed(cache, { callId: "call-3", assignee: Sam }, Me);

    applyMessage(cache, said("call-3", "user", null), Me);

    expect(findRow(cache, "call-3")?.awaitingReply).toBe(true);
    expect(countsIn(cache)?.awaitingReply).toBe(0);
  });

  it("marks the transcript stale whoever spoke", () => {
    cache.setQueryData(handoffKeys.messages("call-2"), { messages: [] });

    applyMessage(cache, said("call-2", "assistant", "system"), Me);

    expect(cache.getQueryState(handoffKeys.messages("call-2"))?.isInvalidated).toBe(true);
  });
});
