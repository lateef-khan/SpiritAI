/**
 * The Neon Auth client, made once for the whole tab.
 *
 * Neon Auth is Managed Better Auth: `createAuthClient` hands back a Better Auth client, and the
 * React adapter is what puts `useSession` on it. The URL is the project's Auth Base URL, from the
 * Neon Console under Auth → Configuration.
 *
 * Nothing secret lives here. The Auth Base URL is public by design — it is the address the browser
 * posts to — which is why it may sit in a `VITE_` variable and be baked into the bundle.
 */
import { createAuthClient } from "@neondatabase/auth";
import { BetterAuthReactAdapter } from "@neondatabase/auth/react/adapters";

const url = import.meta.env.VITE_NEON_AUTH_URL;

if (!url) {
  // Thrown at import, not swallowed into a broken sign-in button. A missing URL is a setup mistake
  // on a developer's machine, and it should read like one. See .env.example.
  throw new Error(
    "VITE_NEON_AUTH_URL is not set. Copy src/web/.env.example to src/web/.env and fill it in " +
      "with the Auth Base URL from the Neon Console (Auth → Configuration).",
  );
}

export const authClient = createAuthClient(url, {
  adapter: BetterAuthReactAdapter(),
});

export const { useSession } = authClient;
