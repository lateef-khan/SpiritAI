import { beforeEach, describe, expect, test, vi } from "vitest";

const sendVerificationOtp = vi.fn();
const signInEmailOtp = vi.fn();

vi.mock("./authClient", () => ({
  authClient: {
    emailOtp: { sendVerificationOtp: (a: unknown) => sendVerificationOtp(a) },
    signIn: { emailOtp: (a: unknown) => signInEmailOtp(a) },
  },
}));

const { sendCode, verifyCode, EmailCodeError } = await import("./emailCode");

/** The shape the Neon client throws: an Error carrying the HTTP status. */
function authApiError(status: number) {
  return Object.assign(new Error(`HTTP ${status}`), { status, __isAuthError: true });
}

beforeEach(() => {
  vi.resetAllMocks();
  sendVerificationOtp.mockResolvedValue({ data: null, error: null });
  signInEmailOtp.mockResolvedValue({ data: { user: {} }, error: null });
});

describe("sendCode", () => {
  test("asks Neon for a sign-in code for the address", async () => {
    await sendCode("someone@example.com");

    expect(sendVerificationOtp).toHaveBeenCalledWith({
      email: "someone@example.com",
      type: "sign-in",
    });
  });

  test("stays quiet when Neon refuses the address", async () => {
    sendVerificationOtp.mockRejectedValue(authApiError(403));

    // Resolving is the point: a refused address must look exactly like an accepted one.
    await expect(sendCode("stranger@example.com")).resolves.toBeUndefined();
  });

  test("names rate limiting, which the person can act on", async () => {
    sendVerificationOtp.mockRejectedValue(authApiError(429));

    await expect(sendCode("someone@example.com")).rejects.toThrow(/wait a minute/i);
  });

  test("reports a service failure rather than swallowing it", async () => {
    sendVerificationOtp.mockRejectedValue(authApiError(500));

    await expect(sendCode("someone@example.com")).rejects.toBeInstanceOf(EmailCodeError);
  });

  test("treats a returned error the same as a thrown one", async () => {
    sendVerificationOtp.mockResolvedValue({ data: null, error: { status: 500 } });

    await expect(sendCode("someone@example.com")).rejects.toBeInstanceOf(EmailCodeError);
  });
});

describe("verifyCode", () => {
  test("signs in with the address and the typed code", async () => {
    await verifyCode("someone@example.com", "123456");

    expect(signInEmailOtp).toHaveBeenCalledWith({
      email: "someone@example.com",
      otp: "123456",
    });
  });

  test("says a wrong code is wrong instead of hiding it", async () => {
    // The opposite of sendCode: at this point the address is already known to the person, so
    // silence would only leave them retyping a code that can never work.
    signInEmailOtp.mockRejectedValue(authApiError(400));

    await expect(verifyCode("someone@example.com", "000000")).rejects.toThrow(
      /code is wrong or has expired/i,
    );
  });

  test("does not leak the client's raw HTTP wording", async () => {
    signInEmailOtp.mockRejectedValue(authApiError(400));

    await expect(verifyCode("someone@example.com", "000000")).rejects.not.toThrow(/HTTP 400/);
  });

  test("names rate limiting on the code too", async () => {
    signInEmailOtp.mockRejectedValue(authApiError(429));

    await expect(verifyCode("someone@example.com", "000000")).rejects.toThrow(/wait a minute/i);
  });
});
