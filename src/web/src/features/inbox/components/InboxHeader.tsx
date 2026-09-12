import { ArrowUpDownIcon, ChevronDownIcon, PanelLeftCloseIcon } from "lucide-react";

import { TooltipIconButton } from "@/components/assistant-ui/tooltip-icon-button";
import { Button } from "@/components/ui/button";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";

import type { InboxView } from "../hooks/useHandoffs";

const ViewLabel: Record<InboxView, string> = { open: "Open", done: "Done" };

/**
 * The panel's top row: the "Conversations" title, the Open/Done picker, and the two tools that
 * act on the list below rather than on one row.
 */
export function InboxHeader({
  view,
  onViewChange,
  oldestFirst,
  onToggleOrder,
}: {
  view: InboxView;
  onViewChange: (view: InboxView) => void;
  oldestFirst: boolean;
  onToggleOrder: () => void;
}) {
  return (
    <div className="flex items-center gap-2 px-4 pt-3.5">
      <h1 className="text-[18px] font-semibold tracking-tight">Conversations</h1>

      <DropdownMenu>
        <DropdownMenuTrigger asChild>
          <Button
            variant="secondary"
            size="sm"
            className="h-6 gap-1 rounded-md bg-muted px-2 text-[12.5px] font-normal text-muted-foreground shadow-none hover:bg-muted"
          >
            {ViewLabel[view]}
            <ChevronDownIcon className="size-3" />
          </Button>
        </DropdownMenuTrigger>
        <DropdownMenuContent align="start">
          <DropdownMenuItem onSelect={() => onViewChange("open")}>Open</DropdownMenuItem>
          <DropdownMenuItem onSelect={() => onViewChange("done")}>Done</DropdownMenuItem>
        </DropdownMenuContent>
      </DropdownMenu>

      <div className="ml-auto flex gap-0.5">
        <TooltipIconButton
          tooltip={oldestFirst ? "Oldest first" : "Newest first"}
          onClick={onToggleOrder}
        >
          <ArrowUpDownIcon />
        </TooltipIconButton>
        <TooltipIconButton tooltip="Hide the list" onClick={() => {}}>
          <PanelLeftCloseIcon />
        </TooltipIconButton>
      </div>
    </div>
  );
}
