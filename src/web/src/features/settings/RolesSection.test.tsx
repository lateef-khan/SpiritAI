import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { cleanup, fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { afterEach, beforeEach, expect, test, vi } from "vitest";

import { PERMISSIONS, role } from "./settingsFixtures";

vi.mock("@/api/sdk.gen", () => ({
  listRoles: vi.fn(),
  listPermissions: vi.fn(),
  createRole: vi.fn(),
  updateRole: vi.fn(),
  deleteRole: vi.fn(),
}));

const { createRole, deleteRole, listPermissions, listRoles, updateRole } =
  await import("@/api/sdk.gen");
const { RolesSection } = await import("./RolesSection");

const ADMIN = role({
  id: "a0000000-0000-4000-8000-000000000001",
  name: "Admin",
  builtIn: true,
  permissions: PERMISSIONS.map((info) => info.key),
  members: 2,
});

function renderSection() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={queryClient}>
      <RolesSection scrollElement={null} />
    </QueryClientProvider>,
  );
}

afterEach(cleanup);
beforeEach(() => {
  vi.resetAllMocks();
  vi.mocked(listPermissions).mockResolvedValue({ data: PERMISSIONS } as never);
  vi.mocked(listRoles).mockResolvedValue({ data: [ADMIN, role({ members: 3 })] } as never);
});

test("lists each role with its members and permission count, and locks Admin", async () => {
  renderSection();

  const admin = (await screen.findByText("Admin")).closest("tr")!;
  expect(within(admin).getByText("All")).toBeTruthy();
  expect(within(admin).getByRole("button", { name: "Edit Admin" }).hasAttribute("disabled")).toBe(
    true,
  );
  expect(within(admin).getByRole("button", { name: "Delete Admin" }).hasAttribute("disabled")).toBe(
    true,
  );

  const tech = screen.getByText("Technician").closest("tr")!;
  expect(within(tech).getByText("3")).toBeTruthy();
  expect(within(tech).getByText("2")).toBeTruthy();
});

test("makes a role from the checked permissions, grouped by heading", async () => {
  vi.mocked(createRole).mockResolvedValue({ data: role({ id: "r-new" }) } as never);
  renderSection();

  fireEvent.click(await screen.findByRole("button", { name: "New role" }));
  expect(screen.getByText("Chat agent")).toBeTruthy();
  expect(screen.getByText("Lookup")).toBeTruthy();
  fireEvent.change(screen.getByLabelText("Name"), { target: { value: "Front desk" } });
  fireEvent.click(screen.getByRole("checkbox", { name: "Look up a work order" }));
  fireEvent.click(screen.getByRole("button", { name: "Save" }));

  await waitFor(() =>
    expect(createRole).toHaveBeenCalledWith(
      expect.objectContaining({
        body: { name: "Front desk", description: null, permissions: ["lookup.orders"] },
      }),
    ),
  );
});

test("edits a role, starting from what it holds", async () => {
  vi.mocked(updateRole).mockResolvedValue({ data: role() } as never);
  renderSection();

  fireEvent.click(await screen.findByRole("button", { name: "Edit Technician" }));
  expect(
    (screen.getByRole("checkbox", { name: "Staff agent" }) as HTMLButtonElement).getAttribute(
      "data-state",
    ),
  ).toBe("checked");
  fireEvent.click(screen.getByRole("checkbox", { name: "Look up a unit by serial" }));
  fireEvent.click(screen.getByRole("button", { name: "Save" }));

  await waitFor(() =>
    expect(updateRole).toHaveBeenCalledWith(
      expect.objectContaining({
        path: { id: "r-1" },
        body: { name: "Technician", description: null, permissions: ["chat.agent.staff"] },
      }),
    ),
  );
});

test("deleting names how many people hold the role", async () => {
  vi.mocked(deleteRole).mockResolvedValue({ data: undefined } as never);
  renderSection();

  fireEvent.click(await screen.findByRole("button", { name: "Delete Technician" }));
  expect(await screen.findByText(/3 people hold it/)).toBeTruthy();
  fireEvent.click(screen.getByRole("button", { name: "Delete role" }));

  await waitFor(() =>
    expect(deleteRole).toHaveBeenCalledWith(expect.objectContaining({ path: { id: "r-1" } })),
  );
});
