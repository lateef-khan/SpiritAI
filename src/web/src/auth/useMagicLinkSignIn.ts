import { useCallback, useEffect, useRef, useState } from "react";

import { sendMagicLink } from "./magicLink";

/**
 * Where the form is.
 *
 * `sent` is a claim about the request, not about the address — see magicLink.ts. There is no
 * `signed in` state here: the link in the email carries the session, and it lands on a different
 * page load.
 */
export type SignInStatus = "idle" | "sending" | "sent" | "error";

/** Seconds the resend button stays disabled, so a stuck user cannot mail-bomb an inbox. */
export const RESEND_COOLDOWN_SECONDS = 30;

export type MagicLinkSignIn = {
  status: SignInStatus;
  /** The address the last request was made for. Kept so the sent panel can name it. */
  email: string;
  /** Set only when `status` is `error`. */
  error: string | null;
  /** Seconds left before resending is allowed. Zero when it is allowed. */
  resendIn: number;
  requestLink: (email: string) => void;
  /** Back to an empty form — the "use a different address" path. */
  reset: () => void;
};

export function useMagicLinkSignIn(): MagicLinkSignIn {
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

  const requestLink = useCallback((next: string) => {
    const id = ++requestId.current;
    const trimmed = next.trim();

    setEmail(trimmed);
    setError(null);
    setStatus("sending");

    sendMagicLink(trimmed).then(
      () => {
        if (requestId.current !== id) return;
        setStatus("sent");
        setResendIn(RESEND_COOLDOWN_SECONDS);
      },
      (cause: unknown) => {
        if (requestId.current !== id) return;
        setStatus("error");
        setError(cause instanceof Error ? cause.message : "Something went wrong. Try again.");
      },
    );
  }, []);

  const reset = useCallback(() => {
    requestId.current++;
    setStatus("idle");
    setEmail("");
    setError(null);
    setResendIn(0);
  }, []);

  return { status, email, error, resendIn, requestLink, reset };
}
