/**
 * The sign-in page.
 */
import { ArrowLeft, MailCheck, TriangleAlert } from "lucide-react";
import { useEffect, useId, useState, type FormEvent } from "react";

import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { cn } from "@/lib/utils";

import { useSession } from "./authClient";
import { APP_URL } from "./routes";
import { useMagicLinkSignIn } from "./useMagicLinkSignIn";

/** Who to ask for an account. Placeholder until the real inbox is decided. */
const ADMIN_CONTACT = "admin@example.com";

export function LoginPage() {
  const signIn = useMagicLinkSignIn();
  const { data: session, isPending } = useSession();

  // The far end of the emailed link: Neon verifies it, sets the session and sends the browser to
  // /chat/. Someone who instead comes back to this page with a live session should not be asked to
  // sign in again, so they get the same push forward.
  useEffect(() => {
    if (session) window.location.replace(APP_URL);
  }, [session]);

  // A signed-in visitor is one navigation away from leaving. Drawing the form first would show
  // them a sign-in page they do not need, so hold the blank ground until the redirect lands.
  if (isPending || session) return <main className="h-dvh" aria-busy />;

  return (
    <main className="flex min-h-dvh items-center justify-center p-6">
      <div className="flex w-full max-w-xs flex-col gap-10">
        <Wordmark />
        {signIn.status === "sent" ? (
          <LinkSentPanel signIn={signIn} />
        ) : (
          <EmailForm signIn={signIn} />
        )}
      </div>
    </main>
  );
}

type SignIn = ReturnType<typeof useMagicLinkSignIn>;

function EmailForm({ signIn }: { signIn: SignIn }) {
  const [email, setEmail] = useState("");
  const id = useId();
  const sending = signIn.status === "sending";

  function onSubmit(event: FormEvent) {
    event.preventDefault();
    if (!sending) signIn.requestLink(email);
  }

  return (
    <>
      <div className="flex flex-col gap-2">
        <h1 className="text-3xl leading-tight font-semibold tracking-tight text-balance">
          Sign in to SpiritAI
        </h1>
        <p className="text-muted-foreground text-sm">
          Enter your email and we’ll send you a sign-in link.
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
            aria-invalid={signIn.status === "error" || undefined}
            aria-describedby={signIn.error ? `${id}-error` : undefined}
            disabled={sending}
          />
        </div>

        {signIn.error ? (
          <p
            id={`${id}-error`}
            role="alert"
            className="text-destructive flex items-start gap-2 text-sm"
          >
            <TriangleAlert className="mt-0.5 size-4 shrink-0" />
            {signIn.error}
          </p>
        ) : null}

        <Button
          type="submit"
          className="w-full"
          disabled={sending || email.trim() === ""}
        >
          <Spinner show={sending} />
          {sending ? "Sending…" : "Send sign-in link"}
        </Button>
      </form>

      {/* Stands in for the sign-up link this page deliberately does not have. Someone who cannot
          get in needs a person, not a form. TODO: point this at the real address. */}
      <p className="text-muted-foreground text-sm">
        Access is granted by an administrator.{" "}
        <a
          href={`mailto:${ADMIN_CONTACT}`}
          className="text-foreground underline underline-offset-4"
        >
          Request access
        </a>
      </p>
    </>
  );
}

function LinkSentPanel({ signIn }: { signIn: SignIn }) {
  return (
    <>
      <div className="flex flex-col gap-3">
        <MailCheck className="text-muted-foreground size-7" />
        <h1 className="text-3xl leading-tight font-semibold tracking-tight text-balance">
          Check your email
        </h1>
        {/* Says "if", not "we did". An address that is not configured gets this same screen and no
            email, and the wording must not give that away. */}
        <p className="text-muted-foreground text-sm">
          If <span className="text-foreground font-medium">{signIn.email}</span>{" "}
          can sign in, a link is on its way. It expires in 15 minutes.
        </p>
      </div>

      <div className="grid gap-3">
        <Button
          variant="outline"
          className="w-full"
          disabled={signIn.resendIn > 0}
          onClick={() => signIn.requestLink(signIn.email)}
        >
          {signIn.resendIn > 0
            ? `Resend in ${signIn.resendIn}s`
            : "Resend the link"}
        </Button>

        <Button
          variant="ghost"
          className="text-muted-foreground w-full"
          onClick={signIn.reset}
        >
          <ArrowLeft />
          Use a different email
        </Button>
      </div>
    </>
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
