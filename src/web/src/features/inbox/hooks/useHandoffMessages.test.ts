import type { ExportedMessageRepository } from "@assistant-ui/react";
import { act, renderHook, waitFor } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";

import type { HistoryPage } from "@/lib/history";
import { stubHandoffsApi } from "@/test/handoffs";
import { queryWrapper } from "@/test/query";

const { useHandoffMessages } = await import("./useHandoffMessages");

/** An empty transcript, distinguishable by `headId` alone. */
function historyOf(headId: string): ExportedMessageRepository {
  return { headId, messages: [] };
}

/** The host's answer for an empty transcript: one page, nothing before it. */
function pageOf(headId: string): HistoryPage {
  return { repository: historyOf(headId), nextCursor: null };
}

/** One message, for a transcript whose shape matters. */
function said(id: string, parentId: string | null): ExportedMessageRepository["messages"][number] {
  return {
    parentId,
    message: {
      id,
      role: "user",
      content: [{ type: "text", text: id }],
      createdAt: new Date("2026-09-12T12:00:00Z"),
      attachments: [],
      metadata: { custom: {} },
    },
  };
}

describe("useHandoffMessages", () => {
  it("makes no call when no call is selected", () => {
    const history = vi.fn();
    const api = stubHandoffsApi({ history });

    const view = renderHook(() => useHandoffMessages(null, api), {
      wrapper: queryWrapper().wrapper,
    });

    expect(view.result.current).toEqual({
      history: null,
      loading: false,
      error: null,
      reload: expect.any(Function),
      older: undefined,
    });
    expect(history).not.toHaveBeenCalled();
  });

  it("loads the transcript for a selected call", async () => {
    const api = stubHandoffsApi({ history: vi.fn().mockResolvedValue(pageOf("call-1:0")) });

    const view = renderHook(() => useHandoffMessages("call-1", api), {
      wrapper: queryWrapper().wrapper,
    });

    await waitFor(() => expect(view.result.current.loading).toBe(false));

    expect(view.result.current.history).toEqual(historyOf("call-1:0"));
    expect(view.result.current.error).toBeNull();
  });

  it("keeps only the last answer when the selected call changes twice quickly", async () => {
    // Two in-flight loads that settle out of order: call-2's answer arrives first, then
    // call-1's late answer must not overwrite it, because call-2 is what is on screen by then.
    const resolvers = new Map<string, (page: HistoryPage) => void>();
    const history = vi.fn(
      (callId: string) => new Promise<HistoryPage>((resolve) => resolvers.set(callId, resolve)),
    );
    const api = stubHandoffsApi({ history });

    const view = renderHook(({ callId }) => useHandoffMessages(callId, api), {
      initialProps: { callId: "call-1" },
      wrapper: queryWrapper().wrapper,
    });

    view.rerender({ callId: "call-2" });

    resolvers.get("call-2")!(pageOf("call-2:0"));
    await waitFor(() => expect(view.result.current.loading).toBe(false));

    resolvers.get("call-1")!(pageOf("call-1:0"));
    await act(() => Promise.resolve());

    expect(view.result.current.history).toEqual(historyOf("call-2:0"));
  });

  it("reloads without dropping the history already on screen", async () => {
    const history = vi
      .fn()
      .mockResolvedValueOnce(pageOf("call-1:0"))
      .mockResolvedValueOnce(pageOf("call-1:1"));
    const api = stubHandoffsApi({ history });

    const view = renderHook(() => useHandoffMessages("call-1", api), {
      wrapper: queryWrapper().wrapper,
    });

    await waitFor(() => expect(view.result.current.loading).toBe(false));
    expect(view.result.current.history).toEqual(historyOf("call-1:0"));

    act(() => view.result.current.reload());

    expect(view.result.current.loading).toBe(false);
    expect(view.result.current.history).toEqual(historyOf("call-1:0"));

    await waitFor(() => expect(view.result.current.history).toEqual(historyOf("call-1:1")));

    expect(history).toHaveBeenCalledTimes(2);
  });

  it("pages back from the cursor the newest page came with, and shows the page above it", async () => {
    const history = vi.fn().mockResolvedValue({
      repository: { headId: "m3", messages: [said("m2", null), said("m3", "m2")] },
      nextCursor: "before-m2",
    });
    const api = stubHandoffsApi({ history });

    const view = renderHook(() => useHandoffMessages("call-1", api), {
      wrapper: queryWrapper().wrapper,
    });
    await waitFor(() => expect(view.result.current.loading).toBe(false));

    const older = view.result.current.older!;
    expect(older.id).toBe("call-1");
    expect(older.initialCursor).toBe("before-m2");

    act(() => older.merge!({ messages: [said("m0", null), said("m1", "m0")] }));

    await waitFor(() => expect(view.result.current.history?.messages).toHaveLength(4));
    const merged = view.result.current.history!;
    expect(merged.headId).toBe("m3");
    expect(merged.messages.map((item) => [item.message.id, item.parentId])).toEqual([
      ["m0", null],
      ["m1", "m0"],
      ["m2", "m1"],
      ["m3", "m2"],
    ]);
  });
});
