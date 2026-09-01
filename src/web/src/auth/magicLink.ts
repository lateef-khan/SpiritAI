/**
 * The one call that leaves the browser during sign-in.
 *
 * Neon mails the link and, once it is clicked, verifies it and redirects to `callbackURL` with the
 * session already set. So there is nothing to await here beyond "the request was accepted".
 *
 * The subtlety is which failures the caller is allowed to see.
 *
 * Only pre-configured addresses can sign in, which means Neon rejects the rest — and if the form
 * repeated that rejection, it would become a way to ask "does this person have an account here?".
 * So a rejection of the *address* is swallowed and reported as success. Only a failure of the
 * *request* — offline, rate limited, Neon down — comes back as an error, because that is the only
 * kind the person at the keyboard can do anything about.
 */
import { authClient } from "./authClient";
import { appUrlAbsolute } from "./routes";

/** Statuses that mean "this address may not sign in". Indistinguishable from success, on purpose. */
const ADDRESS_REJECTED = new Set([400, 401, 403, 404, 422]);

export class MagicLinkError extends Error {}

export async function sendMagicLink(email: string): Promise<void> {
  const { error } = await authClient.signIn.magicLink({
    email,
    callbackURL: appUrlAbsolute(),
  });

  if (!error) return;

  const status = typeof error.status === "number" ? error.status : 0;

  if (ADDRESS_REJECTED.has(status)) return;

  if (status === 429) {
    throw new MagicLinkError("Too many attempts. Wait a minute and try again.");
  }

  throw new MagicLinkError(error.message || "Could not reach the sign-in service. Try again.");
}
