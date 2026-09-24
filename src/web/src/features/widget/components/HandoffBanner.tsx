import { HeadsetIcon, HourglassIcon, PhoneIcon } from "lucide-react";
import { useState, type FormEvent } from "react";

import { TypingDots } from "@/components/assistant-ui/elements/typing-indicator";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { HostRefusedError } from "@/lib/apiClient";
import type { HandoffState } from "../api/widgetApi";

/**
 * The strip above the chat while a person is asked for, or has the chat.
 */

/** Where the chat stands, and how to leave a phone number. */
export type HandoffBannerProps = {
  state: HandoffState;
  typing?: boolean;
  onLeavePhone: (phone: string) => Promise<void>;
};

/** The one line that says where the chat stands. */
function Line({ state, typing }: { state: HandoffState; typing: boolean }) {
  if (state.status === "human") {
    const name = state.assigneeName ?? "A member of staff";
    return (
      <p className="flex items-center gap-2">
        <HeadsetIcon className="size-4 shrink-0" aria-hidden />
        <span>
          <span className="font-medium">{name}</span>{" "}
          {typing ? (
            <>
              is typing <TypingDots />
            </>
          ) : (
            "is with you."
          )}
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

/** What to show when the number did not go through: the host's own words for a refused number. */
function failureOf(error: unknown): string {
  return error instanceof HostRefusedError && error.status === 400 && error.title
    ? error.title
    : "That did not go through. Try again.";
}

/** The phone box, shown while a person is waited for and no number has been left. */
function PhoneBox({ onLeavePhone }: { onLeavePhone: (phone: string) => Promise<void> }) {
  const [phone, setPhone] = useState("");
  const [busy, setBusy] = useState(false);
  const [failure, setFailure] = useState<string | null>(null);

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    if (busy || phone.trim().length === 0) return;

    setBusy(true);
    setFailure(null);
    try {
      await onLeavePhone(phone.trim());
    } catch (error) {
      setFailure(failureOf(error));
    } finally {
      setBusy(false);
    }
  };

  return (
    <form onSubmit={submit} className="flex flex-col gap-2">
      <p className="text-muted-foreground">Leave your phone number, and a team member will reach out.</p>
      <div className="flex gap-2">
        <Input
          type="tel"
          autoComplete="tel"
          required
          value={phone}
          onChange={(event) => setPhone(event.target.value)}
          placeholder="(555) 010-2233"
          aria-label="Your phone number"
          aria-invalid={failure !== null || undefined}
          className="h-8"
        />
        <Button type="submit" size="sm" disabled={busy || phone.trim().length === 0}>
          Send
        </Button>
      </div>
      {failure && <p className="text-destructive">{failure}</p>}
    </form>
  );
}

export function HandoffBanner({ state, typing = false, onLeavePhone }: HandoffBannerProps) {
  if (state.status !== "waiting" && state.status !== "human") return null;

  // Asked whenever a person was requested and no number is on file, online or not: staff call
  // back, so a visitor with no number on file never hears back.
  const asksForPhone = state.status === "waiting";

  return (
    <div
      role="status"
      className="bg-muted text-foreground border-border/60 flex flex-col gap-2 border-b ps-4 pe-10 py-3 text-sm"
    >
      <Line state={state} typing={typing} />
      {state.status === "waiting" && <Presence online={state.staffOnline} />}
      {asksForPhone &&
        (state.phone ? (
          <p className="text-muted-foreground flex items-center gap-2">
            <PhoneIcon className="size-4 shrink-0" aria-hidden />
            <span>
              We will call you at <span className="font-medium">{state.phone}</span>.
              {state.code !== null && (
                <>
                  {" "}
                  Your code is <span className="font-medium">{state.code}</span>.
                </>
              )}
            </span>
          </p>
        ) : (
          <PhoneBox onLeavePhone={onLeavePhone} />
        ))}
    </div>
  );
}
