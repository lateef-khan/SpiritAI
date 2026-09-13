import {
  AssistantRuntimeProvider,
  useExternalStoreRuntime,
  type ThreadMessage,
} from "@assistant-ui/react";
import { cleanup, render, screen } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, test, vi } from "vitest";

import { getUnit } from "@/api/sdk.gen";
import type { UnitDocument } from "@/api/types.gen";
import type { Handoff } from "@/features/inbox/api/handoffsApi";
import { reviveHistory } from "@/lib/history";

import { ContextRail } from "./ContextRail";

/**
 * The rail's switch: which inside it shows for each state.
 *
 * `@/api/sdk.gen` is mocked the same way `UnitPanel.test.tsx` mocks it; the thread branch
 * runs under a bare external-store runtime, which is all `ThreadContextPanel` reads through
 * `useAuiState`.
 */
vi.mock("@/api/sdk.gen", () => ({ getUnit: vi.fn(), getOrder: vi.fn() }));

afterEach(cleanup);
beforeEach(() => vi.resetAllMocks());

const Serial = "5808881004036047";

function handoff(): Handoff {
  return {
    id: 7,
    callId: "call-7",
    status: "waiting",
    askedBy: "bot",
    reason: "Warranty claim.",
    askedAt: new Date("2026-09-12T12:38:00"),
    assignee: null,
    claimedAt: null,
    email: "lorrie@northwind.example",
    doneAt: null,
    title: "Treadmill belt slips at 8 mph",
    firstLine: "Hi, my CT800 belt slips when I go above 8 mph.",
    position: 1,
  };
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

function threadRail() {
  function ThreadMode() {
    const runtime = useExternalStoreRuntime({
      messages: [] as ThreadMessage[],
      onNew: () => Promise.resolve(),
    });
    return (
      <AssistantRuntimeProvider runtime={runtime}>
        <ContextRail mode="thread" handoff={null} history={null} />
      </AssistantRuntimeProvider>
    );
  }

  render(<ThreadMode />);
}

describe("ContextRail", () => {
  test("shows the unit panel for an agent thread", async () => {
    threadRail();

    expect(await screen.findByText("No information yet")).toBeTruthy();
  });

  test("shows the handoff's visitor and unit for a picked conversation", async () => {
    vi.mocked(getUnit).mockResolvedValue({ data: unit } as never);

    const history = reviveHistory({
      headId: "call-7:0",
      messages: [
        {
          parentId: null,
          message: {
            id: "call-7:0",
            role: "user",
            content: [{ type: "text", text: `My serial is ${Serial}.` }],
            attachments: [],
            createdAt: "2026-09-12T12:31:00",
            metadata: { custom: {} },
          },
        },
      ],
    });

    render(<ContextRail mode="handoff" handoff={handoff()} history={history} />);

    expect(screen.getByText("lorrie@northwind.example")).toBeTruthy();
    expect(await screen.findByText("SOLE WF80 2010")).toBeTruthy();
  });

  test("asks for a pick when nothing is selected", () => {
    render(<ContextRail mode="handoff" handoff={null} history={null} />);

    expect(screen.getByText("Pick a conversation.")).toBeTruthy();
    expect(getUnit).not.toHaveBeenCalled();
  });
});
