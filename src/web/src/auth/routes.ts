/**
 * The two pages, as the browser addresses them.
 *
 * This app is not a single-page router: index.html and login.html are separate documents, built
 * separately by vite.config.ts. So moving between them is a navigation, not a state change, and
 * these are the only two places that navigation can land.
 */
export const LOGIN_URL = "/chat/login.html";
export const APP_URL = "/chat/";

/** Where the emailed link drops the user once Neon has verified it. Absolute, as Better Auth
 *  hands this straight to the auth service for a redirect. */
export function appUrlAbsolute(): string {
  return new URL(APP_URL, window.location.origin).toString();
}
