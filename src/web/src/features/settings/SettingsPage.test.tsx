import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { act, cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, test, vi } from "vitest";

import type { PersonRow } from "@/api/types.gen";
import { HostRefusedError } from "@/lib/apiClient";

/**
 * The generated client is mocked rather than the network under it, the same way `HubPage.test.tsx`
 * mocks `@/api/sdk.gen`.
 */
vi.mock("@/api/sdk.gen", () => ({ listPeople: vi.fn(), linkPerson: vi.fn() }));

const { listPeople, linkPerson } = await import("@/api/sdk.gen");

const { SettingsPage } = await import("./SettingsPage");

function person(over: Partial<PersonRow> = {}): PersonRow {
  return {
    id: "11111111-1111-1111-1111-111111111111",
    name: "Dana Otto",
    email: "dana@example.com",
    groups: ["TechServiceManager"],
    desk: "none",
    crm: "none",
    ...over,
  };
}

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
beforeEach(() => vi.resetAllMocks());

describe("SettingsPage", () => {
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

  test("shows an admin-only message on a 403", async () => {
    vi.mocked(listPeople).mockRejectedValue(new HostRefusedError(403, "/v1/settings/people", null));
    renderPage();

    expect(await screen.findByText("Only admins can open Settings.")).toBeTruthy();
  });
});
