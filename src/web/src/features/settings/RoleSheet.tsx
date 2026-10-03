import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useId, useState } from "react";

import { createRole, updateRole } from "@/api/sdk.gen";
import type { Permission, PermissionInfo, RoleRow } from "@/api/types.gen";
import { PendingButton } from "@/components/PendingButton";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  Sheet,
  SheetContent,
  SheetDescription,
  SheetFooter,
  SheetHeader,
  SheetTitle,
} from "@/components/ui/sheet";
import { HostRefusedError } from "@/lib/apiClient";

import { PEOPLE_QUERY_KEY } from "./peopleQueryKey";
import { ROLES_QUERY_KEY } from "./rolesQueryKey";

/** Makes a role (`role` null) or edits one, with its permissions as checkboxes under their headings. */
export function RoleSheet({
  role,
  permissions,
  open,
  onOpenChange,
}: {
  role: RoleRow | null;
  permissions: PermissionInfo[];
  open: boolean;
  onOpenChange(open: boolean): void;
}) {
  const queryClient = useQueryClient();
  const id = useId();
  const [name, setName] = useState(role?.name ?? "");
  const [description, setDescription] = useState(role?.description ?? "");
  const [held, setHeld] = useState<Permission[]>(role?.permissions ?? []);
  const [error, setError] = useState<string | null>(null);

  const groups = [...new Set(permissions.map((info) => info.group))];

  const save = useMutation({
    mutationFn: () => {
      const body = {
        name: name.trim(),
        description: description.trim() === "" ? null : description.trim(),
        permissions: permissions.map((info) => info.key).filter((key) => held.includes(key)),
      };
      return role
        ? updateRole({ throwOnError: true, path: { id: role.id }, body })
        : createRole({ throwOnError: true, body });
    },
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ROLES_QUERY_KEY });
      void queryClient.invalidateQueries({ queryKey: PEOPLE_QUERY_KEY });
      onOpenChange(false);
    },
    onError: (thrown) =>
      setError(
        thrown instanceof HostRefusedError && thrown.detail
          ? thrown.detail
          : "Could not save. Nothing changed.",
      ),
  });

  function toggle(key: Permission, on: boolean) {
    setHeld((current) => (on ? [...current, key] : current.filter((held) => held !== key)));
  }

  return (
    <Sheet open={open} onOpenChange={onOpenChange}>
      <SheetContent className="flex flex-col sm:max-w-md">
        <SheetHeader>
          <SheetTitle>{role ? `Edit ${role.name}` : "New role"}</SheetTitle>
          <SheetDescription>A role is a set of permissions you give to people.</SheetDescription>
        </SheetHeader>
        <div className="flex min-h-0 flex-1 flex-col gap-4 overflow-auto px-4">
          <div className="flex flex-col gap-2">
            <Label htmlFor={`${id}-name`}>Name</Label>
            <Input
              id={`${id}-name`}
              value={name}
              onChange={(event) => setName(event.target.value)}
            />
          </div>
          <div className="flex flex-col gap-2">
            <Label htmlFor={`${id}-description`}>Description</Label>
            <Input
              id={`${id}-description`}
              value={description}
              onChange={(event) => setDescription(event.target.value)}
            />
          </div>
          {groups.map((group) => (
            <div key={group} className="flex flex-col gap-1">
              <h4 className="text-muted-foreground text-xs font-medium tracking-wide uppercase">
                {group}
              </h4>
              {permissions
                .filter((info) => info.group === group)
                .map((info) => (
                  <div key={info.key} className="flex items-center gap-2 px-1 py-1">
                    <Checkbox
                      id={`${id}-${info.key}`}
                      checked={held.includes(info.key)}
                      onCheckedChange={(on) => toggle(info.key, on === true)}
                    />
                    <Label htmlFor={`${id}-${info.key}`} className="font-normal">
                      {info.label}
                    </Label>
                  </div>
                ))}
            </div>
          ))}
        </div>
        <SheetFooter className="flex-row items-center justify-end gap-2">
          {error ? (
            <p role="alert" className="text-destructive mr-auto text-xs">
              {error}
            </p>
          ) : null}
          <Button variant="outline" onClick={() => onOpenChange(false)}>
            Cancel
          </Button>
          <PendingButton
            pending={save.isPending}
            onClick={() => {
              setError(null);
              save.mutate();
            }}
          >
            Save
          </PendingButton>
        </SheetFooter>
      </SheetContent>
    </Sheet>
  );
}
