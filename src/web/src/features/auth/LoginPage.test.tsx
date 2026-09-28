import { cleanup, render, screen, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, test, vi } from "vitest";

// Both mocks stand in for the network. `authClient` additionally reads VITE_NEON_AUTH_URL at
// import time and throws without it, which would take the whole suite down on a machine that has
// no .env — a test of the page's markup should not depend on a Neon project existing.
const useSession = vi.fn();
const sendCode = vi.fn();
const verifyCode = vi.fn();

vi.mock("./authClient", () => ({
  useSession: () => useSession(),
  authClient: {},
}));
vi.mock("./emailCode", () => ({
  sendCode: (email: string) => sendCode(email),
  verifyCode: (email: string, code: string) => verifyCode(email, code),
  EmailCodeError: class extends Error {},
}));

const { LoginPage } = await import("./LoginPage");

// Testing-library only registers its own cleanup when vitest runs with `globals: true`. It does
// not here, so each test would otherwise leave its DOM in the body. Mock spies on `window.location`
// / `.parent` / `.top` go here too, so a test that fails before its own `restoreAllMocks` call
// cannot leak a spy into the next test.
afterEach(() => {
  cleanup();
  vi.restoreAllMocks();
});

beforeEach(() => {
  vi.resetAllMocks();
  useSession.mockReturnValue({ data: null, isPending: false });
  sendCode.mockResolvedValue(undefined);
  verifyCode.mockResolvedValue(undefined);
});

/**
 * Types into a field the way a person would. `user-event` is not a dependency here, and the fields
 * are controlled, so the change has to be dispatched through React's own value setter rather than
 * by assigning `.value`.
 */
function type(label: string, value: string) {
  const field = screen.getByLabelText(label) as HTMLInputElement;
  const setter = Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, "value")!.set!;

  setter.call(field, value);
  field.dispatchEvent(new Event("input", { bubbles: true }));
}

function askForCode(address: string) {
  type("Email", address);
  screen.getByRole("button", { name: /send sign-in code/i }).click();
}

/** Drives the page to the point where a code may be typed. */
async function atCodeStep(address = "someone@example.com") {
  askForCode(address);
  await screen.findByLabelText("Code");
}

function enterCode(code: string) {
  type("Code", code);
  screen.getByRole("button", { name: /^sign in$/i }).click();
}

/**
 * Replaces `window.location` so a navigation can be observed instead of performed.
 *
 * `Location`'s fields live on its prototype, not as the object's own properties, so `{
 * ...window.location }` copies none of them — only the ones named here are.
 */
function watchNavigation(overrides: Partial<Location> = {}) {
  const replace = vi.fn();
  vi.spyOn(window, "location", "get").mockReturnValue({
    origin: window.location.origin,
    pathname: window.location.pathname,
    search: window.location.search,
    hash: window.location.hash,
    replace,
    ...overrides,
  } as unknown as Location);

  return replace;
}

/** Puts the page in a frame, as the Hub does. `parent` need only differ from `window`. */
function frameThePage(parent: object = {}) {
  vi.spyOn(window, "parent", "get").mockReturnValue(parent as Window);
}

