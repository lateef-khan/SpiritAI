import { cleanup, render, screen } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, test, vi } from "vitest";

import type { OrderDocument, UnitDocument } from "@/api/types.gen";
import { HostRefusedError } from "@/apiClient";
import type { Said } from "@/hooks/useIdentifiers";

import { UnitPanel } from "./UnitPanel";

/**
 * The panel, one test per state.
 *
 * The generated client is mocked rather than the network under it: what is worth holding in place
 * here is which state a given answer puts the panel in, and the client is the thing that turns a
 * status into that answer.
 */
vi.mock("@/api/sdk.gen", () => ({ getUnit: vi.fn(), getOrder: vi.fn() }));

const { getOrder, getUnit } = await import("@/api/sdk.gen");

afterEach(cleanup);
beforeEach(() => vi.resetAllMocks());

const Serial = "5808881004036047";

const said = (text: string): Said[] => [{ role: "user", text }];

const unit = (over: Partial<UnitDocument> = {}): UnitDocument => ({
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
  history: [
    {
      orderNumber: "796955-1",
      serviceId: 796955,
      orderId: 1,
      status: "closed",
      statusText: "CLOSED",
      calledOn: "2025-01-15T15:18:00+00:00",
      servicedOn: null,
      technician: "tiana.bills",
      summary: "Missing Parts",
      solution: null,
    },
  ],
  parts: [{ spNo: "J99A0002", dyacoNo: null, description: "HARDWARE KIT", quantity: 1 }],
  warranty: [
    { category: "Labor", days: 730, expiresOn: "2012-10-22T00:00:00+00:00", isCovered: false },
    { category: "Deck", days: 3650, expiresOn: "2020-10-20T00:00:00+00:00", isCovered: false },
  ],
  unavailable: [],
  ...over,
});

const order: OrderDocument = {
  orderNumber: "796955-1",
  serviceId: 796955,
  orderId: 1,
  orderType: "Warranty",
  orderedOn: "2025-01-15T16:21:18+00:00",
  appointedOn: null,
  shippedOn: null,
  closedOn: null,
  technician: "tiana.bills",
  ispName: null,
  ispStatus: null,
  dealerNo: null,
  trackingNo: "284431256500",
  notes: null,
  lines: [{ partNo: "K140002-Z3", description: "ROLLER, REAR", shipped: 1, isReturned: true }],
};

function panel(turns: Said[]) {
  render(<UnitPanel said={turns} onAsk={() => {}} />);
}

describe("UnitPanel", () => {
  test("stays empty when nobody has pasted a number", () => {
    panel(said("the belt slips"));

    expect(screen.getByText("No information yet")).toBeTruthy();
  });

  test("shows skeletons while the lookup is in flight", () => {
    vi.mocked(getUnit).mockReturnValue(new Promise(() => {}) as never);

    panel(said(Serial));

    expect(document.querySelectorAll('[data-slot="skeleton"]').length).toBeGreaterThan(0);
  });

  test("shows the machine when a serial resolves", async () => {
    vi.mocked(getUnit).mockResolvedValue({ data: unit() } as never);

    panel(said(Serial));

    expect(await screen.findByText("SOLE WF80 2010")).toBeTruthy();

    // Grouped in fours, because this gets read digit by digit off a sticker on a frame.
    expect(screen.getByText("5808 8810 0403 6047")).toBeTruthy();
    expect(screen.getByText("Parts")).toBeTruthy();
  });

  test("shows a work order with no tabs under it", async () => {
    vi.mocked(getOrder).mockResolvedValue({ data: order } as never);

    panel(said("order 796955-1"));

    expect(await screen.findByText("Warranty")).toBeTruthy();
    expect(screen.getByText("K140002-Z3")).toBeTruthy();
    expect(screen.queryByText("History")).toBeNull();
  });

  test("says so when nothing carries the number, and keeps the chip", async () => {
    vi.mocked(getUnit).mockRejectedValue(new HostRefusedError(404, `/v1/units/${Serial}`));

    panel(said(Serial));

    expect(await screen.findByText("No unit with that number")).toBeTruthy();

    // Written in full: a serial gets checked digit by digit against a sticker, and a shortened one
    // cannot be checked at all.
    expect(screen.getByRole("button", { name: Serial })).toBeTruthy();
  });

  test("offers to try again when the host is the problem", async () => {
    vi.mocked(getUnit).mockRejectedValue(new HostRefusedError(500, `/v1/units/${Serial}`));

    panel(said(Serial));

    expect(await screen.findByText("Could not reach the database")).toBeTruthy();
    expect(screen.getByText("Try again")).toBeTruthy();
  });

  test("names the one section that failed and renders the rest", async () => {
    vi.mocked(getUnit).mockResolvedValue({
      data: unit({ jobs: null, unavailable: ["jobs"] }),
    } as never);

    panel(said(Serial));

    // The header still renders: one tool failing is not the machine going away.
    expect(await screen.findByText("SOLE WF80 2010")).toBeTruthy();
    expect(screen.getByText("Could not load this.")).toBeTruthy();

    // And the tabs that did load still carry their counts, while the failed one carries none.
    expect(screen.getByRole("tab", { name: "History 1" })).toBeTruthy();
    expect(screen.getByRole("tab", { name: "Jobs" })).toBeTruthy();
  });

  test("offers every number the thread has mentioned", async () => {
    vi.mocked(getUnit).mockResolvedValue({ data: unit() } as never);

    panel([
      { role: "user", text: "12345-1" },
      { role: "user", text: Serial },
    ]);

    // Newest first, and the older one stays one click away.
    expect(await screen.findByRole("button", { name: Serial })).toBeTruthy();
    expect(screen.getByRole("button", { name: "12345-1" })).toBeTruthy();
  });
  test("answers the cover question above the tabs, not inside the fourth one", async () => {
    vi.mocked(getUnit).mockResolvedValue({ data: unit() } as never);

    panel(said(Serial));

    // Eight categories do not fit above the tabs. Labor is the one staff quote, so it is the one
    // named, and it is readable without a tab click.
    expect(await screen.findByText("Out of warranty")).toBeTruthy();
    expect(screen.getByText(/^Labor ended/)).toBeTruthy();
  });

  test("keeps serials and work orders on bars of their own", async () => {
    vi.mocked(getUnit).mockResolvedValue({ data: unit() } as never);

    panel([
      { role: "user", text: "796955-1" },
      { role: "user", text: Serial },
    ]);

    // Selecting an order replaces the whole panel rather than filtering the machine, so the two
    // kinds never share a row. The machine stays one click away while an order is open.
    expect(await screen.findByRole("button", { name: Serial })).toBeTruthy();
    expect(screen.getByText("Work order")).toBeTruthy();
    expect(screen.getByRole("button", { name: "796955-1" })).toBeTruthy();
  });
});
