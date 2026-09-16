import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, test, vi } from "vitest";

import { getUnit } from "@/api/sdk.gen";
import type { UnitDocument } from "@/api/types.gen";
import { reviveHistory } from "@/lib/history";

import type { Handoff } from "../api/handoffsApi";
import { HandoffContextPanel } from "./HandoffContextPanel";

/**
 * The context rail, against a mocked wire.
 *
 * `@/api/sdk.gen` is mocked the same way `UnitPanel.test.tsx` mocks it: what is worth holding
 * in place here is which section a given transcript and lookup answer paint — the visitor from
 * the handoff, the unit from the transcript's serial, the handoff's own facts behind the fold.
 * The mock is hoisted, so these static imports resolve to it.
 */
vi.mock("@/api/sdk.gen", () => ({ getUnit: vi.fn(), getOrder: vi.fn() }));

afterEach(cleanup);
beforeEach(() => vi.resetAllMocks());

const Serial = "5808881004036047";

function handoff(over: Partial<Handoff> = {}): Handoff {
  return {
    id: 7,
    callId: "call-7",
    status: "waiting",
    askedBy: "bot",
    reason: "Warranty claim. The visitor tried the belt tension steps twice.",
    askedAt: new Date("2026-09-12T12:38:00"),
    assignee: null,
    claimedAt: null,
    email: "lorrie@northwind.example",
    doneAt: null,
    title: "Treadmill belt slips at 8 mph",
    firstLine: "Hi, my CT800 belt slips when I go above 8 mph.",
    position: 1,
    ...over,
  };
}

function historyWith(text: string) {
  return reviveHistory({
    headId: "call-7:0",
    messages: [
      {
        parentId: null,
        message: {
          id: "call-7:0",
          role: "user",
          content: [{ type: "text", text }],
          attachments: [],
          createdAt: "2026-09-12T12:31:00",
          metadata: { custom: {} },
        },
      },
    ],
  });
}

const unit: UnitDocument = {
  serial: Serial,
  header: {
    serial: Serial,
    modelNo: "580888",
    modelVersion: 2,
    modelName: "SOLE WF80 2010",
    category: "TREADMILL",
    isSole: true,
    manufacturedOn: "04/2010",
    purchasedOn: "2010-10-23T00:00:00+00:00",
    setUpOn: null,
  },
  jobs: [],
  history: [],
  parts: [],
  warranty: [],
  unavailable: [],
};

describe("HandoffContextPanel", () => {
  test("shows the visitor, the unit the transcript names, and the folded handoff", async () => {
    vi.mocked(getUnit).mockResolvedValue({ data: unit } as never);

    render(
      <HandoffContextPanel handoff={handoff()} history={historyWith(`My serial is ${Serial}.`)} />,
    );

    expect(screen.getByText("lorrie@northwind.example")).toBeTruthy();
    expect(await screen.findByText("SOLE WF80 2010")).toBeTruthy();
    expect(screen.getByRole("button", { name: "Handoff · Waiting · #1 in line" })).toBeTruthy();

    fireEvent.click(screen.getByRole("button", { name: "Handoff · Waiting · #1 in line" }));
    expect(
      await screen.findByText("Warranty claim. The visitor tried the belt tension steps twice."),
    ).toBeTruthy();
  });

  test("shows the visitor with an empty unit section when no number is named", () => {
    render(<HandoffContextPanel handoff={handoff()} history={historyWith("The belt slips.")} />);

    expect(screen.getByText("lorrie@northwind.example")).toBeTruthy();
    expect(screen.getByText("No information yet")).toBeTruthy();
    expect(getUnit).not.toHaveBeenCalled();
  });
});
