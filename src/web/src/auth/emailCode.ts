/**
 * The two calls that leave the browser during sign-in.
 *
 * Neon mails a short code and the person types it back. A code, rather than a link, because a link
 * is a URL and a mail scanner opens URLs: Microsoft Defender's Safe Links fetches every link it
 * delivers, which spends the one-time token before the person ever clicks it. They then land on the
 * app with no session and are sent straight back to this form, forever. A code cannot be spent by
 * anything that only reads the mail.
 *
 * The subtlety is which failures the caller is allowed to see, and it differs between the two
 * calls.
 *
 * Only pre-configured addresses can sign in, so a rejection of the *address* is swallowed by
 * {@link sendCode} and reported as success — otherwise the form becomes a way to ask "does this
 * person have an account here?". By {@link verifyCode} the address is no longer a secret from the
 * person typing, and silence there would only leave them retyping a code that can never work, so a
 * refused code is named.
 */
import { authClient } from "./authClient";

/**
 * Statuses that mean "refused", as opposed to "the request never arrived". Which refusal it is
 * depends on the call: an address that may not sign in, or a code that does not match.
 */
const REFUSED = new Set([400, 401, 403, 404, 422]);

export class EmailCodeError extends Error {}

/** Asks Neon to mail a sign-in code. Resolves whether or not the address is allowed one. */
export async function sendCode(email: string): Promise<void> {
  const status = await refusalStatus(() =>
    authClient.emailOtp.sendVerificationOtp({ email, type: "sign-in" }),
  );

  if (status === null || REFUSED.has(status)) return;

  throw new EmailCodeError(requestFailure(status));
}

/** Trades a typed code for a session. */
export async function verifyCode(email: string, code: string): Promise<void> {
  const status = await refusalStatus(() => authClient.signIn.emailOtp({ email, otp: code }));

  if (status === null) return;

  if (REFUSED.has(status)) {
    throw new EmailCodeError("That code is wrong or has expired. Ask for a new one.");
  }

  throw new EmailCodeError(requestFailure(status));
}

/**
 * The HTTP status behind a refusal, or `null` when the call succeeded.
 *
 * The client reports a refusal two ways — a rejected promise carrying an `AuthApiError`, or a
 * resolved `{ error }` — so both are read here rather than at each call site. An unrecognised
 * shape becomes 0, which no branch treats as a refusal, so it surfaces as a request failure.
 */
async function refusalStatus(call: () => Promise<unknown>): Promise<number | null> {
  try {
    const { error } = (await call()) as { error?: { status?: unknown } | null };

    return error ? statusOf(error) : null;
  } catch (cause) {
    return statusOf(cause);
  }
}

function statusOf(source: unknown): number {
  const status = (source as { status?: unknown } | null)?.status;

  return typeof status === "number" ? status : 0;
}

/**
 * What to say when the request itself failed. Never the client's own message: it is the bare
 * "HTTP 400 Bad Request", which tells the person nothing they can act on.
 */
function requestFailure(status: number): string {
  return status === 429
    ? "Too many attempts. Wait a minute and try again."
    : "Could not reach the sign-in service. Try again.";
}
