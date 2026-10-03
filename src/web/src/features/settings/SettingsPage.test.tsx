import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { act, cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, test, vi } from "vitest";

import type { PersonRow } from "@/api/types.gen";
import { HostRefusedError } from "@/lib/apiClient";

import { PERMISSIONS, person } from "./settingsFixtures";

/**
 * The generated client is mocked rather than the network under it, the same way `HubPage.test.tsx`
 * mocks `@/api/sdk.gen`.
 */
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

const { getMe, listPeople, linkPerson, listPermissions, listRoles } = await import("@/api/sdk.gen");

const { SettingsPage } = await import("./SettingsPage");

function servePeople(rows: PersonRow[]) {
  vi.mocked(listPeople).mockResolvedValue({ data: rows } as never);
}

/** A fresh client per test: no retries, so a rejected query settles on the first try. */
function renderPage() {
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

describe("SettingsPage", () => {
  test("a person with settings.roles alone sees Roles, not People", async () => {
    vi.mocked(getMe).mockResolvedValue({
      data: {
        id: "me",
        name: "Me",
        email: "me@x.test",
        banned: false,
        permissions: ["settings.roles"],
        agent: null,
      },
    } as never);
    vi.mocked(listRoles).mockResolvedValue({ data: [] } as never);
    renderPage();

    expect(await screen.findByRole("button", { name: "Roles" })).toBeTruthy();
    expect(screen.queryByRole("button", { name: "People" })).toBeNull();
    expect(listPeople).not.toHaveBeenCalled();
  });

  test("shows a left sidebar with People selected", async () => {
    servePeople([]);
    renderPage();

    const item = await screen.findByRole("button", { name: "People" });
    expect(item.getAttribute("data-active")).toBe("true");
  });

  test("shows Linked, Create, or Finish per app from the server's states", async () => {
    servePeople([
      person({ id: "1", desk: "ready", crm: "none" }),
      person({ id: "2", name: "Sam Reed", desk: "unfinished", crm: "ready" }),
    ]);
    renderPage();

    await screen.findByText("Dana Otto");
    expect(screen.getAllByText("Linked")).toHaveLength(2);
    expect(screen.getByRole("button", { name: "Create" })).toBeTruthy();
    expect(screen.getByRole("button", { name: "Finish" })).toBeTruthy();
  });

  test("sends one create when Create is clicked twice fast", async () => {
    servePeople([person({ desk: "none", crm: "ready" })]);
    let resolveLink!: (value: unknown) => void;
    vi.mocked(linkPerson).mockReturnValue(
      new Promise((resolve) => {
        resolveLink = resolve;
      }) as never,
    );
    renderPage();

    const button = await screen.findByRole("button", { name: "Create" });
    fireEvent.click(button);
    fireEvent.click(button);

    // The ref guard in `PersonLinkButton` decides call count at click time, synchronously, so a
    // second call was already refused before either promise settles — this only waits for the
    // one call the guard did let through.
    await waitFor(() => expect(linkPerson).toHaveBeenCalled());
    expect(linkPerson).toHaveBeenCalledTimes(1);
    expect(linkPerson).toHaveBeenCalledWith({
      throwOnError: true,
      path: { id: person().id, app: "desk" },
    });

    // Settles the pending request so it does not resolve after the test has ended.
    await act(async () => {
      resolveLink({ data: person({ desk: "ready", crm: "ready" }) });
    });
  });

  test("shows the app's own words when the email is already used", async () => {
    servePeople([person({ desk: "none", crm: "ready" })]);
    vi.mocked(linkPerson).mockRejectedValue(
      new HostRefusedError(
        409,
        "/v1/settings/people/1/desk",
        "The app already has this email.",
        "email already used in Desk",
      ),
    );
    renderPage();

    fireEvent.click(await screen.findByRole("button", { name: "Create" }));

    expect(await screen.findByText("email already used in Desk")).toBeTruthy();
  });

  test("shows the server's words for a refusal that is not the email-already-used 409", async () => {
    servePeople([person({ desk: "none", crm: "ready" })]);
    vi.mocked(linkPerson).mockRejectedValue(
      new HostRefusedError(
        503,
        "/v1/settings/people/1/desk",
        "CRM is unavailable.",
        "CRM is not set up yet.",
      ),
    );
    renderPage();

    fireEvent.click(await screen.findByRole("button", { name: "Create" }));

    expect(await screen.findByText("CRM is not set up yet.")).toBeTruthy();
  });

  test("shows Linked after a create", async () => {
    vi.mocked(listPeople)
      .mockResolvedValueOnce({ data: [person({ desk: "none", crm: "ready" })] } as never)
      .mockResolvedValueOnce({ data: [person({ desk: "ready", crm: "ready" })] } as never);
    vi.mocked(linkPerson).mockResolvedValue({
      data: person({ desk: "ready", crm: "ready" }),
    } as never);
    renderPage();

    fireEvent.click(await screen.findByRole("button", { name: "Create" }));

    await waitFor(() => expect(screen.getAllByText("Linked")).toHaveLength(2));
  });

  test("says People is not theirs on a 403", async () => {
    vi.mocked(listPeople).mockRejectedValue(new HostRefusedError(403, "/v1/settings/people", null));
    renderPage();

    expect(await screen.findByText("Your roles do not open People.")).toBeTruthy();
  });

  test("says Settings could not load when the server does not say who they are", async () => {
    vi.mocked(getMe).mockRejectedValue(new HostRefusedError(500, "/v1/me", null));
    renderPage();

    expect(await screen.findByText("Could not load Settings.")).toBeTruthy();
    expect(listPeople).not.toHaveBeenCalled();
  });

  test("a person without Settings permissions sees no section", async () => {
    vi.mocked(getMe).mockResolvedValue({
      data: {
        id: "me",
        name: "Me",
        email: "me@x.test",
        banned: false,
        permissions: ["lookup.units"],
        agent: null,
      },
    } as never);
    renderPage();

    expect(await screen.findByText("Your roles do not open Settings.")).toBeTruthy();
    expect(screen.queryByRole("button", { name: "People" })).toBeNull();
    expect(listPeople).not.toHaveBeenCalled();
  });

  test("filters to banned people, and search narrows by name or email", async () => {
    servePeople([
      person({ id: "1", name: "Dana Otto" }),
      person({ id: "2", name: "Sam Reed", email: "sam@example.com", banned: true }),
    ]);
    renderPage();

    await screen.findByText("Dana Otto");
    fireEvent.click(screen.getByRole("radio", { name: "Banned" }));
    expect(screen.queryByText("Dana Otto")).toBeNull();
    expect(screen.getByText("Sam Reed")).toBeTruthy();

    fireEvent.click(screen.getByRole("radio", { name: "All" }));
    fireEvent.change(screen.getByPlaceholderText("Search name or email"), {
      target: { value: "sam@" },
    });
    expect(screen.queryByText("Dana Otto")).toBeNull();
    expect(screen.getByText("Sam Reed")).toBeTruthy();
  });

  test("your own row cannot be banned or deleted", async () => {
    servePeople([person({ id: "me", name: "Matthew Hsu" })]);
    renderPage();

    fireEvent.pointerDown(await screen.findByRole("button", { name: "Actions for Matthew Hsu" }), {
      button: 0,
      ctrlKey: false,
    });
    expect(
      (await screen.findByRole("menuitem", { name: /Ban/ })).getAttribute("data-disabled"),
    ).not.toBeNull();
    expect(
      screen.getByRole("menuitem", { name: /Delete/ }).getAttribute("data-disabled"),
    ).not.toBeNull();
  });

  test("shows No agent and No roles, and Banned", async () => {
    servePeople([
      person({ id: "1", roles: [{ id: "r-wh", name: "Warehouse" }], agent: null }),
      person({ id: "2", name: "Sam Reed", roles: [], agent: null }),
      person({ id: "3", name: "Lee Park", banned: true }),
    ]);
    renderPage();

    expect(await screen.findByText("No agent")).toBeTruthy();
    expect(screen.getByText("No roles")).toBeTruthy();
    expect(screen.getByText("Banned", { selector: "[data-slot=badge]" })).toBeTruthy();
  });

  test("shows the agent's label and the role names", async () => {
    servePeople([
      person({
        roles: [{ id: "r-1", name: "Technician" }],
        agent: "chat.agent.staff",
      }),
    ]);
    renderPage();

    expect(await screen.findByText("Staff agent")).toBeTruthy();
    expect(screen.getByText("Roles: Technician")).toBeTruthy();
  });

  test("a Linked cell offers Unlink", async () => {
    servePeople([person({ desk: "ready" })]);
    renderPage();

    fireEvent.click(await screen.findByRole("button", { name: "Unlink Desk" }));
    expect(await screen.findByRole("alertdialog")).toBeTruthy();
  });

  test("disables the Unlink X on your own row", async () => {
    servePeople([person({ id: "me", desk: "ready", crm: "ready" })]);
    renderPage();

    await screen.findByText("Dana Otto");
    expect(screen.getByRole("button", { name: "Unlink Desk" }).hasAttribute("disabled")).toBe(true);
    expect(screen.getByRole("button", { name: "Unlink CRM" }).hasAttribute("disabled")).toBe(true);
  });
});
