import { act, cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, test, vi } from "vitest";

/**
 * The generated client is mocked rather than the network under it, the same way `UnitPanel.test.tsx`
 * mocks `@/api/sdk.gen`. `authClient` is mocked so a sign-out can be watched without a real Neon
 * project, with a fixed signed-in user for the banner's account menu.
 */
vi.mock("@/api/sdk.gen", () => ({ listHubApps: vi.fn(), openHubApp: vi.fn() }));
vi.mock("../auth/authClient", () => ({
  authClient: { signOut: vi.fn() },
  useSession: () => ({ data: { user: { name: "Ada Lovelace", email: "ada@spirit.test" } } }),
}));

const { listHubApps, openHubApp } = await import("@/api/sdk.gen");
const { authClient } = await import("../auth/authClient");

const { HubPage } = await import("./HubPage");

type Tile = { id: string; name: string; url: string };

const ALL_TILES: Record<string, Tile> = {
  chat: { id: "chat", name: "Chat", url: "/chat/" },
  desk: { id: "desk", name: "Desk", url: "https://desk.spirit.test/app" },
  crm: { id: "crm", name: "CRM", url: "https://crm.spirit.test/" },
};

function tiles(...ids: Array<keyof typeof ALL_TILES>): Tile[] {
  return ids.map((id) => ALL_TILES[id]);
}

function serveTiles(list: Tile[]) {
  vi.mocked(listHubApps).mockResolvedValue({ data: { tiles: list } } as never);
}

function postToHub(data: unknown, origin = window.location.origin) {
  act(() => {
    window.dispatchEvent(new MessageEvent("message", { data, origin }));
  });
}

async function openTile(name: string) {
  fireEvent.click(await screen.findByText(name));
}

/** `window.location.replace` cannot be called for real in a test: it would tear down the document. */
function stubNavigation() {
  const replace = vi.fn();
  vi.spyOn(window, "location", "get").mockReturnValue({
    origin: window.location.origin,
    pathname: "/",
    search: "",
    hash: "",
    replace,
  } as unknown as Location);
  return replace;
}

async function openAccountMenuAndSignOut() {
  fireEvent.keyDown(screen.getByRole("button", { name: "Account" }), { key: "Enter" });
  fireEvent.click(await screen.findByText("Sign out"));
}

beforeEach(() => {
  vi.resetAllMocks();
  vi.mocked(authClient.signOut).mockResolvedValue(undefined as never);
  window.history.replaceState(null, "", "/");
  window.localStorage.clear();
});

afterEach(() => {
  cleanup();
  vi.restoreAllMocks();
  vi.useRealTimers();
});

