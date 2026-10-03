/** The ⋯ menu on a row. It owns which dialog or sheet is open; those render beside the menu, not inside it. */
import { BanIcon, EllipsisIcon, PencilIcon, RotateCcwIcon, Trash2Icon } from "lucide-react";
import { useState } from "react";

import type { PersonRow } from "@/api/types.gen";
import { Button } from "@/components/ui/button";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";

import { BanDialog } from "./BanDialog";
import { DeleteDialog } from "./DeleteDialog";
import { EditRolesSheet } from "./EditRolesSheet";
import { UnbanDialog } from "./UnbanDialog";

type Open = "roles" | "ban" | "unban" | "delete" | null;

const SELF = "You cannot do this to your own account";

export function PersonActions({ person, isYou }: { person: PersonRow; isYou: boolean }) {
  const [open, setOpen] = useState<Open>(null);
  const [rolesOpenCount, setRolesOpenCount] = useState(0);

  function show(next: Exclude<Open, null>) {
    if (next === "roles") setRolesOpenCount((count) => count + 1);
    setOpen(next);
  }

  const close = (value: boolean) => {
    if (!value) setOpen(null);
  };

  return (
    <>
      <DropdownMenu>
        <DropdownMenuTrigger asChild>
          <Button variant="ghost" size="icon-sm" aria-label={`Actions for ${person.name}`}>
            <EllipsisIcon />
          </Button>
        </DropdownMenuTrigger>
        <DropdownMenuContent align="end">
          <DropdownMenuItem onSelect={() => show("roles")}>
            <PencilIcon />
            Edit roles
          </DropdownMenuItem>
          <DropdownMenuSeparator />
          {person.banned ? (
            <DropdownMenuItem onSelect={() => show("unban")}>
              <RotateCcwIcon />
              Unban
            </DropdownMenuItem>
          ) : (
            <DropdownMenuItem variant="destructive" disabled={isYou} onSelect={() => show("ban")}>
              <BanIcon />
              <span title={isYou ? SELF : undefined}>Ban…</span>
            </DropdownMenuItem>
          )}
          <DropdownMenuItem variant="destructive" disabled={isYou} onSelect={() => show("delete")}>
            <Trash2Icon />
            <span title={isYou ? SELF : undefined}>Delete…</span>
          </DropdownMenuItem>
        </DropdownMenuContent>
      </DropdownMenu>
      {open === "roles" ? (
        <EditRolesSheet
          key={`${person.id}-${rolesOpenCount}`}
          person={person}
          open
          onOpenChange={close}
        />
      ) : null}
      {open === "ban" ? <BanDialog person={person} open onOpenChange={close} /> : null}
      {open === "unban" ? <UnbanDialog person={person} open onOpenChange={close} /> : null}
      {open === "delete" ? <DeleteDialog person={person} open onOpenChange={close} /> : null}
    </>
  );
}
