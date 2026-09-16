import type { ExportedMessageRepository } from "@assistant-ui/react";
import { act, renderHook, waitFor } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";

import type { HandoffsApi } from "../api/handoffsApi";

const { useHandoffMessages } = await import("./useHandoffMessages");

/** An empty transcript, distinguishable by `headId` alone. */
function historyOf(headId: string): ExportedMessageRepository {
  return { headId, messages: [] };
}

describe("useHandoffMessages", () => {
  it("makes no call when no call is selected", () => {
    const messages = vi.fn();
    const api: HandoffsApi = {
      list: vi.fn(),
      messages,
      claim: vi.fn(),
      finish: vi.fn(),
      reply: vi.fn(),
    };

    const view = renderHook(() => useHandoffMessages(null, api));

    expect(view.result.current).toEqual({
      history: null,
      loading: false,
      error: null,
      reload: expect.any(Function),
    });
    expect(messages).not.toHaveBeenCalled();
  });

  it("loads the transcript for a selected call", async () => {
    const api: HandoffsApi = {
      list: vi.fn(),
      messages: vi.fn().mockResolvedValue(historyOf("call-1:0")),
      claim: vi.fn(),
      finish: vi.fn(),
      reply: vi.fn(),
    };

    const view = renderHook(() => useHandoffMessages("call-1", api));

    await waitFor(() => expect(view.result.current.loading).toBe(false));

    expect(view.result.current.history).toEqual(historyOf("call-1:0"));
    expect(view.result.current.error).toBeNull();
  });

  it("keeps only the last answer when the selected call changes twice quickly", async () => {
    // Two in-flight loads that settle out of order: call-2's answer arrives first, then
    // call-1's late answer must not overwrite it, because call-2 is what is on screen by then.
    const resolvers = new Map<string, (history: ExportedMessageRepository) => void>();
    const messages = vi.fn(
      (callId: string) =>
        new Promise<ExportedMessageRepository>((resolve) => resolvers.set(callId, resolve)),
    );
    const api: HandoffsApi = {
      list: vi.fn(),
      messages,
      claim: vi.fn(),
      finish: vi.fn(),
      reply: vi.fn(),
    };

    const view = renderHook(({ callId }) => useHandoffMessages(callId, api), {
      initialProps: { callId: "call-1" },
    });

    view.rerender({ callId: "call-2" });

    resolvers.get("call-2")!(historyOf("call-2:0"));
    await waitFor(() => expect(view.result.current.loading).toBe(false));

    resolvers.get("call-1")!(historyOf("call-1:0"));
    await act(() => Promise.resolve());

    expect(view.result.current.history).toEqual(historyOf("call-2:0"));
  });

  it("reloads without dropping the history already on screen", async () => {
    const messages = vi
      .fn()
      .mockResolvedValueOnce(historyOf("call-1:0"))
      .mockResolvedValueOnce(historyOf("call-1:1"));
    const api: HandoffsApi = {
      list: vi.fn(),
      messages,
      claim: vi.fn(),
      finish: vi.fn(),
      reply: vi.fn(),
    };

    const view = renderHook(() => useHandoffMessages("call-1", api));

    await waitFor(() => expect(view.result.current.loading).toBe(false));
    expect(view.result.current.history).toEqual(historyOf("call-1:0"));

    act(() => view.result.current.reload());

    expect(view.result.current.loading).toBe(false);
    expect(view.result.current.history).toEqual(historyOf("call-1:0"));

    await waitFor(() => expect(view.result.current.history).toEqual(historyOf("call-1:1")));

    expect(messages).toHaveBeenCalledTimes(2);
  });
});
