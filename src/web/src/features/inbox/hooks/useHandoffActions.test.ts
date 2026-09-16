import { act, renderHook, waitFor } from "@testing-library/react";
import { describe, expect, it } from "vitest";

import { HostRefusedError } from "@/lib/apiClient";

import type { Handoff, HandoffsApi } from "../api/handoffsApi";
import { useHandoffActions } from "./useHandoffActions";

/**
 * `useHandoffActions`, against a fake api rather than a network.
 *
 * `take` and `finish` both funnel through the same `busy`/`error` bookkeeping, so one test per
 * outcome — a resolved claim, a `HostRefusedError` with a `title`, and a plain `Error` — covers
 * both actions without repeating the plumbing.
 */

const summary: Handoff = {
  id: 1,
  callId: "call-1",
  status: "human",
  askedBy: "visitor",
  reason: null,
  askedAt: new Date("2026-09-10T09:00:00Z"),
  assignee: { key: "user:dana", name: "Dana" },
  claimedAt: new Date("2026-09-12T12:50:00"),
  email: null,
  doneAt: null,
  title: null,
  firstLine: null,
  position: null,
};

/** Fails a test that reaches a route it has no business calling. */
function notNeeded(): never {
  throw new Error("not needed for this test");
}

describe("useHandoffActions take", () => {
  it("resolves the claimed handoff and returns busy to false", async () => {
    const api: HandoffsApi = {
      list: async () => notNeeded(),
      messages: async () => notNeeded(),
      claim: async () => summary,
      finish: async () => notNeeded(),
      reply: async () => notNeeded(),
    };

    const view = renderHook(() => useHandoffActions(api));

    let claimed: Handoff | null = null;
    await act(async () => {
      claimed = await view.result.current.take("call-1");
    });

    expect(claimed).toEqual(summary);
    await waitFor(() => expect(view.result.current.busy).toBe(false));
  });

  it("sets error to the refusal's title", async () => {
    const api: HandoffsApi = {
      list: async () => notNeeded(),
      messages: async () => notNeeded(),
      claim: async () => Promise.reject(new HostRefusedError(409, "/x", "Somebody has this chat.")),
      finish: async () => notNeeded(),
      reply: async () => notNeeded(),
    };

    const view = renderHook(() => useHandoffActions(api));

    await act(async () => {
      await view.result.current.take("call-1");
    });

    expect(view.result.current.error).toBe("Somebody has this chat.");
  });

  it("sets error to a plain Error's message", async () => {
    const api: HandoffsApi = {
      list: async () => notNeeded(),
      messages: async () => notNeeded(),
      claim: async () => Promise.reject(new Error("host refused")),
      finish: async () => notNeeded(),
      reply: async () => notNeeded(),
    };

    const view = renderHook(() => useHandoffActions(api));

    await act(async () => {
      await view.result.current.take("call-1");
    });

    expect(view.result.current.error).toBe("host refused");
  });
});

describe("useHandoffActions finish", () => {
  it("resolves true on success", async () => {
    const api: HandoffsApi = {
      list: async () => notNeeded(),
      messages: async () => notNeeded(),
      claim: async () => notNeeded(),
      finish: async () => undefined,
      reply: async () => notNeeded(),
    };

    const view = renderHook(() => useHandoffActions(api));

    let done = false;
    await act(async () => {
      done = await view.result.current.finish("call-1");
    });

    expect(done).toBe(true);
  });

  it("ignores a second call while one is already in flight", async () => {
    let releaseFirst!: () => void;
    const first = new Promise<void>((resolve) => (releaseFirst = resolve));
    let calls = 0;
    const api: HandoffsApi = {
      list: async () => notNeeded(),
      messages: async () => notNeeded(),
      claim: async () => notNeeded(),
      finish: async () => {
        calls += 1;
        await first;
      },
      reply: async () => notNeeded(),
    };

    const view = renderHook(() => useHandoffActions(api));

    let firstResult: Promise<boolean>;
    let secondResult: Promise<boolean>;
    act(() => {
      firstResult = view.result.current.finish("call-1");
      secondResult = view.result.current.finish("call-1");
    });

    releaseFirst();
    await act(async () => {
      await Promise.all([firstResult, secondResult]);
    });

    expect(calls).toBe(1);
    await expect(secondResult!).resolves.toBe(false);
  });
});