/** Stands in for the top window the framed page's hand-off navigates. */
function watchTopNavigation() {
  const assign = vi.fn();
  vi.spyOn(window, "top", "get").mockReturnValue({ location: { assign } } as unknown as Window);
  return assign;
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
    await atCodeStep("someone@example.com");

    expect(sendCode).toHaveBeenCalledWith("someone@example.com");

    // The wording is the whole point of this test: a configured address and an unconfigured one
    // must be indistinguishable from here.
    expect(screen.getByText(/can sign in, a code is on its way/i)).toBeTruthy();
    expect(screen.getByText("someone@example.com")).toBeTruthy();
  });

  test("blocks an immediate resend", async () => {
    render(<LoginPage />);
    await atCodeStep();

    const resend = await screen.findByRole("button", { name: /resend in \d+s/i });
    expect((resend as HTMLButtonElement).disabled).toBe(true);
  });

  test("surfaces a failed request on the address form", async () => {
    sendCode.mockRejectedValue(new Error("Could not reach the sign-in service."));

    render(<LoginPage />);
    askForCode("someone@example.com");

    const alert = await screen.findByRole("alert");
    expect(alert.textContent).toContain("Could not reach the sign-in service.");
    // The form stays put so the address can be retried.
    expect(screen.getByLabelText("Email")).toBeTruthy();
  });

  test("checks the typed code against the address it was sent to", async () => {
    render(<LoginPage />);
    await atCodeStep("someone@example.com");

    enterCode("123456");

    await waitFor(() => expect(verifyCode).toHaveBeenCalledWith("someone@example.com", "123456"));
  });

  test("keeps the code box open when the code is refused", async () => {
    verifyCode.mockRejectedValue(
      new Error("That code is wrong or has expired. Ask for a new one."),
    );

    render(<LoginPage />);
    await atCodeStep();
    enterCode("000000");

    const alert = await screen.findByRole("alert");
    expect(alert.textContent).toContain("That code is wrong or has expired.");
    // Sending them back to the address form would make them request a second code to fix a typo.
    expect(screen.getByLabelText("Code")).toBeTruthy();
    expect(screen.queryByLabelText("Email")).toBeNull();
  });

  test("leaves for the app once the code is accepted", async () => {
    const replace = watchNavigation();

    render(<LoginPage />);
    await atCodeStep();
    enterCode("123456");

    await waitFor(() => expect(replace).toHaveBeenCalledWith("/chat/"));
  });

  test("pushes a signed-in visitor to the app instead of the form", async () => {
    const replace = watchNavigation();

    useSession.mockReturnValue({ data: { user: {} }, isPending: false });
    render(<LoginPage />);

    await waitFor(() => expect(replace).toHaveBeenCalledWith("/chat/"));
    expect(screen.queryByLabelText("Email")).toBeNull();
  });

  test("returns a signed-in visitor to the page named in ?returnTo instead of the app", async () => {
    const replace = watchNavigation({ search: "?returnTo=%2F%23crm" });

    useSession.mockReturnValue({ data: { user: {} }, isPending: false });
    render(<LoginPage />);

    await waitFor(() => expect(replace).toHaveBeenCalledWith("/#crm"));
  });

  test("sends a signed-in visitor to the Hub when ?returnTo is present but empty", async () => {
    const replace = watchNavigation({ search: "?returnTo=" });

    useSession.mockReturnValue({ data: { user: {} }, isPending: false });
    render(<LoginPage />);

    await waitFor(() => expect(replace).toHaveBeenCalledWith("/"));
  });

  describe("framed inside the Hub", () => {
    test("tells the parent it needs a sign-in when the visitor already has a session", async () => {
      const postMessage = vi.fn();
      frameThePage({ postMessage });
      watchNavigation({ search: "?app=desk" });
      useSession.mockReturnValue({ data: { user: {} }, isPending: false });

      render(<LoginPage />);

      await waitFor(() =>
        expect(postMessage).toHaveBeenCalledWith(
          { type: "hub:needs-sign-in", app: "desk" },
          window.location.origin,
        ),
      );
      expect(screen.queryByLabelText("Email")).toBeNull();
    });

    test("sends the top window to a fresh sign-in when there is no session", async () => {
      frameThePage();
      const assign = watchTopNavigation();
      watchNavigation({ search: "?app=desk" });
      useSession.mockReturnValue({ data: null, isPending: false });

      render(<LoginPage />);

      await waitFor(() =>
        expect(assign).toHaveBeenCalledWith("/chat/login.html?returnTo=%2F%23desk"),
      );
      expect(screen.queryByLabelText("Email")).toBeNull();
    });

    test("sends the top window to a fresh sign-in returning to the Hub when the frame names no app the Hub knows", async () => {
      frameThePage();
      const assign = watchTopNavigation();
      watchNavigation({ search: "" });
      useSession.mockReturnValue({ data: null, isPending: false });

      render(<LoginPage />);

      await waitFor(() => expect(assign).toHaveBeenCalledWith("/chat/login.html?returnTo=%2F"));
      expect(screen.queryByLabelText("Email")).toBeNull();
    });
  });

  test("returns a signed-in visitor with ?app= to that app's tile instead of the plain app", async () => {
    const replace = watchNavigation({ search: "?app=crm" });

    useSession.mockReturnValue({ data: { user: {} }, isPending: false });
    render(<LoginPage />);

    await waitFor(() => expect(replace).toHaveBeenCalledWith("/#crm"));
  });
});
