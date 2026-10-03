import { renderHook, waitFor } from "@testing-library/react";
import { beforeEach, expect, test, vi } from "vitest";

import { queryWrapper } from "@/test/query";

vi.mock("@/api/sdk.gen", () => ({ getMe: vi.fn() }));

const { getMe } = await import("@/api/sdk.gen");
const { useCan } = await import("./useMe");

function serveMe(permissions: string[], banned = false) {
  vi.mocked(getMe).mockResolvedValue({
    data: { id: "u-1", name: "Dana", email: "dana@x.test", banned, permissions, agent: null },
  } as never);
}

beforeEach(() => vi.resetAllMocks());

test("is true for a permission the person holds", async () => {
  serveMe(["lookup.units", "lookup.orders"]);
  const { result } = renderHook(() => useCan("lookup.units"), queryWrapper());

  await waitFor(() => expect(result.current).toBe(true));
});

test("is false for one they do not hold", async () => {
  serveMe(["lookup.orders"]);
  const { result } = renderHook(
    () => [useCan("lookup.orders"), useCan("settings.people")],
    queryWrapper(),
  );

  await waitFor(() => expect(result.current).toEqual([true, false]));
});

test("is false until the server has answered", () => {
  vi.mocked(getMe).mockReturnValue(new Promise(() => {}) as never);
  const { result } = renderHook(() => useCan("lookup.orders"), queryWrapper());

  expect(result.current).toBe(false);
});
