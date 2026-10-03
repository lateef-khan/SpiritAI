import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";

import { listRoles, setPersonRoles } from "@/api/sdk.gen";
import type { PersonRow } from "@/api/types.gen";
import { PendingButton } from "@/components/PendingButton";
import { Button } from "@/components/ui/button";
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
import { RolePicker } from "./RolePicker";
import { ROLES_QUERY_KEY } from "./rolesQueryKey";
import { usePermissions } from "./usePermissions";

export function EditRolesSheet({
  person,
  open,
  onOpenChange,
}: {
  person: PersonRow;
  open: boolean;
  onOpenChange(open: boolean): void;
}) {
  const queryClient = useQueryClient();
  const [value, setValue] = useState(person.roles.map((role) => role.id));
  const [error, setError] = useState<string | null>(null);
  const roles = useQuery({
    queryKey: ROLES_QUERY_KEY,
    queryFn: async () => (await listRoles({ throwOnError: true })).data,
  });
  const permissions = usePermissions();

  const save = useMutation({
    mutationFn: () =>
      setPersonRoles({ throwOnError: true, path: { id: person.id }, body: { roleIds: value } }),
    onSuccess: () => {
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

  return (
    <Sheet open={open} onOpenChange={onOpenChange}>
      <SheetContent className="flex flex-col sm:max-w-md">
        <SheetHeader>
          <SheetTitle>Roles for {person.name}</SheetTitle>
          <SheetDescription>{person.email}</SheetDescription>
        </SheetHeader>
        <div className="min-h-0 flex-1 overflow-auto px-4">
          {roles.data && permissions.data ? (
            <RolePicker
              roles={roles.data}
              permissions={permissions.data}
              value={value}
              onChange={setValue}
            />
          ) : (
            <p className="text-muted-foreground">Loading…</p>
          )}
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
