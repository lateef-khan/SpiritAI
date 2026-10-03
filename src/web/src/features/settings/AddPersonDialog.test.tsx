import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, expect, test, vi } from "vitest";

import { HostRefusedError } from "@/lib/apiClient";

import { PERMISSIONS, person, role } from "./settingsFixtures";

vi.mock("@/api/sdk.gen", () => ({
  listRoles: vi.fn(),
  listPermissions: vi.fn(),
  addPerson: vi.fn(),
}));

const { addPerson, listPermissions, listRoles } = await import("@/api/sdk.gen");
const { AddPersonDialog } = await import("./AddPersonDialog");

function renderDialog() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={queryClient}>
      <AddPersonDialog open onOpenChange={() => {}} />
    </QueryClientProvider>,
  );
}

function fillAndSubmit() {
  fireEvent.change(screen.getByLabelText("Name"), { target: { value: "Ann Lee" } });
  fireEvent.change(screen.getByLabelText("Email"), {
    target: { value: "Ann.Lee@SpiritFitness.test" },
  });
  fireEvent.click(screen.getByRole("button", { name: "Add person" }));
}

const steps = (over = {}) => ({
  neon: "done",
  roles: "done",
  desk: "none",
  crm: "none",
  detail: null,
  ...over,
});

afterEach(cleanup);
beforeEach(() => {
  vi.resetAllMocks();
  vi.mocked(listRoles).mockResolvedValue({ data: [role({ id: "r-tech" })] } as never);
  vi.mocked(listPermissions).mockResolvedValue({ data: PERMISSIONS } as never);
});

test("adds in one call, with the picked roles and apps, and the email lower-cased", async () => {
  vi.mocked(addPerson).mockResolvedValue({
    data: { person: person(), steps: steps({ desk: "done" }) },
  } as never);
  renderDialog();

  fireEvent.click(await screen.findByRole("checkbox", { name: /Technician/ }));
  fireEvent.click(screen.getByRole("checkbox", { name: "Also link Desk" }));
  fillAndSubmit();

  await waitFor(() => expect(addPerson).toHaveBeenCalledTimes(1));
  expect(addPerson).toHaveBeenCalledWith(
    expect.objectContaining({
      body: {
        name: "Ann Lee",
        email: "ann.lee@spiritfitness.test",
        roleIds: ["r-tech"],
        desk: true,
        crm: false,
      },
    }),
  );
  expect(await screen.findByRole("button", { name: "Done" })).toBeTruthy();
});

test("shows the agent the picked role gives", async () => {
  renderDialog();

  fireEvent.click(await screen.findByRole("checkbox", { name: /Technician/ }));

  expect(screen.getByText("Staff agent", { selector: "b" })).toBeTruthy();
});

test("a later step that failed is shown with its reason, and the person is finished from the row", async () => {
  vi.mocked(addPerson).mockResolvedValue({
    data: { person: person(), steps: steps({ desk: "failed", detail: "Desk did not answer." }) },
  } as never);
  renderDialog();

  fireEvent.click(await screen.findByRole("checkbox", { name: "Also link Desk" }));
  fillAndSubmit();

  expect(await screen.findByText("Desk did not answer.")).toBeTruthy();
  expect(screen.getByText("Finish the rest from their row in the table.")).toBeTruthy();
  expect(screen.getByRole("button", { name: "Done" })).toBeTruthy();
});

test("a failed step the server gave no reason for still says it did not finish", async () => {
  vi.mocked(addPerson).mockResolvedValue({
    data: { person: person(), steps: steps({ desk: "failed", detail: null }) },
  } as never);
  renderDialog();

  fireEvent.click(await screen.findByRole("checkbox", { name: "Also link Desk" }));
  fillAndSubmit();

  expect(await screen.findByText("A step did not finish.")).toBeTruthy();
});

test("a failed sign-in offers Try again", async () => {
  vi.mocked(addPerson).mockResolvedValue({
    data: {
      person: null,
      steps: steps({ neon: "failed", roles: "none", detail: "Neon did not answer." }),
    },
  } as never);
  renderDialog();

  await screen.findByRole("checkbox", { name: /Technician/ });
  fillAndSubmit();

  expect(await screen.findByText("Neon did not answer.")).toBeTruthy();
  expect(screen.getByRole("button", { name: "Try again" })).toBeTruthy();
});

test("roles that were not saved offer Try again, which adds once more", async () => {
  vi.mocked(addPerson)
    .mockResolvedValueOnce({
      data: {
        person: person(),
        steps: steps({ roles: "failed", detail: "The roles were not saved." }),
      },
    } as never)
    .mockResolvedValueOnce({ data: { person: person(), steps: steps() } } as never);
  renderDialog();

  await screen.findByRole("checkbox", { name: /Technician/ });
  fillAndSubmit();

  expect(await screen.findByText("The roles were not saved.")).toBeTruthy();
  fireEvent.click(screen.getByRole("button", { name: "Try again" }));

  expect(await screen.findByRole("button", { name: "Done" })).toBeTruthy();
  expect(addPerson).toHaveBeenCalledTimes(2);
});

test.each(["Somebody already has that email.", "This person is banned. Unban them first."])(
  "shows the server's words for a 409: %s",
  async (words) => {
    vi.mocked(addPerson).mockRejectedValue(new HostRefusedError(409, "people", null, words));
    renderDialog();

    await screen.findByRole("checkbox", { name: /Technician/ });
    fillAndSubmit();

    expect(await screen.findByText(words)).toBeTruthy();
  },
);

test("Cancel is disabled while the call is pending", async () => {
  vi.mocked(addPerson).mockReturnValue(new Promise(() => {}) as never);
  renderDialog();

  await screen.findByRole("checkbox", { name: /Technician/ });
  fillAndSubmit();

  await waitFor(() =>
    expect(screen.getByRole("button", { name: "Cancel" }).hasAttribute("disabled")).toBe(true),
  );
});
