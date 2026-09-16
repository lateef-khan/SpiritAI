import { cleanup, render, screen, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, test, vi } from "vitest";

const useSession = vi.fn();

vi.mock("./authClient", () => ({
  useSession: () => useSession(),
  authClient: {},
}));

const { AuthGate } = await import("./AuthGate");

afterEach(() => {
  cleanup();
  vi.restoreAllMocks();
});

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

beforeEach(() => {
  replace = stubNavigation();
});

describe("AuthGate", () => {
  test("sends a signed-out visitor to the login page", async () => {
    useSession.mockReturnValue({ data: null, isPending: false });

    render(
      <AuthGate>
        <p>the app</p>
      </AuthGate>,
    );

    await waitFor(() => expect(replace).toHaveBeenCalledWith("/chat/login.html"));
    expect(screen.queryByText("the app")).toBeNull();
  });

  test("waits out the pending session rather than flashing the login page", () => {
    useSession.mockReturnValue({ data: null, isPending: true });

    render(
      <AuthGate>
        <p>the app</p>
      </AuthGate>,
    );

    // The session is a cookie the client still has to check with Neon. Redirecting here would
    // throw a signed-in person back to the door on every reload.
    expect(replace).not.toHaveBeenCalled();
    expect(screen.queryByText("the app")).toBeNull();
  });

  test("renders the app for a signed-in visitor", () => {
    useSession.mockReturnValue({ data: { user: {} }, isPending: false });

    render(
      <AuthGate>
        <p>the app</p>
      </AuthGate>,
    );

    expect(screen.getByText("the app")).toBeTruthy();
    expect(replace).not.toHaveBeenCalled();
  });
});
