import { act, cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";

import { HostRefusedError } from "@/lib/apiClient";

import type { Handoff } from "../api/handoffsApi";

/**
 * The Take/Done buttons, against a mocked wire.
 *
 * `@/api/sdk.gen` is mocked the same way `HandoffChat.test.tsx` mocks it: `useHandoffActions`'s
 * own `api` parameter is not reachable from this component, so what stands in for the network is
 * the generated `claimHandoff`/`finishHandoff` calls underneath `createHandoffsApi`.
 */
vi.mock("@/api/sdk.gen", () => ({ claimHandoff: vi.fn(), finishHandoff: vi.fn() }));

const { claimHandoff } = await import("@/api/sdk.gen");
const { HandoffChatActions } = await import("./HandoffChatActions");
const { HandoffChatHeader } = await import("./HandoffChatHeader");

const MeKey = "user:dana";

function handoff(over: Partial<Handoff> = {}): Handoff {
  return {
    id: 1,
    callId: "call-1",
    status: "waiting",
    askedBy: "visitor",
    reason: null,
    askedAt: new Date("2026-09-12T12:38:00"),
    assignee: null,
    claimedAt: null,
    email: null,
    doneAt: null,
    title: "Treadmill belt slips at 8 mph",
    firstLine: "I already did that twice.",
    position: 1,
    awaitingReply: false,
    unread: false,
    ...over,
  };
}

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
});

describe("HandoffChatActions", () => {
  it("shows Done then Take for a waiting handoff", () => {
    render(<HandoffChatActions handoff={handoff()} meKey={MeKey} onChanged={() => {}} />);

    const buttons = screen.getAllByRole("button").map((button) => button.textContent);

    expect(buttons).toEqual(["Done", "Take"]);
  });

  it("claims the handoff on Take and reports the claimed row back", async () => {
    const claimed: Handoff = {
      ...handoff(),
      status: "human",
      assignee: { key: MeKey, name: "Dana" },
      claimedAt: new Date("2026-09-12T12:50:00"),
    };
    vi.mocked(claimHandoff).mockResolvedValue({ data: claimed } as never);

    const onChanged = vi.fn();
    render(<HandoffChatActions handoff={handoff()} meKey={MeKey} onChanged={onChanged} />);

    await act(async () => {
      fireEvent.click(screen.getByRole("button", { name: "Take" }));
    });

    expect(claimHandoff).toHaveBeenCalledWith(
      expect.objectContaining({ path: { conversationId: "call-1" } }),
    );
    expect(onChanged).toHaveBeenCalledWith(expect.objectContaining({ status: "human" }));
  });

  it("shows only Done for a handoff the signed-in caller has", () => {
    render(
      <HandoffChatActions
        handoff={handoff({ status: "human", assignee: { key: MeKey, name: "Dana" } })}
        meKey={MeKey}
        onChanged={() => {}}
      />,
    );

    const buttons = screen.getAllByRole("button").map((button) => button.textContent);

    expect(buttons).toEqual(["Done"]);
  });

  it("shows no buttons for a handoff someone else has", () => {
    render(
      <HandoffChatHeader
        handoff={handoff({ status: "human", assignee: { key: "user:other", name: "Dana" } })}
        now={new Date("2026-09-12T12:53:00")}
        meKey={MeKey}
        onChanged={() => {}}
      />,
    );

    expect(screen.getByText("Dana has this chat")).toBeTruthy();
    expect(screen.queryByRole("button")).toBeNull();
  });

  it("shows no buttons for a handoff already done, even one I claimed", () => {
    render(
      <HandoffChatHeader
        handoff={handoff({ status: "done", assignee: { key: MeKey, name: "Dana" } })}
        now={new Date("2026-09-12T12:53:00")}
        meKey={MeKey}
        onChanged={() => {}}
      />,
    );

    expect(screen.getByText("Back with Spirit")).toBeTruthy();
    expect(screen.queryByRole("button")).toBeNull();
  });

  it("shows the host's refusal when somebody else already claimed it", async () => {
    vi.mocked(claimHandoff).mockRejectedValue(
      new HostRefusedError(409, "/v1/handoff/call-1/claim", "Somebody has this chat."),
    );

    render(<HandoffChatActions handoff={handoff()} meKey={MeKey} onChanged={() => {}} />);

    await act(async () => {
      fireEvent.click(screen.getByRole("button", { name: "Take" }));
    });

    expect(screen.getByText("Somebody has this chat.")).toBeTruthy();
  });
});
