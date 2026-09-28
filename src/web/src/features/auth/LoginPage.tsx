/**
 * The sign-in page.
 */
import { ArrowLeft, MailCheck, TriangleAlert } from "lucide-react";
import { useEffect, useId, useState, type FormEvent } from "react";

import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { cn } from "@/lib/utils";

import { isHubAppId, type HubAppId } from "../hub/hubMessages";
import { useSession } from "./authClient";
import { safeReturnTo } from "./returnTo";
import { APP_URL, loginUrlWithReturnTo } from "./routes";
import { useEmailCodeSignIn } from "./useEmailCodeSignIn";

/** The app named in `?app=`, or `null` when it names none of the Hub's apps. */
function hubAppFromQuery(): HubAppId | null {
  const app = new URLSearchParams(window.location.search).get("app");
  return isHubAppId(app) ? app : null;
}

export function LoginPage() {
  const signIn = useEmailCodeSignIn();
  const { data: session, isPending } = useSession();

  // Chatwoot and Twenty send their own sign-in redirect here, framed inside the Hub, so this page
  // can hand off without ever drawing the Neon form where a stranger's iframe could read it. This
  // has to run before any of the ordinary page's form-or-redirect logic below, and stays out of it
  // even when the frame names no app the Hub knows.
  const framed = window.parent !== window;
  const app = framed ? hubAppFromQuery() : null;

  useEffect(() => {
    if (!framed) return;

    if (!app) {
      window.top!.location.assign(loginUrlWithReturnTo("/"));
      return;
    }
    if (isPending) return;

    if (session) {
      window.parent.postMessage({ type: "hub:needs-sign-in", app }, window.location.origin);
    } else {
      window.top!.location.assign(loginUrlWithReturnTo("/#" + app));
    }
  }, [framed, app, isPending, session]);

  // Two ways to arrive at the app, and neither happens on its own. A code accepted on this page
  // hands back a session in the same page load, and someone who reloads with a live session should
  // not be asked to sign in again; both are one navigation away from leaving.
  const leaving = !framed && (signIn.status === "signedIn" || Boolean(session));

  useEffect(() => {
    if (!leaving) return;

    const returnTo = new URLSearchParams(window.location.search).get("returnTo");
    if (returnTo !== null) {
      window.location.replace(safeReturnTo(returnTo, window.location.origin));
      return;
    }

    // No returnTo, but the query still names an app when Chatwoot or Twenty sent someone here
    // directly (not framed) with a live session already — send them straight to that tile rather
    // than dropping them on the chat app they may not have been looking for.
    const app = hubAppFromQuery();
    window.location.replace(app ? `/#${app}` : APP_URL);
  }, [leaving]);

  // Never draw the sign-in form inside a frame, whether or not the frame named an app.
  if (framed) return <main className="h-dvh" aria-busy />;

  // Drawing the form first would show them a sign-in page they do not need, so hold the blank
  // ground until the redirect lands.
  if (isPending || leaving) return <main className="h-dvh" aria-busy />;

  return (
    <main className="flex min-h-dvh items-center justify-center p-6">
      <div className="flex w-full max-w-xs flex-col gap-10">
        <Wordmark />
        {signIn.status === "idle" || signIn.status === "sending" ? (
          <EmailForm signIn={signIn} />
        ) : (
          <CodeForm signIn={signIn} />
        )}
      </div>
    </main>
  );
}

type SignIn = ReturnType<typeof useEmailCodeSignIn>;

