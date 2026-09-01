import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, test, vi } from "vitest";

import { SidebarProvider } from "@/components/ui/sidebar";

const useSession = vi.fn();
const signOut = vi.fn();
const forgetToken = vi.fn();

vi.mock("./authClient", () => ({
  useSession: () => useSession(),
  authClient: { signOut: () => signOut() },
}));

vi.mock("./authFetch", () => ({
  forgetToken: () => forgetToken(),
}));

const { AccountMenu } = await import("./AccountMenu");

/** `window.location.replace` cannot be called for real in a test: it would tear down the document. */
function stubNavigation() {
  const replace = vi.fn();
  vi.spyOn(window, "location", "get").mockReturnValue({
    ...window.location,
    replace,
  } as unknown as Location);
  return replace;
}

let replace: ReturnType<typeof stubNavigation>;

function show() {
  return render(
    <SidebarProvider>
      <AccountMenu />
    </SidebarProvider>,
  );
}

/** Radix opens on a key press as readily as on a pointer, and happy-dom has no pointer. */
function openMenu() {
  fireEvent.keyDown(screen.getByRole("button"), { key: "Enter" });
}

beforeEach(() => {
  replace = stubNavigation();
  signOut.mockResolvedValue(undefined);
});

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
  vi.restoreAllMocks();
});

describe("AccountMenu", () => {
  test("shows the signed-in person on the row", () => {
    useSession.mockReturnValue({
      data: { user: { name: "Matthew Hsu", email: "it@soletreadmills.com" } },
    });

    show();

    expect(screen.getAllByText("Matthew Hsu").length).toBeGreaterThan(0);
    expect(screen.getAllByText("it@soletreadmills.com").length).toBeGreaterThan(0);
  });

  test("falls back to the email when the account has no name", () => {
    useSession.mockReturnValue({
      data: { user: { name: "", email: "it@soletreadmills.com" } },
    });

    show();

    // Both lines read the same address rather than one of them reading blank.
    expect(screen.getAllByText("it@soletreadmills.com").length).toBe(2);
  });

  test("draws nothing while the session is still being read", () => {
    useSession.mockReturnValue({ data: null, isPending: true });

    const { container } = show();

    expect(container.querySelector("[data-slot=sidebar-menu]")).toBeNull();
  });

  test("signs out, drops the cached token and leaves for the login page", async () => {
    useSession.mockReturnValue({
      data: { user: { name: "Matthew Hsu", email: "it@soletreadmills.com" } },
    });

    show();
    openMenu();

    fireEvent.click(await screen.findByText("Log out"));

    await waitFor(() => expect(replace).toHaveBeenCalledWith("/chat/login.html"));
    expect(signOut).toHaveBeenCalled();
    // A token minted for the old session must not sign the next one's requests.
    expect(forgetToken).toHaveBeenCalled();
  });

  test("still leaves when Neon refuses the sign-out", async () => {
    useSession.mockReturnValue({
      data: { user: { name: "Matthew Hsu", email: "it@soletreadmills.com" } },
    });
    signOut.mockRejectedValue(new Error("network"));
    vi.spyOn(console, "error").mockImplementation(() => {});

    show();
    openMenu();

    fireEvent.click(await screen.findByText("Log out"));

    // The login page sends a live session back to the app, so the failure surfaces there instead
    // of leaving a button that silently does nothing.
    await waitFor(() => expect(replace).toHaveBeenCalledWith("/chat/login.html"));
  });
});
