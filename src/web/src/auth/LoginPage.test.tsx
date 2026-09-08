import { cleanup, render, screen, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, test, vi } from "vitest";

// Both mocks stand in for the network. `authClient` additionally reads VITE_NEON_AUTH_URL at
// import time and throws without it, which would take the whole suite down on a machine that has
// no .env — a test of the page's markup should not depend on a Neon project existing.
const useSession = vi.fn();
const sendMagicLink = vi.fn();

vi.mock("./authClient", () => ({
  useSession: () => useSession(),
  authClient: {},
}));
vi.mock("./magicLink", () => ({
  sendMagicLink: (email: string) => sendMagicLink(email),
  MagicLinkError: class extends Error {},
}));

const { LoginPage } = await import("./LoginPage");

// Testing-library only registers its own cleanup when vitest runs with `globals: true`. It does
// not here, so each test would otherwise leave its DOM in the body.
afterEach(cleanup);

beforeEach(() => {
  useSession.mockReturnValue({ data: null, isPending: false });
  sendMagicLink.mockResolvedValue(undefined);
});

/** Types an address into the one field and submits, the way a person would. */
function signInAs(address: string) {
  const field = screen.getByLabelText("Email") as HTMLInputElement;

  // `user-event` is not a dependency here, and the field is controlled, so the change has to be
  // dispatched through React's own value setter rather than by assigning `.value`.
  const setter = Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, "value")!.set!;
  setter.call(field, address);
  field.dispatchEvent(new Event("input", { bubbles: true }));

  screen.getByRole("button", { name: /send sign-in link/i }).click();
}

describe("LoginPage", () => {
  test("offers no way to sign up or use a password", () => {
    render(<LoginPage />);

    expect(screen.queryByText(/sign up/i)).toBeNull();
    expect(screen.queryByLabelText(/password/i)).toBeNull();
    expect(screen.getAllByRole("textbox")).toHaveLength(1);
  });

  test("confirms without claiming the address is allowed in", async () => {
    render(<LoginPage />);
    signInAs("someone@example.com");

    expect(await screen.findByText("Check your email")).toBeTruthy();
    expect(sendMagicLink).toHaveBeenCalledWith("someone@example.com");

    // The wording is the whole point of this test: a configured address and an unconfigured one
    // must be indistinguishable from here.
    expect(screen.getByText(/can sign in, a link is on its way/i)).toBeTruthy();
    expect(screen.getByText("someone@example.com")).toBeTruthy();
  });

  test("blocks an immediate resend", async () => {
    render(<LoginPage />);
    signInAs("someone@example.com");

    const resend = await screen.findByRole("button", { name: /resend in \d+s/i });
    expect((resend as HTMLButtonElement).disabled).toBe(true);
  });

  test("surfaces a failed request on the form", async () => {
    sendMagicLink.mockRejectedValue(new Error("Could not reach the sign-in service."));

    render(<LoginPage />);
    signInAs("someone@example.com");

    const alert = await screen.findByRole("alert");
    expect(alert.textContent).toContain("Could not reach the sign-in service.");
    // The form stays put so the address can be retried.
    expect(screen.getByLabelText("Email")).toBeTruthy();
  });

  test("pushes a signed-in visitor to the app instead of the form", async () => {
    const replace = vi.fn();
    vi.spyOn(window, "location", "get").mockReturnValue({
      ...window.location,
      replace,
    } as unknown as Location);

    useSession.mockReturnValue({ data: { user: {} }, isPending: false });
    render(<LoginPage />);

    await waitFor(() => expect(replace).toHaveBeenCalledWith("/chat/"));
    expect(screen.queryByLabelText("Email")).toBeNull();

    vi.restoreAllMocks();
  });
});
