import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, beforeEach, expect, test, vi } from "vitest";

import type { PersonRow } from "@/api/types.gen";

import { PERMISSIONS, person } from "./settingsFixtures";

vi.mock("@/api/sdk.gen", () => ({
  getMe: vi.fn(),
  listPermissions: vi.fn(),
  listPeople: vi.fn(),
  linkPerson: vi.fn(),
  unlinkPerson: vi.fn(),
  listRoles: vi.fn(),
  setPersonRoles: vi.fn(),
  banPerson: vi.fn(),
  unbanPerson: vi.fn(),
  deletePerson: vi.fn(),
}));

vi.mock("@/features/auth/authClient", () => ({
  useSession: () => ({ data: { user: { id: "me" } } }),
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

const { getMe, listPeople, listPermissions } = await import("@/api/sdk.gen");
const { SettingsPage } = await import("./SettingsPage");

function people(count: number): PersonRow[] {
  return Array.from({ length: count }, (_, index) =>
    person({ id: `id-${index}`, name: `Person ${index}`, email: `person${index}@example.com` }),
  );
}

function renderPage(rows: PersonRow[]) {
  vi.mocked(listPeople).mockResolvedValue({ data: rows } as never);
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={queryClient}>
      <SettingsPage />
    </QueryClientProvider>,
  );
}

afterEach(cleanup);
beforeEach(() => {
  vi.resetAllMocks();
  vi.mocked(getMe).mockResolvedValue({
    data: {
      id: "me",
      name: "Me",
      email: "me@x.test",
      banned: false,
      permissions: ["settings.people"],
      agent: null,
    },
  } as never);
  vi.mocked(listPermissions).mockResolvedValue({ data: PERMISSIONS } as never);
});

test("500 people draw far fewer rows, and searching for the last one shows them", async () => {
  renderPage(people(500));

  await screen.findByText("Person 0");
  const drawn = screen.getAllByRole("row").length;
  expect(drawn).toBeLessThan(60);
  expect(screen.queryByText("Person 499")).toBeNull();

  fireEvent.change(screen.getByPlaceholderText("Search name or email"), {
    target: { value: "person499@" },
  });

  expect(await screen.findByText("Person 499")).toBeTruthy();
});

test("the row menu opens the Ban dialog only once Ban… is chosen", async () => {
  renderPage(people(3));

  await screen.findByText("Person 0");
  expect(screen.queryByRole("alertdialog")).toBeNull();

  fireEvent.keyDown(screen.getByRole("button", { name: "Actions for Person 0" }), {
    key: "Enter",
  });
  expect(screen.queryByRole("alertdialog")).toBeNull();

  fireEvent.click(await screen.findByRole("menuitem", { name: "Ban…" }));

  expect(await screen.findByRole("alertdialog")).toBeTruthy();
});