describe("HubPage", () => {
  test("names the signed-in person in the account menu", async () => {
    serveTiles(tiles("chat"));
    render(<HubPage />);

    fireEvent.keyDown(screen.getByRole("button", { name: "Account" }), { key: "Enter" });

    expect(await screen.findByText("Ada Lovelace")).toBeTruthy();
    expect(screen.getByText("ada@spirit.test")).toBeTruthy();
  });

  test("draws only the tiles the server returns", async () => {
    serveTiles(tiles("chat", "desk"));

    render(<HubPage />);

    expect(await screen.findByText("Desk")).toBeTruthy();
    expect(screen.getByText("Chat")).toBeTruthy();
    expect(screen.queryByText("CRM")).toBeNull();
    expect(screen.queryByText("Settings")).toBeNull();
  });

  test("preloads the Desk frame at start so its alerts work before it is opened", async () => {
    serveTiles(tiles("chat", "desk"));

    render(<HubPage />);

    const frame = (await screen.findByTitle("Desk")) as HTMLIFrameElement;
    expect(frame.src).toBe("https://desk.spirit.test/app");
    expect(openHubApp).not.toHaveBeenCalled();
    // The drawer is still what's on screen — preloading is not the same as opening.
    expect(screen.getByRole("heading", { name: "Spirit Hub" })).toBeTruthy();
  });

  test("opens Desk at its home page, not a sign-in link", async () => {
    serveTiles(tiles("chat", "desk"));
    render(<HubPage />);

    await openTile("Desk");

    const frame = (await screen.findByTitle("Desk")) as HTMLIFrameElement;
    expect(frame.src).toBe("https://desk.spirit.test/app");
    expect(openHubApp).not.toHaveBeenCalled();
  });

  test("loads a one-time link when the Desk frame asks to sign in", async () => {
    serveTiles(tiles("chat", "desk"));
    vi.mocked(openHubApp).mockResolvedValue({
      data: { url: "https://desk.spirit.test/sign-in/tok-1" },
    } as never);
    render(<HubPage />);
    await openTile("Desk");
    await screen.findByTitle("Desk");

    postToHub({ type: "hub:needs-sign-in", app: "desk" });

    await waitFor(() =>
      expect(openHubApp).toHaveBeenCalledWith({ throwOnError: true, path: { app: "desk" } }),
    );
    await waitFor(() => {
      const frame = screen.getByTitle("Desk") as HTMLIFrameElement;
      expect(frame.src).toBe("https://desk.spirit.test/sign-in/tok-1");
    });
  });

  test("ignores sign-in requests from another origin", async () => {
    serveTiles(tiles("chat", "desk"));
    render(<HubPage />);
    await openTile("Desk");
    await screen.findByTitle("Desk");

    postToHub({ type: "hub:needs-sign-in", app: "desk" }, "https://evil.test");

    await new Promise((resolve) => setTimeout(resolve, 0));
    expect(openHubApp).not.toHaveBeenCalled();
  });

  test("ignores a second sign-in request for the same app while the first link is still loading", async () => {
    serveTiles(tiles("chat", "desk"));
    let resolveLink!: (value: unknown) => void;
    vi.mocked(openHubApp).mockReturnValue(
      new Promise((resolve) => {
        resolveLink = resolve;
      }) as never,
    );
    render(<HubPage />);
    await openTile("Desk");
    await screen.findByTitle("Desk");

    postToHub({ type: "hub:needs-sign-in", app: "desk" });
    postToHub({ type: "hub:needs-sign-in", app: "desk" });

    expect(openHubApp).toHaveBeenCalledTimes(1);
    expect(screen.queryByText("Could not sign in to Desk")).toBeNull();

    // Settles the pending request so it does not resolve after the test (and its `act`) has ended.
    await act(async () => {
      resolveLink({ data: { url: "https://desk.spirit.test/sign-in/tok-1" } });
    });
  });

  test("stops after a second request right after a link and says so", async () => {
    serveTiles(tiles("chat", "desk"));
    vi.mocked(openHubApp).mockResolvedValue({
      data: { url: "https://desk.spirit.test/sign-in/tok-1" },
    } as never);
    const now = vi.spyOn(Date, "now").mockReturnValue(0);

    render(<HubPage />);
    await openTile("Desk");
    await screen.findByTitle("Desk");

    postToHub({ type: "hub:needs-sign-in", app: "desk" });
    await waitFor(() => expect(openHubApp).toHaveBeenCalledTimes(1));

    // A second request one second after the link loaded — well inside the 10 s window.
    now.mockReturnValue(1000);
    postToHub({ type: "hub:needs-sign-in", app: "desk" });

    expect(await screen.findByText("Could not sign in to Desk")).toBeTruthy();
    expect(openHubApp).toHaveBeenCalledTimes(1);
    expect(screen.getByRole("button", { name: "Try again" })).toBeTruthy();
  });

  test("Try again clears the guard and asks for a fresh link", async () => {
    serveTiles(tiles("chat", "desk"));
    vi.mocked(openHubApp).mockResolvedValueOnce({
      data: { url: "https://desk.spirit.test/sign-in/tok-1" },
    } as never);
    const now = vi.spyOn(Date, "now").mockReturnValue(0);

    render(<HubPage />);
    await openTile("Desk");
    await screen.findByTitle("Desk");

    postToHub({ type: "hub:needs-sign-in", app: "desk" });
    await waitFor(() => expect(openHubApp).toHaveBeenCalledTimes(1));

    now.mockReturnValue(1000);
    postToHub({ type: "hub:needs-sign-in", app: "desk" });
    await screen.findByText("Could not sign in to Desk");

    vi.mocked(openHubApp).mockResolvedValueOnce({
      data: { url: "https://desk.spirit.test/sign-in/tok-2" },
    } as never);
    fireEvent.click(screen.getByRole("button", { name: "Try again" }));

    await waitFor(() => expect(openHubApp).toHaveBeenCalledTimes(2));
    expect(screen.queryByText("Could not sign in to Desk")).toBeNull();
    await waitFor(() => {
      const frame = screen.getByTitle("Desk") as HTMLIFrameElement;
      expect(frame.src).toBe("https://desk.spirit.test/sign-in/tok-2");
    });
  });

  test("signs out of each open app before Spirit, then returns to the Hub", async () => {
    const replace = stubNavigation();
    serveTiles(tiles("chat", "desk"));
    render(<HubPage />);
    await openTile("Desk");
    await screen.findByTitle("Desk");

    await openAccountMenuAndSignOut();

    await waitFor(() => {
      const frame = screen.getByTitle("Desk") as HTMLIFrameElement;
      expect(frame.src).toBe("https://desk.spirit.test/spirit/sign-out");
    });
    expect(authClient.signOut).not.toHaveBeenCalled();

    postToHub({ type: "hub:signed-out", app: "desk" }, "https://desk.spirit.test");

    await waitFor(() => expect(authClient.signOut).toHaveBeenCalled());
    await waitFor(() => expect(replace).toHaveBeenCalledWith("/chat/login.html?returnTo=%2F"));
  });

  test("signs out of Spirit after 3 s even if an app never answers", async () => {
    stubNavigation();
    serveTiles(tiles("chat", "desk"));
    render(<HubPage />);
    await openTile("Desk");
    await screen.findByTitle("Desk");
    fireEvent.keyDown(screen.getByRole("button", { name: "Account" }), { key: "Enter" });
    await screen.findByText("Sign out");

    vi.useFakeTimers();
    fireEvent.click(screen.getByText("Sign out"));
    act(() => {
      vi.advanceTimersByTime(3000);
    });

    expect(authClient.signOut).toHaveBeenCalled();
  });

  test("ignores hub:signed-out from the wrong origin, and still ends by the 3 s timer", async () => {
    stubNavigation();
    serveTiles(tiles("chat", "desk"));
    render(<HubPage />);
    await openTile("Desk");
    await screen.findByTitle("Desk");
    fireEvent.keyDown(screen.getByRole("button", { name: "Account" }), { key: "Enter" });
    await screen.findByText("Sign out");

    vi.useFakeTimers();
    fireEvent.click(screen.getByText("Sign out"));

    const frame = screen.getByTitle("Desk") as HTMLIFrameElement;
    expect(frame.src).toBe("https://desk.spirit.test/spirit/sign-out");

    act(() => {
      window.dispatchEvent(
        new MessageEvent("message", {
          data: { type: "hub:signed-out", app: "desk" },
          origin: "https://evil.test",
        }),
      );
    });
    expect(authClient.signOut).not.toHaveBeenCalled();

    act(() => {
      vi.advanceTimersByTime(3000);
    });
    expect(authClient.signOut).toHaveBeenCalled();
  });

  test("still leaves for a fresh sign-in returning to the Hub when Neon refuses the sign-out", async () => {
    const replace = stubNavigation();
    vi.mocked(authClient.signOut).mockRejectedValueOnce(new Error("network"));
    vi.spyOn(console, "error").mockImplementation(() => {});
    serveTiles(tiles("chat"));
    render(<HubPage />);
    await screen.findByText("Chat");

    await openAccountMenuAndSignOut();

    await waitFor(() => expect(replace).toHaveBeenCalledWith("/chat/login.html?returnTo=%2F"));
  });

  test("Escape closes the drawer back to the app that was open", async () => {
    serveTiles(tiles("chat", "desk"));
    render(<HubPage />);
    await openTile("Desk");
    await screen.findByTitle("Desk");

    fireEvent.click(screen.getByRole("button", { name: "Apps" }));
    expect(screen.getByRole("heading", { name: "Spirit Hub" })).toBeTruthy();

    fireEvent.keyDown(window, { key: "Escape" });

    await waitFor(() => expect(screen.queryByRole("heading", { name: "Spirit Hub" })).toBeNull());
  });

  test("keeps the last app in the address while the drawer is open, for F5 to return to it", async () => {
    serveTiles(tiles("chat", "desk"));
    render(<HubPage />);
    await openTile("Desk");
    await screen.findByTitle("Desk");

    fireEvent.click(screen.getByRole("button", { name: "Apps" }));

    await waitFor(() => expect(window.location.hash).toBe("#desk"));
  });

  test("opens the drawer when the address names a tile the Person does not have", async () => {
    window.history.replaceState(null, "", "/#crm");
    serveTiles(tiles("chat", "desk"));

    render(<HubPage />);

    expect(await screen.findByText("Chat")).toBeTruthy();
    expect(screen.getByText("Desk")).toBeTruthy();
    expect(screen.queryByTitle("CRM")).toBeNull();
  });

  test("dismisses the Desk alert hint once Desk is opened in a new tab", async () => {
    serveTiles(tiles("chat", "desk"));
    render(<HubPage />);

    const link = await screen.findByRole("link", { name: /open desk/i });
    fireEvent.click(link);

    expect(screen.queryByText(/Turn on alerts/)).toBeNull();
  });

  test("shows a retry when the tile list fails to load", async () => {
    vi.mocked(listHubApps).mockRejectedValueOnce(new Error("network"));

    render(<HubPage />);

    expect(await screen.findByText("Could not load your apps.")).toBeTruthy();

    serveTiles(tiles("chat", "desk"));
    fireEvent.click(screen.getByRole("button", { name: "Try again" }));

    expect(await screen.findByText("Desk")).toBeTruthy();
  });
});
