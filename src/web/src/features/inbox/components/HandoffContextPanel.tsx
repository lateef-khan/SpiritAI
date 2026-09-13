import type { ExportedMessageRepository } from "@assistant-ui/react";
import { useMemo, useState } from "react";

import { Avatar, AvatarFallback } from "@/components/ui/avatar";
import { Button } from "@/components/ui/button";
import {
  Collapsible,
  CollapsibleContent,
  CollapsibleTrigger,
} from "@/components/ui/collapsible";
import { flatten } from "@/features/threads/AgentCoreRuntime";
import { UnitPanel } from "@/features/unit/UnitPanel";
import type { Said } from "@/hooks/useIdentifiers";
import { cn } from "@/lib/utils";

import type { Handoff } from "../api/handoffsApi";
import { clockTime } from "../format";

/**
 * The context rail beside one handoff's transcript: who the visitor is, which machine the
 * conversation names, and the handoff's own facts folded away.
 *
 * The visitor card renders only when the handoff carries an email; the unit section is the
 * shared `UnitPanel`, fed the transcript's turns the same way the chat's own panel is fed the
 * live thread. The inbox has no agent turn behind it, so asking is permanently off and the
 * unit rows stay inert.
 */
export function HandoffContextPanel({
  handoff,
  history,
  className,
}: {
  handoff: Handoff;
  history: ExportedMessageRepository | null;
  className?: string;
}) {
  const said = useMemo<Said[]>(
    () =>
      history?.messages.map((item) => ({
        role: item.message.role,
        text: flatten(item.message).content,
      })) ?? [],
    [history],
  );

  return (
    <section
      aria-label="Context"
      data-testid="handoff-context"
      className={cn("flex h-full min-h-0 w-full flex-col bg-card", className)}
    >
      <VisitorCard handoff={handoff} />
      <div className="min-h-0 flex-1">
        <UnitPanel said={said} onAsk={noop} isRunning className="border-l-0" />
      </div>
      <HandoffFold handoff={handoff} />
    </section>
  );
}

/**
 * Nowhere to send a question. This never fires: every unit row is inert while `isRunning`
 * holds, and here it always holds.
 */
function noop(): void {}

function VisitorCard({ handoff }: { handoff: Handoff }) {
  if (!handoff.email) return null;

  return (
    <div className="flex shrink-0 items-center gap-2.5 border-b px-3.5 py-3">
      <Avatar size="sm">
        <AvatarFallback>{handoff.email.slice(0, 1).toUpperCase()}</AvatarFallback>
      </Avatar>
      <p className="min-w-0 flex-1 truncate text-sm font-medium">{handoff.email}</p>
      <CopyButton text={handoff.email} label={`Copy ${handoff.email}`} />
    </div>
  );
}

function CopyButton({ text, label }: { text: string; label: string }) {
  const [copied, setCopied] = useState(false);

  return (
    <Button
      type="button"
      variant="ghost"
      size="sm"
      aria-label={label}
      onClick={() => {
        const clipboard = navigator.clipboard;
        if (!clipboard) return;

        void clipboard.writeText(text).then(() => {
          setCopied(true);
          window.setTimeout(() => setCopied(false), 1500);
        });
      }}
    >
      {copied ? "Copied" : "Copy"}
    </Button>
  );
}

function HandoffFold({ handoff }: { handoff: Handoff }) {
  return (
    <Collapsible className="shrink-0 border-t px-3.5 py-2.5">
      <CollapsibleTrigger className="w-full truncate text-left text-xs font-medium text-muted-foreground">
        {foldTitle(handoff)}
      </CollapsibleTrigger>
      <CollapsibleContent>
        <dl className="grid grid-cols-[4.5rem_1fr] gap-x-3 gap-y-2 pt-2 text-xs">
          <dt className="text-muted-foreground">Asked by</dt>
          <dd>{handoff.askedBy === "bot" ? "Spirit" : "Visitor"}</dd>
          {handoff.reason ? (
            <>
              <dt className="text-muted-foreground">Reason</dt>
              <dd>{handoff.reason}</dd>
            </>
          ) : null}
          <dt className="text-muted-foreground">Asked</dt>
          <dd className="tabular-nums">{clockTime(handoff.askedAt)}</dd>
          <dt className="text-muted-foreground">Chat id</dt>
          <dd className="truncate font-mono tabular-nums">{handoff.callId}</dd>
        </dl>
      </CollapsibleContent>
    </Collapsible>
  );
}

function foldTitle(handoff: Handoff): string {
  if (handoff.status === "waiting") {
    return handoff.position != null
      ? `Handoff · Waiting · #${handoff.position} in line`
      : "Handoff · Waiting";
  }

  if (handoff.status === "human") {
    return `Handoff · ${handoff.assignee?.name ?? "Someone"}`;
  }

  return "Handoff · Back with Spirit";
}
