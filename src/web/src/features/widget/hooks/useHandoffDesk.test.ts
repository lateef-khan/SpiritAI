import { act, renderHook, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it } from "vitest";

import { HostRefusedError } from "@/lib/apiClient";
import { rememberCall } from "../api/visitorIdentity";
import type { HandoffState, WidgetApi } from "../api/widgetApi";
import { useHandoffDesk, WithBot } from "./useHandoffDesk";

/**
 * The desk, one test per way the state moves.
 *
 * The api is a fake that answers one state; what is held in place is that a remembered call is
 * read on mount, that a refresh answers what it read, and that a call the host has no row for
 * reads as `bot` rather than as an error.
 */
const waiting: HandoffState = {
  status: "waiting",
  position: 3,
  assigneeName: null,
  staffOnline: 0,
  email: null,
};

function fakeApi(handoffState: (callId: string) => Promise<HandoffState>): {
  api: WidgetApi;
  emails: { callId: string; email: string }[];
} {
  const emails: { callId: string; email: string }[] = [];
  return {
    emails,
    api: {
      createThread: async () => "call-1",
      history: async () => ({ messages: [] }),
      handoffState,
      leaveEmail: async (callId, email) => {
        emails.push({ callId, email });
      },
      say: async () => {
        throw new Error("not here");
      },
    },
  };
}

beforeEach(() => localStorage.clear());

describe("useHandoffDesk", () => {
  it("starts with the bot when nothing is remembered", () => {
    const { api } = fakeApi(async () => waiting);

    const view = renderHook(() => useHandoffDesk(api));

    expect(view.result.current.state).toEqual(WithBot);
  });

  it("reads a remembered call's state on mount", async () => {
    rememberCall("call-kept");
    const { api } = fakeApi(async () => waiting);

    const view = renderHook(() => useHandoffDesk(api));

    await waitFor(() => expect(view.result.current.state).toEqual(waiting));
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

  it("leaves the email on the remembered call and keeps it on the state", async () => {
    rememberCall("call-kept");
    const { api, emails } = fakeApi(async () => waiting);

    const view = renderHook(() => useHandoffDesk(api));
    await waitFor(() => expect(view.result.current.state.status).toBe("waiting"));

    await act(async () => {
      await view.result.current.leaveEmail("pat@example.com");
    });

    expect(emails).toEqual([{ callId: "call-kept", email: "pat@example.com" }]);
    expect(view.result.current.state.email).toBe("pat@example.com");
  });

  it("applies what a push said, over what it holds", async () => {
    rememberCall("call-kept");
    const { api } = fakeApi(async () => waiting);

    const view = renderHook(() => useHandoffDesk(api));
    await waitFor(() => expect(view.result.current.state.status).toBe("waiting"));

    act(() =>
      view.result.current.apply({ status: "human", position: null, assigneeName: "Dana R." }),
    );

    expect(view.result.current.state).toEqual({
      ...waiting,
      status: "human",
      position: null,
      assigneeName: "Dana R.",
    });
  });
});