function EmailForm({ signIn }: { signIn: SignIn }) {
  const [email, setEmail] = useState("");
  const id = useId();
  const sending = signIn.status === "sending";

  function onSubmit(event: FormEvent) {
    event.preventDefault();
    if (!sending) signIn.requestCode(email);
  }

  return (
    <>
      <div className="flex flex-col gap-2">
        <h1 className="text-3xl leading-tight font-semibold tracking-tight text-balance">
          Sign in to SpiritAI
        </h1>
        <p className="text-muted-foreground text-sm">
          Enter your email and we’ll send you a sign-in code.
        </p>
      </div>

      <form className="grid gap-5" onSubmit={onSubmit}>
        <div className="grid gap-2">
          <Label htmlFor={id}>Email</Label>
          <Input
            id={id}
            type="email"
            name="email"
            placeholder="you@example.com"
            autoComplete="email"
            // The only field on the page, and the page exists to be typed into.
            autoFocus
            required
            value={email}
            onChange={(event) => setEmail(event.target.value)}
            aria-invalid={Boolean(signIn.error) || undefined}
            aria-describedby={signIn.error ? `${id}-error` : undefined}
            disabled={sending}
          />
        </div>

        <Problem id={`${id}-error`} error={signIn.error} />

        <Button type="submit" className="w-full" disabled={sending || email.trim() === ""}>
          <Spinner show={sending} />
          {sending ? "Sending…" : "Send sign-in code"}
        </Button>
      </form>
    </>
  );
}

function CodeForm({ signIn }: { signIn: SignIn }) {
  const [code, setCode] = useState("");
  const id = useId();
  const verifying = signIn.status === "verifying";

  function onSubmit(event: FormEvent) {
    event.preventDefault();
    if (!verifying) signIn.submitCode(code);
  }

  return (
    <>
      <div className="flex flex-col gap-3">
        <MailCheck className="text-muted-foreground size-7" />
        <h1 className="text-3xl leading-tight font-semibold tracking-tight text-balance">
          Check your email
        </h1>
        <p className="text-muted-foreground text-sm">
          If <span className="text-foreground font-medium">{signIn.email}</span> can sign in, a code
          is on its way.
        </p>
      </div>

      <form className="grid gap-5" onSubmit={onSubmit}>
        <div className="grid gap-2">
          <Label htmlFor={id}>Code</Label>
          <Input
            id={id}
            type="text"
            name="one-time-code"
            placeholder="123456"
            autoComplete="one-time-code"
            inputMode="numeric"
            autoFocus
            required
            value={code}
            onChange={(event) => setCode(event.target.value)}
            aria-invalid={Boolean(signIn.error) || undefined}
            aria-describedby={signIn.error ? `${id}-error` : undefined}
            disabled={verifying}
          />
        </div>

        <Problem id={`${id}-error`} error={signIn.error} />

        <Button type="submit" className="w-full" disabled={verifying || code.trim() === ""}>
          <Spinner show={verifying} />
          {verifying ? "Checking…" : "Sign in"}
        </Button>
      </form>

      <div className="grid gap-3">
        <Button
          variant="outline"
          className="w-full"
          disabled={signIn.resendIn > 0 || verifying}
          onClick={() => signIn.requestCode(signIn.email)}
        >
          {signIn.resendIn > 0 ? `Resend in ${signIn.resendIn}s` : "Send a new code"}
        </Button>

        <Button variant="ghost" className="text-muted-foreground w-full" onClick={signIn.reset}>
          <ArrowLeft />
          Use a different email
        </Button>
      </div>
    </>
  );
}

function Problem({ id, error }: { id: string; error: string | null }) {
  if (!error) return null;

  return (
    <p id={id} role="alert" className="text-destructive flex items-start gap-2 text-sm">
      <TriangleAlert className="mt-0.5 size-4 shrink-0" />
      {error}
    </p>
  );
}

function Wordmark() {
  return (
    <div className="flex items-center gap-2">
      <div className="bg-primary text-primary-foreground grid size-8 place-items-center rounded-lg text-sm font-semibold">
        S
      </div>
      <span className="text-lg font-semibold tracking-tight">SpiritAI</span>
    </div>
  );
}

/* Holds its slot whether or not it is spinning, so the button label does not shift sideways when
   the request starts. */
function Spinner({ show }: { show: boolean }) {
  return (
    <span
      aria-hidden
      className={cn(
        "border-primary-foreground/40 border-t-primary-foreground size-4 rounded-full border-2",
        show ? "animate-spin" : "invisible",
      )}
    />
  );
}
