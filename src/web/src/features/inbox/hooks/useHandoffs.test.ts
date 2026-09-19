import { renderHook, waitFor } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";

import { handoffOf, stubHandoffsApi } from "@/test/handoffs";
import { queryWrapper } from "@/test/query";

import { defaultFilter } from "../inboxFilter";
import { useHandoffs } from "./useHandoffs";

/**
 * The listing hook, against a fake api.
 *
 * The host cuts the pages and says where the next one starts; what is worth holding in place
 * here is that the hook asks for the next page with exactly the cursor the last page handed
 * back, and reads the two out as one list in order.
 */

const Open = defaultFilter("open");

describe("useHandoffs", () => {
  it("asks for the next page with the last page's cursor, and lists both in order", async () => {
    const first = handoffOf({ id: 1, callId: "call-1" });
    const second = handoffOf({ id: 2, callId: "call-2" });
    const list = vi
      .fn()
      .mockResolvedValueOnce({ items: [first], nextCursor: "after-1" })
      .mockResolvedValueOnce({ items: [second], nextCursor: null });
    const api = stubHandoffsApi({ list });

    const view = renderHook(() => useHandoffs(Open, api), { wrapper: queryWrapper().wrapper });

    await waitFor(() => expect(view.result.current.loading).toBe(false));
    expect(view.result.current.rows).toEqual([first]);
    expect(view.result.current.hasMore).toBe(true);

    view.result.current.loadMore();

    await waitFor(() => expect(view.result.current.rows).toEqual([first, second]));
    expect(view.result.current.hasMore).toBe(false);
    expect(list).toHaveBeenNthCalledWith(1, Open, null);
    expect(list).toHaveBeenNthCalledWith(2, Open, "after-1");
  });

  it("reports the error from a refused request", async () => {
    const api = stubHandoffsApi({ list: vi.fn().mockRejectedValue(new Error("host refused")) });

    const view = renderHook(() => useHandoffs(Open, api), { wrapper: queryWrapper().wrapper });

    await waitFor(() => expect(view.result.current.loading).toBe(false));

    expect(view.result.current.error?.message).toBe("host refused");
    expect(view.result.current.rows).toEqual([]);
  });
});
