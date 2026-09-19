import { InboxIcon } from "lucide-react";

import { Badge } from "@/components/ui/badge";
import { SidebarMenuButton, SidebarMenuItem } from "@/components/ui/sidebar";
import { Tooltip, TooltipContent, TooltipTrigger } from "@/components/ui/tooltip";

import { useHandoffPushes } from "../hooks/useHandoffPushes";
import { useInboxCounts } from "../hooks/useInboxCounts";

/**
 * The sidebar's Inbox entry, with two numbers beside it: the viewer's chats whose visitor is
 * waiting on them, and chats nobody has taken yet.
 *
 * This entry is up on every screen, so it is also where the app hears the handoff pushes that
 * keep the cache — these counts, and the inbox's rows whenever it is open — current. A zero is
 * not drawn; an empty inbox reads better as no badge than as two grey zeros.
 */
export function InboxMenuItem({
  meKey,
  active,
  onOpen,
}: {
  meKey: string;
  active: boolean;
  onOpen: () => void;
}) {
  useHandoffPushes(meKey);
  const counts = useInboxCounts("open");
  return (
    <SidebarMenuItem>
      <SidebarMenuButton isActive={active} onClick={onOpen}>
        <InboxIcon />
        <span className="flex-1">Inbox</span>
        {counts.awaitingReply > 0 || counts.unassigned > 0 ? (
          <span className="flex shrink-0 items-center gap-1">
            {counts.awaitingReply > 0 ? (
              <CountBadge
                count={counts.awaitingReply}
                variant="secondary"
                tip="Your chats waiting for your reply"
              />
            ) : null}
            {counts.unassigned > 0 ? (
              <CountBadge count={counts.unassigned} variant="default" tip="Waiting for someone" />
            ) : null}
          </span>
        ) : null}
      </SidebarMenuButton>
    </SidebarMenuItem>
  );
}

/**
 * One number with its meaning on hover.
 *
 * The trigger is the badge itself, a `span`, because this sits inside the menu button and the
 * `button` Radix would otherwise render is not valid inside another button.
 */
function CountBadge({
  count,
  variant,
  tip,
}: {
  count: number;
  variant: "secondary" | "default";
  tip: string;
}) {
  return (
    <Tooltip>
      <TooltipTrigger asChild>
        <Badge variant={variant} className="min-w-5 px-1.5 tabular-nums" aria-label={tip}>
          {count}
        </Badge>
      </TooltipTrigger>
      <TooltipContent side="right">{tip}</TooltipContent>
    </Tooltip>
  );
}
