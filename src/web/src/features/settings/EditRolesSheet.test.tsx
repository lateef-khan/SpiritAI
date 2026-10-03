import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, expect, test, vi } from "vitest";

import type { PersonRow } from "@/api/types.gen";
import { HostRefusedError } from "@/lib/apiClient";

import { PERMISSIONS, person, role } from "./settingsFixtures";

vi.mock("@/api/sdk.gen", () => ({
  listRoles: vi.fn(),
  listPermissions: vi.fn(),
  setPersonRoles: vi.fn(),
}));

const { listPermissions, listRoles, setPersonRoles } = await import("@/api/sdk.gen");
const { EditRolesSheet } = await import("./EditRolesSheet");

function renderSheet(who: PersonRow) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={queryClient}>
      <EditRolesSheet person={who} open onOpenChange={() => {}} />
    </QueryClientProvider>,
  );
}

afterEach(cleanup);
beforeEach(() => {
  vi.resetAllMocks();
  vi.mocked(listPermissions).mockResolvedValue({ data: PERMISSIONS } as never);
  vi.mocked(listRoles).mockResolvedValue({
    data: [
      role({ id: "r-tech", name: "Technician", permissions: ["chat.agent.staff"] }),
      role({ id: "r-mgr", name: "TechManager", permissions: ["chat.agent.manager"] }),
      role({ id: "r-wh", name: "Warehouse", permissions: ["lookup.orders"] }),
    ],
  } as never);
});

test("shows the agent the picked roles give, and saves the ids", async () => {
  vi.mocked(setPersonRoles).mockResolvedValue({ data: person() } as never);
  renderSheet(person({ roles: [{ id: "r-tech", name: "Technician" }] }));

  expect(await screen.findByText("Staff agent", { selector: "b" })).toBeTruthy();
  fireEvent.click(screen.getByRole("checkbox", { name: /TechManager/ }));
  expect(screen.getByText("Manager agent", { selector: "b" })).toBeTruthy();

  fireEvent.click(screen.getByRole("button", { name: "Save" }));
  await waitFor(() =>
    expect(setPersonRoles).toHaveBeenCalledWith(
      expect.objectContaining({
        path: { id: person().id },
        body: { roleIds: ["r-tech", "r-mgr"] },
      }),
    ),
  );
});

test("warns when no picked role gives an agent", async () => {
  renderSheet(person({ roles: [{ id: "r-wh", name: "Warehouse" }] }));

  expect(await screen.findByText("No agent.")).toBeTruthy();
});

test("a failed save keeps the sheet open with the server's words", async () => {
  vi.mocked(setPersonRoles).mockRejectedValue(
    new HostRefusedError(409, "roles", null, "This would leave Spirit with no admin."),
  );
  renderSheet(person());

  await screen.findByRole("checkbox", { name: /Technician/ });
  fireEvent.click(screen.getByRole("button", { name: "Save" }));

  expect(await screen.findByText("This would leave Spirit with no admin.")).toBeTruthy();
});
