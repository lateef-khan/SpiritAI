import { cleanup, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";

import type { Handoff } from "../api/handoffsApi";
import { countHandoffs } from "../hooks/useHandoffs";

/**
 * The panel, fed rows directly.
 *
 * The screen owns the load — `useHandoffs` and the wire underneath it are `InboxScreen.test
 * .tsx`'s business. What is worth holding in place here is what a waiting row and a claimed row
 * read as, and what happens with an empty or errored load, once those are already props.
 */

const { InboxPanel } = await import("./InboxPanel");

const MeKey = "user:dana";

function handoff(over: Partial<Handoff> = {}): Handoff {
  return {
    id: 1,
    callId: "call-1",
    status: "waiting",
    askedBy: "bot",
    reason: null,
    askedAt: new Date("2026-09-12T12:38:00"),
    assignee: null,
    claimedAt: null,
    email: "lorrie@northwind.example",
    doneAt: null,
    title: "Treadmill belt slips at 8 mph",
    firstLine: "I already did that twice.",
    position: 1,
    ...over,
  };
}

afterEach(() => {
  cleanup();
  vi.useRealTimers();
});

describe("InboxPanel", () => {
  it("shows a waiting row's wait time and position, a claimed row's assignee, and tab counts", () => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date("2026-09-12T12:52:00"));

    const waiting = handoff();
    const human = handoff({
      id: 2,
      status: "human",
      assignee: { key: MeKey, name: "Dana" },
      email: "priya.n@example.com",
      title: "Bluetooth will not pair on XBR95",
      firstLine: "Try holding the button for 5 seconds.",
      position: null,
    });
    const rows = [waiting, human];

    render(
      <InboxPanel
        meKey={MeKey}
        view="open"
        onViewChange={() => {}}
        rows={rows}
        counts={countHandoffs(rows, MeKey)}
        loading={false}
        error={null}
        selectedId={null}
        onSelect={() => {}}
      />,
    );

    expect(screen.getByText("14 min")).toBeTruthy();
    expect(screen.getByText("lorrie@northwind.example")).toBeTruthy();
    expect(screen.getByText("Dana")).toBeTruthy();

    expect(screen.getByRole("tab", { name: "Mine 1" })).toBeTruthy();
    expect(screen.getByRole("tab", { name: "Unassigned 1" })).toBeTruthy();
    expect(screen.getByRole("tab", { name: "All 2" })).toBeTruthy();
  });

  it("says so when there are no conversations", () => {
    render(
      <InboxPanel
        meKey={MeKey}
        view="open"
        onViewChange={() => {}}
        rows={[]}
        counts={countHandoffs([], MeKey)}
        loading={false}
        error={null}
        selectedId={null}
        onSelect={() => {}}
      />,
    );

    expect(screen.getByText("No conversations.")).toBeTruthy();
  });

  it("reports the error from a refused request", () => {
    render(
      <InboxPanel
        meKey={MeKey}
        view="open"
        onViewChange={() => {}}
        rows={[]}
        counts={countHandoffs([], MeKey)}
        loading={false}
        error={new Error("host refused")}
        selectedId={null}
        onSelect={() => {}}
      />,
    );

    expect(screen.getByText("host refused")).toBeTruthy();
  });
});
