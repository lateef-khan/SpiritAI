/**
 * Settings → Roles: every role, drawn a screenful at a time, a sheet to make or edit one, and a
 * dialog to delete one. Admin is locked.
 */
import { useQuery } from "@tanstack/react-query";
import { useVirtualizer } from "@tanstack/react-virtual";
import { LockIcon, PencilIcon, PlusIcon, Trash2Icon } from "lucide-react";
import { useState } from "react";

import { listRoles } from "@/api/sdk.gen";
import type { RoleRow } from "@/api/types.gen";
import { Button } from "@/components/ui/button";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";

import { DeleteRoleDialog } from "./DeleteRoleDialog";
import { RoleSheet } from "./RoleSheet";
import { ROLES_QUERY_KEY } from "./rolesQueryKey";
import { usePermissions } from "./usePermissions";
import { SpacerRow, spacersOf } from "./VirtualSpacerRow";

const ROW_HEIGHT = 57;

type Open = { kind: "edit"; role: RoleRow | null } | { kind: "delete"; role: RoleRow } | null;

export function RolesSection({ scrollElement }: { scrollElement: HTMLElement | null }) {
  const roles = useQuery({
    queryKey: ROLES_QUERY_KEY,
    queryFn: async () => (await listRoles({ throwOnError: true })).data,
  });
  const permissions = usePermissions();
  const [open, setOpen] = useState<Open>(null);
  const [sheetCount, setSheetCount] = useState(0);

  const rows = roles.data ?? [];
  const virtualizer = useVirtualizer({
    count: rows.length,
    getScrollElement: () => scrollElement,
    estimateSize: () => ROW_HEIGHT,
    getItemKey: (index) => rows[index]!.id,
    overscan: 10,
  });
  const { items, above, below } = spacersOf(virtualizer);

  if (roles.isPending || permissions.isPending) {
    return <p className="text-muted-foreground p-4">Loading…</p>;
  }

  if (roles.error || permissions.error || !roles.data || !permissions.data) {
    return <p className="text-destructive p-4">Could not load Roles.</p>;
  }

  function edit(role: RoleRow | null) {
    setSheetCount((count) => count + 1);
    setOpen({ kind: "edit", role });
  }

  const close = (value: boolean) => {
    if (!value) setOpen(null);
  };

  return (
    <div className="flex flex-col px-4 pb-4">
      <div className="bg-background sticky top-0 z-20 flex h-14 flex-none items-center">
        <div className="flex-1" />
        <Button onClick={() => edit(null)}>
          <PlusIcon />
          New role
        </Button>
      </div>
      {/* The table's own wrapper scrolls sideways, which would trap the sticky header inside it. */}
      <div className="[&_[data-slot=table-container]]:overflow-visible">
        <Table className="table-fixed">
          <TableHeader className="bg-background sticky top-14 z-10">
            <TableRow>
              <TableHead>Name</TableHead>
              <TableHead className="w-28">Members</TableHead>
              <TableHead className="w-28">Permissions</TableHead>
              <TableHead className="w-24" />
            </TableRow>
          </TableHeader>
          <TableBody>
            <SpacerRow height={above} colSpan={4} />
            {items.map((item) => {
              const role = rows[item.index]!;
              return (
                <TableRow key={item.key} data-index={item.index} ref={virtualizer.measureElement}>
                  <TableCell>
                    <div className="flex min-w-0 items-center gap-2 font-medium">
                      {role.builtIn ? (
                        <LockIcon className="size-4 shrink-0" aria-label="Built in" />
                      ) : null}
                      <span className="truncate" title={role.name}>
                        {role.name}
                      </span>
                    </div>
                    {role.description ? (
                      <div className="text-muted-foreground truncate text-xs">
                        {role.description}
                      </div>
                    ) : null}
                  </TableCell>
                  <TableCell>{role.members}</TableCell>
                  <TableCell>{role.builtIn ? "All" : role.permissions.length}</TableCell>
                  <TableCell>
                    <div className="flex justify-end gap-1">
                      <Button
                        variant="ghost"
                        size="icon-sm"
                        aria-label={`Edit ${role.name}`}
                        disabled={role.builtIn}
                        onClick={() => edit(role)}
                      >
                        <PencilIcon />
                      </Button>
                      <Button
                        variant="ghost"
                        size="icon-sm"
                        aria-label={`Delete ${role.name}`}
                        disabled={role.builtIn}
                        onClick={() => setOpen({ kind: "delete", role })}
                      >
                        <Trash2Icon />
                      </Button>
                    </div>
                  </TableCell>
                </TableRow>
              );
            })}
            <SpacerRow height={below} colSpan={4} />
          </TableBody>
        </Table>
      </div>
      {open?.kind === "edit" ? (
        <RoleSheet
          key={sheetCount}
          role={open.role}
          permissions={permissions.data}
          open
          onOpenChange={close}
        />
      ) : null}
      {open?.kind === "delete" ? (
        <DeleteRoleDialog role={open.role} open onOpenChange={close} />
      ) : null}
    </div>
  );
}
