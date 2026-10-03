import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { cleanup, render, screen } from "@testing-library/react";
import { afterEach, beforeEach, expect, test, vi } from "vitest";

import { PERMISSIONS, role } from "./settingsFixtures";

vi.mock("@/api/sdk.gen", () => ({
  listRoles: vi.fn(),
  listPermissions: vi.fn(),
}));

/**
 * The global setup stubs the virtualizer to draw every row. These tests are about which rows
 * are drawn, so they use the real one, given a 600px viewport and fixed row heights because
 * happy-dom lays nothing out.
 */
vi.mock("@tanstack/react-virtual", async () => {
  const actual =
    await vi.importActual<typeof import("@tanstack/react-virtual")>("@tanstack/react-virtual");
  return {
    ...actual,
    useVirtualizer: (options: Parameters<typeof actual.useVirtualizer>[0]) =>
      actual.useVirtualizer({
        ...options,
        initialRect: { width: 800, height: 600 },
        observeElementRect: (_instance, callback) => {
          callback({ width: 800, height: 600 });
          return () => {};
        },
        measureElement: () => 57,
      }),
  };
});

const { listPermissions, listRoles } = await import("@/api/sdk.gen");
const { RolesSection } = await import("./RolesSection");

afterEach(cleanup);
beforeEach(() => {
  vi.resetAllMocks();
  vi.mocked(listPermissions).mockResolvedValue({ data: PERMISSIONS } as never);
});

test("500 roles draw far fewer rows, with the toolbar and header still shown", async () => {
  const rows = Array.from({ length: 500 }, (_, index) =>
    role({ id: `id-${index}`, name: `Role ${index}` }),
  );
  vi.mocked(listRoles).mockResolvedValue({ data: rows } as never);
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  render(
    <QueryClientProvider client={queryClient}>
      <RolesSection scrollElement={document.createElement("div")} />
    </QueryClientProvider>,
  );

  await screen.findByText("Role 0");
  expect(screen.getAllByRole("row").length).toBeLessThan(60);
  expect(screen.queryByText("Role 499")).toBeNull();
  expect(screen.getByRole("button", { name: "New role" })).toBeTruthy();
  expect(screen.getByRole("columnheader", { name: "Members" })).toBeTruthy();
});
