import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import type { ReactElement } from "react";
import { afterEach, beforeEach, expect, test, vi } from "vitest";

import { person } from "./settingsFixtures";

vi.mock("@/api/sdk.gen", () => ({
  banPerson: vi.fn(),
  unbanPerson: vi.fn(),
  deletePerson: vi.fn(),
  unlinkPerson: vi.fn(),
}));

const { banPerson, unbanPerson, deletePerson } = await import("@/api/sdk.gen");
const { BanDialog } = await import("./BanDialog");
const { UnbanDialog } = await import("./UnbanDialog");
const { DeleteDialog } = await import("./DeleteDialog");
const { UnlinkDialog } = await import("./UnlinkDialog");

function renderWith(ui: ReactElement) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(<QueryClientProvider client={queryClient}>{ui}</QueryClientProvider>);
}

afterEach(cleanup);
beforeEach(() => vi.resetAllMocks());

test("Ban is one call, with the reason", async () => {
  vi.mocked(banPerson).mockResolvedValue({
    data: { person: person({ banned: true }), desk: "none", detail: null },
  } as never);
  renderWith(<BanDialog person={person()} open onOpenChange={() => {}} />);

  fireEvent.click(screen.getByRole("button", { name: "Ban" }));

  await waitFor(() =>
    expect(banPerson).toHaveBeenCalledWith(
      expect.objectContaining({
        path: { id: person().id },
        body: { reason: "Left Spirit (Settings)" },
      }),
    ),
  );
  expect(banPerson).toHaveBeenCalledTimes(1);
});

test("Ban shows a failed Desk step with its reason, and Try again sends Ban again", async () => {
  vi.mocked(banPerson)
    .mockResolvedValueOnce({
      data: {
        person: person({ banned: true, desk: "ready" }),
        desk: "failed",
        detail: "Desk did not answer. Press Ban again to finish.",
      },
    } as never)
    .mockResolvedValueOnce({
      data: { person: person({ banned: true, desk: "unfinished" }), desk: "done", detail: null },
    } as never);
  const closed = vi.fn();
  renderWith(<BanDialog person={person({ desk: "ready" })} open onOpenChange={closed} />);

  fireEvent.click(screen.getByRole("button", { name: "Ban" }));

  expect(await screen.findByText("Desk did not answer. Press Ban again to finish.")).toBeTruthy();
  expect(stateOf("Ban")).toBe("done");
  expect(stateOf("Leave Desk")).toBe("failed");

  fireEvent.click(screen.getByRole("button", { name: "Try again" }));

  await waitFor(() => expect(closed).toHaveBeenCalledWith(false));
  expect(banPerson).toHaveBeenCalledTimes(2);
});

test("Unban shows a failed Desk step, and Try again sends Unban again", async () => {
  vi.mocked(unbanPerson)
    .mockResolvedValueOnce({
      data: {
        person: person({ desk: "unfinished" }),
        desk: "failed",
        detail: "Desk did not answer. Press Unban again to finish.",
      },
    } as never)
    .mockResolvedValueOnce({
      data: { person: person({ desk: "ready" }), desk: "done", detail: null },
    } as never);
  const closed = vi.fn();
  renderWith(
    <UnbanDialog
      person={person({ banned: true, desk: "unfinished" })}
      open
      onOpenChange={closed}
    />,
  );

  fireEvent.click(screen.getByRole("button", { name: "Unban" }));

  expect(await screen.findByText("Desk did not answer. Press Unban again to finish.")).toBeTruthy();
  expect(stateOf("Rejoin Desk")).toBe("failed");

  fireEvent.click(screen.getByRole("button", { name: "Try again" }));

  await waitFor(() => expect(closed).toHaveBeenCalledWith(false));
  expect(unbanPerson).toHaveBeenCalledTimes(2);
});

test("Delete stays disabled until the email is typed, in any case", async () => {
  renderWith(
    <DeleteDialog person={person({ email: "dana@example.com" })} open onOpenChange={() => {}} />,
  );
  const go = screen.getByRole("button", { name: "Delete for good" });

  expect(go.hasAttribute("disabled")).toBe(true);
  fireEvent.change(screen.getByLabelText(/to confirm/), {
    target: { value: " Dana@Example.com " },
  });
  expect(go.hasAttribute("disabled")).toBe(false);
});

function stateOf(label: string) {
  return screen.getByText(label).closest("li")?.getAttribute("data-state");
}

function confirmDelete() {
  fireEvent.change(screen.getByLabelText(/to confirm/), { target: { value: "dana@example.com" } });
  fireEvent.click(screen.getByRole("button", { name: "Delete for good" }));
}

test("Delete is one call, and shows each step the server ran, with the reason on the first failure", async () => {
  vi.mocked(deletePerson).mockResolvedValue({
    data: { desk: "done", crm: "failed", neon: "failed", detail: "CRM did not answer." },
  } as never);
  renderWith(<DeleteDialog person={person()} open onOpenChange={() => {}} />);

  confirmDelete();

  expect(await screen.findByText("CRM did not answer.")).toBeTruthy();
  expect(stateOf("Leave Desk")).toBe("done");
  expect(stateOf("Delete in CRM")).toBe("failed");
  expect(screen.getByText("Delete in CRM").closest("li")?.textContent).toContain(
    "CRM did not answer.",
  );
  expect(stateOf("Delete the Spirit sign-in")).not.toBe("done");
  expect(deletePerson).toHaveBeenCalledTimes(1);
});

test("Delete's Try again calls the server again, and closes once every step is done", async () => {
  vi.mocked(deletePerson)
    .mockResolvedValueOnce({
      data: { desk: "done", crm: "failed", neon: "failed", detail: "CRM did not answer." },
    } as never)
    .mockResolvedValueOnce({
      data: { desk: "done", crm: "done", neon: "done", detail: null },
    } as never);
  const onOpenChange = vi.fn();
  renderWith(<DeleteDialog person={person()} open onOpenChange={onOpenChange} />);

  confirmDelete();
  fireEvent.click(await screen.findByRole("button", { name: "Try again" }));

  await waitFor(() => expect(onOpenChange).toHaveBeenCalledWith(false));
  expect(deletePerson).toHaveBeenCalledTimes(2);
});

test("Unlink CRM warns that records lose their owner", () => {
  renderWith(
    <UnlinkDialog person={person({ crm: "ready" })} app="crm" open onOpenChange={() => {}} />,
  );

  expect(screen.getByText(/records lose their owner/)).toBeTruthy();
});

test("Delete says the Chatwoot user stays and only leaves the Spirit account", () => {
  renderWith(<DeleteDialog person={person()} open onOpenChange={() => {}} />);

  expect(
    screen.getByText(
      "Desk: they leave the Spirit account. Their Chatwoot user stays, so old messages keep their name.",
    ),
  ).toBeTruthy();
});
