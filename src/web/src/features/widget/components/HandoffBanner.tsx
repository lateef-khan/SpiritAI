import { HeadsetIcon, HourglassIcon, MailCheckIcon } from "lucide-react";
import { useState, type FormEvent } from "react";

import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import type { HandoffState } from "../api/widgetApi";

/**
 * The strip above the chat while a person is asked for, or has the chat.
 */

/** Where the chat stands, and how to leave an email. */
export type HandoffBannerProps = {
  state: HandoffState;
  typing?: boolean;
  onLeaveEmail: (email: string) => Promise<void>;
};

/** The one line that says where the chat stands. */
function Line({ state, typing }: { state: HandoffState; typing: boolean }) {
  if (state.status === "human") {
    const name = state.assigneeName ?? "A member of staff";
    return (
      <p className="flex items-center gap-2">
        <HeadsetIcon className="size-4 shrink-0" aria-hidden />
        <span>
          <span className="font-medium">{name}</span> {typing ? "is typing…" : "is with you."}
        </span>
      </p>
    );
  }

  return (
    <p className="flex items-center gap-2">
      <HourglassIcon className="size-4 shrink-0" aria-hidden />
      <span>Waiting for a person.</span>
    </p>
  );
}

/**
 * Whether anyone is behind the desk.
 */
function Presence({ online }: { online: boolean }) {
  return (
    <p className="text-muted-foreground flex items-center gap-2">
      <span
        aria-hidden
        className={`size-2 shrink-0 rounded-full ${online ? "bg-emerald-500" : "bg-muted-foreground/40"}`}
      />
      <span>{online ? "Someone is online." : "Nobody is online right now."}</span>
    </p>
  );
}

/** The email box, shown while a person is waited for and no email has been left. */
function EmailBox({ onLeaveEmail }: { onLeaveEmail: (email: string) => Promise<void> }) {
  const [email, setEmail] = useState("");
  const [busy, setBusy] = useState(false);
  const [failed, setFailed] = useState(false);

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    if (busy || email.trim().length === 0) return;

    setBusy(true);
    setFailed(false);
    try {
      await onLeaveEmail(email.trim());
    } catch {
      setFailed(true);
    } finally {
      setBusy(false);
    }
  };

  return (
    <form onSubmit={submit} className="flex flex-col gap-2">
      <p className="text-muted-foreground">
        Leave your email, and we will reply there if you step away.
      </p>
      <div className="flex gap-2">
        <Input
          type="email"
          required
          value={email}
          onChange={(event) => setEmail(event.target.value)}
          placeholder="you@example.com"
          aria-label="Your email"
          aria-invalid={failed || undefined}
          className="h-8"
        />
        <Button type="submit" size="sm" disabled={busy || email.trim().length === 0}>
          Send
        </Button>
      </div>
      {failed && <p className="text-destructive">That did not go through. Try again.</p>}
    </form>
  );
}

export function HandoffBanner({ state, typing = false, onLeaveEmail }: HandoffBannerProps) {
  if (state.status !== "waiting" && state.status !== "human") return null;

  // Asked whenever a person was requested and no address is on file, online or not: a visitor
  // who closes the tab before someone is free would otherwise never hear back.
  const asksForEmail = state.status === "waiting";

  return (
    <div
      role="status"
      className="bg-muted text-foreground border-border/60 flex flex-col gap-2 border-b ps-4 pe-10 py-3 text-sm"
    >
      <Line state={state} typing={typing} />
      {state.status === "waiting" && <Presence online={state.staffOnline} />}
      {asksForEmail &&
        (state.email ? (
          <p className="text-muted-foreground flex items-center gap-2">
            <MailCheckIcon className="size-4 shrink-0" aria-hidden />
            <span>
              We will email <span className="font-medium">{state.email}</span> when a person
              replies.
            </span>
          </p>
        ) : (
          <EmailBox onLeaveEmail={onLeaveEmail} />
        ))}
    </div>
  );
}
