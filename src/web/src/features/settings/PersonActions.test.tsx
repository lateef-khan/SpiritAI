import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, expect, test, vi } from "vitest";

import type { PersonRow } from "@/api/types.gen";

import { person } from "./settingsFixtures";

vi.mock("@/api/sdk.gen", () => ({
  banPerson: vi.fn(),
  unbanPerson: vi.fn(),
  deletePerson: vi.fn(),
  unlinkPerson: vi.fn(),
  listRoles: vi.fn(),
  listPermissions: vi.fn(),
  setPersonRoles: vi.fn(),
}));

const { unbanPerson } = await import("@/api/sdk.gen");
const { PersonActions } = await import("./PersonActions");

function openMenu(row: PersonRow) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  render(
    <QueryClientProvider client={queryClient}>
      <PersonActions person={row} isYou={false} />
    </QueryClientProvider>,
  );
  fireEvent.keyDown(screen.getByRole("button", { name: "Actions for Dana Otto" }), {
    key: "Enter",
  });
}

afterEach(cleanup);
beforeEach(() => vi.resetAllMocks());

test("a banned person is unbanned from the menu in one call", async () => {
  vi.mocked(unbanPerson).mockResolvedValue({ data: person() } as never);
  openMenu(person({ banned: true, desk: "ready" }));

  fireEvent.click(await screen.findByRole("menuitem", { name: "Unban" }));
  fireEvent.click(await screen.findByRole("button", { name: "Unban" }));

  await waitFor(() =>
    expect(unbanPerson).toHaveBeenCalledWith(
      expect.objectContaining({ path: { id: person().id } }),
    ),
  );
  expect(unbanPerson).toHaveBeenCalledTimes(1);
});
