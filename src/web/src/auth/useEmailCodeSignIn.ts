import { useCallback, useEffect, useRef, useState } from "react";

import { sendCode, verifyCode } from "./emailCode";

/**
 * Where the form is.
 *
 * `awaitingCode` is a claim about the request, not about the address — see emailCode.ts. It is also
 * where a refused code lands, because a person who mistyped one digit should be able to fix that
 * digit rather than ask for a whole new code.
 *
 * `signedIn` exists because nothing navigates on its own here: the session arrives in the same page
 * load that asked for it, so the page has to be told to leave.
 */
export type SignInStatus = "idle" | "sending" | "awaitingCode" | "verifying" | "signedIn";

/** Seconds the resend button stays disabled, so a stuck user cannot mail-bomb an inbox. */
export const RESEND_COOLDOWN_SECONDS = 30;

export type EmailCodeSignIn = {
  status: SignInStatus;
  /** The address the code was asked for. Kept so the code step can name it. */
  email: string;
  /** What went wrong, at whichever step it went wrong. Cleared by the next attempt. */
  error: string | null;
  /** Seconds left before another code may be asked for. Zero when it is allowed. */
  resendIn: number;
  requestCode: (email: string) => void;
  submitCode: (code: string) => void;
  /** Back to an empty form — the "use a different address" path. */
  reset: () => void;
};

export function useEmailCodeSignIn(): EmailCodeSignIn {
  const [status, setStatus] = useState<SignInStatus>("idle");
  const [email, setEmail] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [resendIn, setResendIn] = useState(0);

  // Guards a late reply from a request the user has already walked away from, which would
  // otherwise drag the form back to a state they left.
  const requestId = useRef(0);

  useEffect(() => {
    if (resendIn <= 0) return;

    const timer = setTimeout(() => setResendIn((s) => s - 1), 1000);
    return () => clearTimeout(timer);
  }, [resendIn]);

  /** Runs one step, and hands the form back to `fallback` if it fails. */
  const attempt = useCallback(
    (
      working: SignInStatus,
      done: SignInStatus,
      fallback: SignInStatus,
      step: () => Promise<void>,
    ) => {
      const id = ++requestId.current;

      setError(null);
      setStatus(working);

      step().then(
        () => {
          if (requestId.current !== id) return;
          setStatus(done);
        },
        (cause: unknown) => {
          if (requestId.current !== id) return;
          setStatus(fallback);
          setError(cause instanceof Error ? cause.message : "Something went wrong. Try again.");
        },
      );
    },
    [],
  );

  const requestCode = useCallback(
    (next: string) => {
      const trimmed = next.trim();
      setEmail(trimmed);

      attempt("sending", "awaitingCode", "idle", async () => {
        await sendCode(trimmed);
        setResendIn(RESEND_COOLDOWN_SECONDS);
      });
    },
    [attempt],
  );

  const submitCode = useCallback(
    (code: string) => {
      attempt("verifying", "signedIn", "awaitingCode", () => verifyCode(email, code.trim()));
    },
    [attempt, email],
  );

  const reset = useCallback(() => {
    requestId.current++;
    setStatus("idle");
    setEmail("");
    setError(null);
    setResendIn(0);
  }, []);

  return { status, email, error, resendIn, requestCode, submitCode, reset };
}
