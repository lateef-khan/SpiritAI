import { act, renderHook, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, test, vi } from "vitest";

const sendCode = vi.fn();
const verifyCode = vi.fn();

vi.mock("./emailCode", () => ({
  sendCode: (email: string) => sendCode(email),
  verifyCode: (email: string, code: string) => verifyCode(email, code),
  EmailCodeError: class extends Error {},
}));

const { useEmailCodeSignIn, RESEND_COOLDOWN_SECONDS } = await import("./useEmailCodeSignIn");

beforeEach(() => {
  vi.resetAllMocks();
  sendCode.mockResolvedValue(undefined);
  verifyCode.mockResolvedValue(undefined);
});

afterEach(() => {
  vi.useRealTimers();
});

/** Drives the hook to the point where a code may be typed. */
async function atCodeStep(address = "someone@example.com") {
  const view = renderHook(() => useEmailCodeSignIn());

  await act(async () => view.result.current.requestCode(address));
  await waitFor(() => expect(view.result.current.status).toBe("awaitingCode"));

  return view;
}

describe("requestCode", () => {
  test("asks for a code for the trimmed address", async () => {
    const view = await atCodeStep("  someone@example.com  ");

    expect(sendCode).toHaveBeenCalledWith("someone@example.com");
    expect(view.result.current.email).toBe("someone@example.com");
  });

  test("holds the resend shut so a stuck person cannot mail-bomb an inbox", async () => {
    const view = await atCodeStep();

    expect(view.result.current.resendIn).toBe(RESEND_COOLDOWN_SECONDS);
  });

  test("returns to the address form when the request itself fails", async () => {
    sendCode.mockRejectedValue(new Error("Could not reach the sign-in service. Try again."));

    const view = renderHook(() => useEmailCodeSignIn());
    await act(async () => view.result.current.requestCode("someone@example.com"));

    await waitFor(() => expect(view.result.current.status).toBe("idle"));
    expect(view.result.current.error).toBe("Could not reach the sign-in service. Try again.");
  });
});

describe("submitCode", () => {
  test("checks the code against the address the code was sent to", async () => {
    const view = await atCodeStep();

    await act(async () => view.result.current.submitCode(" 123456 "));

    expect(verifyCode).toHaveBeenCalledWith("someone@example.com", "123456");
  });

  test("reports being signed in once the code is accepted", async () => {
    const view = await atCodeStep();

    await act(async () => view.result.current.submitCode("123456"));

    await waitFor(() => expect(view.result.current.status).toBe("signedIn"));
  });

  test("stays on the code step when the code is refused", async () => {
    verifyCode.mockRejectedValue(
      new Error("That code is wrong or has expired. Ask for a new one."),
    );

    const view = await atCodeStep();
    await act(async () => view.result.current.submitCode("000000"));

    // Dropping back to the address form here would make the person request a second code to fix a
    // typo in the first.
    await waitFor(() => expect(view.result.current.error).toBeTruthy());
    expect(view.result.current.status).toBe("awaitingCode");
    expect(view.result.current.email).toBe("someone@example.com");
  });
});

describe("reset", () => {
  test("empties the form so a different address can be used", async () => {
    const view = await atCodeStep();

    act(() => view.result.current.reset());

    expect(view.result.current.status).toBe("idle");
    expect(view.result.current.email).toBe("");
    expect(view.result.current.error).toBeNull();
    expect(view.result.current.resendIn).toBe(0);
  });

  test("ignores a reply from the request it walked away from", async () => {
    let land = () => {};
    sendCode.mockReturnValue(new Promise<void>((resolve) => (land = resolve)));

    const view = renderHook(() => useEmailCodeSignIn());
    await act(async () => view.result.current.requestCode("someone@example.com"));
    act(() => view.result.current.reset());

    await act(async () => {
      land();
    });

    expect(view.result.current.status).toBe("idle");
  });
});
