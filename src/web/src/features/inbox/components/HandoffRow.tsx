import { BotIcon, UserIcon } from "lucide-react";

import { Avatar, AvatarFallback } from "@/components/ui/avatar";
import { Button } from "@/components/ui/button";
import { cn } from "@/lib/utils";

import type { Handoff } from "../api/handoffsApi";
import { clockTime, handoffTitle, minutesBetween } from "../format";

/**
 * One conversation, as it reads in the list.
 *
 * The screen owns which handoff is selected; this row only ever reports a click and receives
 * back whether it is the current one.
 */
export function HandoffRow({
  handoff,
  selected,
  onSelect,
}: {
  handoff: Handoff;
  selected: boolean;
  onSelect: () => void;
}) {

  const title = handoffTitle(handoff);

  return (
    <Button
      type="button"
      variant="ghost"
      aria-current={selected ? "true" : undefined}
      onClick={onSelect}
      className={cn(
        "h-auto w-full flex-col items-stretch gap-0 rounded-none border-b border-l-2 border-l-transparent px-3.5 py-3 text-left whitespace-normal",
        "hover:bg-accent aria-[current=true]:border-l-primary aria-[current=true]:bg-accent",
      )}
    >
      <OverLine handoff={handoff} />

      <div className="flex gap-2.5">
        <RowAvatar email={handoff.email} />

        <div className="min-w-0 flex-1 space-y-0.5">
          <span className="block truncate text-sm font-semibold">{title}</span>

          <div className="flex items-baseline justify-between gap-2">
            <span className="min-w-0 flex-1 truncate text-xs text-muted-foreground">
              {handoff.firstLine}
            </span>
            <span className="shrink-0 text-[12.5px] tabular-nums text-muted-foreground">
              {clockTime(handoff.askedAt)}
            </span>
          </div>

          {handoff.reason ? (
            <span className="mt-1.5 block rounded-md bg-aui-warning/10 px-2 py-1 text-[12.5px]">
              {handoff.reason}
            </span>
          ) : null}
        </div>
      </div>
    </Button>
  );
}

/** Who asked, on the left; who is waiting or who has it, on the right. */
function OverLine({ handoff }: { handoff: Handoff }) {
  const AskedIcon = handoff.askedBy === "bot" ? BotIcon : UserIcon;
  const askedLabel = handoff.askedBy === "bot" ? "Spirit asked" : "Visitor asked";

  return (
    <div className="mb-1 flex items-center justify-between gap-2 text-[12.5px] text-muted-foreground">
      <span className="inline-flex items-center gap-1">
        <AskedIcon className="size-3.5" />
        {askedLabel}
      </span>

      {handoff.status === "waiting" ? (
        <span className="tabular-nums">{minutesBetween(handoff.askedAt, new Date())} min</span>
      ) : (
        <span className="inline-flex items-center gap-1">
          <UserIcon className="size-3.5" />
          {handoff.assignee?.name}
        </span>
      )}
    </div>
  );
}

function RowAvatar({ email }: { email: string | null }) {
  return (
    <Avatar size="lg" className="shrink-0">
      <AvatarFallback className="text-sm font-semibold">
        {email ? email.charAt(0).toUpperCase() : <UserIcon className="size-5" />}
      </AvatarFallback>
    </Avatar>
  );
}

