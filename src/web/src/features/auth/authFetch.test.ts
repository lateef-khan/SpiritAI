import { afterEach, beforeEach, describe, expect, test, vi } from "vitest";

const getSession = vi.fn();
const token = vi.fn();

vi.mock("./authClient", () => ({
  authClient: { getSession: () => getSession(), token: () => token() },
  useSession: () => ({ data: null, isPending: false }),
}));

const { authFetch, forgetToken, NotSignedInError } = await import("./authFetch");

const LIVE = { data: { session: { token: "jwt-abc" } }, error: null };
const LIVE_WITHOUT_TOKEN = { data: { session: {} }, error: null };
const SIGNED_OUT = { data: null, error: null };

let fetchMock: ReturnType<typeof vi.fn>;
let replace: ReturnType<typeof vi.fn>;

beforeEach(() => {
  forgetToken();
  token.mockResolvedValue({ data: null, error: null });

  fetchMock = vi.fn().mockResolvedValue(new Response("ok", { status: 200 }));
  vi.stubGlobal("fetch", fetchMock);

  // Calling the real one would tear down the test document.
  replace = vi.fn();
  vi.spyOn(window, "location", "get").mockReturnValue({
    ...window.location,
    replace,
  } as unknown as Location);
});

afterEach(() => {
  vi.unstubAllGlobals();
  vi.restoreAllMocks();
  getSession.mockReset();
  token.mockReset();
});

/** The header the host reads, on the nth request sent. */
function sentAuthorization(call = 0): string | null {
  const init = fetchMock.mock.calls[call]?.[1] as RequestInit | undefined;
  return new Headers(init?.headers).get("Authorization");
}

/** An unsigned JWT whose `exp` is the given number of seconds from now. */
function jwtExpiringIn(seconds: number): string {
  const b64 = (o: object) => btoa(JSON.stringify(o)).replace(/=+$/, "");
  const exp = Math.floor(Date.now() / 1000) + seconds;
  return `${b64({ alg: "EdDSA" })}.${b64({ exp })}.sig`;
}

const live = (jwt: string) => ({ data: { session: { token: jwt } }, error: null });

describe("authFetch", () => {
  test("signs the request with the session's token", async () => {
    getSession.mockResolvedValue(LIVE);

    await authFetch("/v1/responses", { method: "POST" });

    expect(sentAuthorization()).toBe("Bearer jwt-abc");
  });

  test("reuses the token instead of asking Neon per turn", async () => {
    getSession.mockResolvedValue(LIVE);

    await authFetch("/v1/responses");
    await authFetch("/v1/responses");

    expect(getSession).toHaveBeenCalledTimes(1);
    expect(fetchMock).toHaveBeenCalledTimes(2);
  });

  test("drops a token before its own exp, not ten minutes after fetching it", async () => {
    // Neon's session cache hands back the token it holds, however little life it has left.
    vi.useFakeTimers();
    getSession.mockResolvedValueOnce(live(jwtExpiringIn(60))).mockResolvedValue(live("jwt-new"));

    await authFetch("/v1/responses");
    vi.advanceTimersByTime(45 * 1000);
    await authFetch("/v1/responses");

    expect(sentAuthorization(1)).toBe("Bearer jwt-new");
    vi.useRealTimers();
  });

  test("a 401 on a cached token is retried once with a fresh one", async () => {
    getSession.mockResolvedValueOnce(LIVE).mockResolvedValue(live("jwt-new"));
    fetchMock
      .mockResolvedValueOnce(new Response("", { status: 401 }))
      .mockResolvedValue(new Response("ok", { status: 200 }));

    const response = await authFetch("/v1/responses", { method: "POST", body: "{}" });

    expect(response.status).toBe(200);
    expect(sentAuthorization(0)).toBe("Bearer jwt-abc");
    expect(sentAuthorization(1)).toBe("Bearer jwt-new");
    expect(replace).not.toHaveBeenCalled();
  });

  test("never sends an unsigned request", async () => {
    // The bug this file exists to prevent: a turn that goes out with no header, is refused, and
    // reads to the user as the page reloading.
    getSession.mockResolvedValue(LIVE_WITHOUT_TOKEN);

    await expect(authFetch("/v1/responses")).rejects.toBeInstanceOf(NotSignedInError);
    expect(fetchMock).not.toHaveBeenCalled();
    expect(replace).not.toHaveBeenCalled();
  });

  test("goes to the door only when the session is really gone", async () => {
    getSession.mockResolvedValue(SIGNED_OUT);

    await expect(authFetch("/v1/responses")).rejects.toBeInstanceOf(NotSignedInError);
    expect(replace).toHaveBeenCalledWith("/chat/login.html");
  });

  test("a 401 on a live session fails the turn without navigating", async () => {
    getSession.mockResolvedValue(LIVE);
    fetchMock.mockResolvedValue(new Response("", { status: 401 }));

    await expect(authFetch("/v1/responses")).rejects.toBeInstanceOf(NotSignedInError);

    // Navigating here is what made every message reload the page: the login page saw a live
    // session and sent the browser straight back.
    expect(replace).not.toHaveBeenCalled();
  });

  test("a 401 with a dead session goes to the door", async () => {
    getSession.mockResolvedValueOnce(LIVE).mockResolvedValue(SIGNED_OUT);
    fetchMock.mockResolvedValue(new Response("", { status: 401 }));

    await expect(authFetch("/v1/responses")).rejects.toBeInstanceOf(NotSignedInError);
    expect(replace).toHaveBeenCalledWith("/chat/login.html");
  });

  test("falls back to the token endpoint when the session carries none", async () => {
    getSession.mockResolvedValue(LIVE_WITHOUT_TOKEN);
    token.mockResolvedValue({ data: { token: "jwt-from-endpoint" }, error: null });

    await authFetch("/v1/responses");

    expect(sentAuthorization()).toBe("Bearer jwt-from-endpoint");
  });

  test("passes the caller's own headers and body through", async () => {
    getSession.mockResolvedValue(LIVE);

    await authFetch("/v1/responses", {
      method: "POST",
      headers: { "Content-Type": "application/json", "X-Test-Header": "s1" },
      body: "{}",
    });

    const init = fetchMock.mock.calls[0][1] as RequestInit;
    const headers = new Headers(init.headers);

    expect(headers.get("Content-Type")).toBe("application/json");
    expect(headers.get("X-Test-Header")).toBe("s1");
    expect(init.body).toBe("{}");
  });
});

describe("authFetch given a Request", () => {
  test("keeps the headers the Request already carries", async () => {
    // The generated client builds a Request and hands it over with no init. Headers passed as
    // init replace the Request's own, so the token has to join them rather than stand alone.
    getSession.mockResolvedValue(LIVE);

    const request = new Request("http://localhost/v1/threads/t1", {
      method: "PATCH",
      headers: { "Content-Type": "application/json" },
      body: "{}",
    });

    await authFetch(request);

    const init = fetchMock.mock.calls[0][1] as RequestInit;
    const headers = new Headers(init.headers);

    expect(headers.get("Content-Type")).toBe("application/json");
    expect(headers.get("Authorization")).toBe("Bearer jwt-abc");
  });
});
