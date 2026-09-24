import { act, renderHook } from "@testing-library/react";
import { describe, expect, it } from "vitest";

import { HostRefusedError } from "@/lib/apiClient";
import type { HandoffState, WidgetApi } from "../api/widgetApi";
import { useHandoffDesk, WithBot } from "./useHandoffDesk";

/**
 * The desk, one test per way the state moves.
 *
 * The api is a fake that answers one state; what is held in place is that a refresh answers what
 * it read, and that a call the host has no row for reads as `bot` rather than as an error.
 */
const waiting: HandoffState = {
  status: "waiting",
  assigneeName: null,
  staffOnline: false,
  phone: null,
  code: null,
};

function fakeApi(handoffState: (callId: string) => Promise<HandoffState>): {
  api: WidgetApi;
  phones: { callId: string; phone: string }[];
} {
  const phones: { callId: string; phone: string }[] = [];
  return {
    phones,
    api: {
      createThread: async () => "call-1",
      latestThread: async () => null,
      history: async () => ({ repository: { messages: [] }, nextCursor: null }),
      handoffState,
      leavePhone: async (callId, phone) => {
        phones.push({ callId, phone });
      },
      say: async () => {
        throw new Error("not here");
      },
    },
  };
}

describe("useHandoffDesk", () => {
  it("starts with the bot", () => {
    const { api } = fakeApi(async () => waiting);

    const view = renderHook(() => useHandoffDesk(api));

    expect(view.result.current.state).toEqual(WithBot);
  });

  it("answers what a refresh read, and reads a forgotten call as the bot", async () => {
    let answer: HandoffState | null = waiting;
    const { api } = fakeApi(async () => {
      if (answer === null) throw new HostRefusedError(404, "/v1/public/handoff/call-1");
      return answer;
    });

    const view = renderHook(() => useHandoffDesk(api));

    let read: HandoffState | undefined;
    await act(async () => {
      read = await view.result.current.refresh("call-1");
    });
    expect(read).toEqual(waiting);
    expect(view.result.current.state).toEqual(waiting);

    answer = null;
    await act(async () => {
      read = await view.result.current.refresh("call-1");
    });
    expect(read).toEqual(WithBot);
    expect(view.result.current.state).toEqual(WithBot);
  });

  it("leaves the phone on the call it is given and reads the host's display form back", async () => {
    const { api, phones } = fakeApi(async () =>
      phones.length === 0 ? waiting : { ...waiting, phone: "+1 201-555-0123", code: 7 },
    );

    const view = renderHook(() => useHandoffDesk(api));
    await act(async () => {
      await view.result.current.refresh("call-kept");
    });

    await act(async () => {
      await view.result.current.leavePhone("call-kept", "(201) 555-0123");
    });

    expect(phones).toEqual([{ callId: "call-kept", phone: "(201) 555-0123" }]);
    expect(view.result.current.state.phone).toBe("+1 201-555-0123");
    expect(view.result.current.state.code).toBe(7);
  });

  it("applies what a push said, over what it holds", async () => {
    const { api } = fakeApi(async () => waiting);

    const view = renderHook(() => useHandoffDesk(api));
    await act(async () => {
      await view.result.current.refresh("call-kept");
    });

    act(() => view.result.current.apply({ status: "human", assigneeName: "Dana R." }));

    expect(view.result.current.state).toEqual({
      ...waiting,
      status: "human",
      assigneeName: "Dana R.",
    });
  });
});
