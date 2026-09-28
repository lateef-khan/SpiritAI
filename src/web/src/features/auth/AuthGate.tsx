/**
 * The lock on the front door.
 *
 * Wrap the app in this and an unauthenticated visitor is sent to the login page instead of
 * rendering it. `useSession` starts as pending on every load — the session lives in a cookie the
 * client has to check with Neon — so there are three outcomes, not two, and the pending one must
 * not flash the login page at somebody who is in fact signed in.
 */
import type { ReactNode } from "react";
import { useEffect } from "react";

import { useSession } from "./authClient";
import { loginUrlWithReturnTo } from "./routes";

export function AuthGate({ children }: { children: ReactNode }) {
  const { data, isPending } = useSession();
  const signedOut = !isPending && !data;

  useEffect(() => {
    // `replace`, so the back button does not bounce between the app and the login page. The page
    // they were on comes back as `returnTo`, so signing in returns them to it instead of the app's
    // front door.
    if (signedOut) {
      const { pathname, search, hash } = window.location;
      window.location.replace(loginUrlWithReturnTo(pathname + search + hash));
    }
  }, [signedOut]);

  // Pending and signed-out render the same blank ground: one is about to become the app, the other
  // is about to become a different document, and neither wants a half-drawn UI in the meantime.
  if (isPending || signedOut) return <AuthSplash />;

  return <>{children}</>;
}

function AuthSplash() {
  return (
    <div className="bg-background flex h-dvh w-full items-center justify-center" aria-busy>
      <span className="sr-only">Checking your session…</span>
    </div>
  );
}
