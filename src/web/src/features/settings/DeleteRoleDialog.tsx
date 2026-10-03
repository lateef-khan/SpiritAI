import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";

import { deleteRole } from "@/api/sdk.gen";
import type { RoleRow } from "@/api/types.gen";
import { PendingButton } from "@/components/PendingButton";
import {
  AlertDialog,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from "@/components/ui/alert-dialog";
import { HostRefusedError } from "@/lib/apiClient";

import { PEOPLE_QUERY_KEY } from "./peopleQueryKey";
import { ROLES_QUERY_KEY } from "./rolesQueryKey";

export function DeleteRoleDialog({
  role,
  open,
  onOpenChange,
}: {
  role: RoleRow;
  open: boolean;
  onOpenChange(open: boolean): void;
}) {
  const queryClient = useQueryClient();
  const [error, setError] = useState<string | null>(null);

  const remove = useMutation({
    mutationFn: () => deleteRole({ throwOnError: true, path: { id: role.id } }),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ROLES_QUERY_KEY });
      void queryClient.invalidateQueries({ queryKey: PEOPLE_QUERY_KEY });
      onOpenChange(false);
    },
    onError: (thrown) =>
      setError(
        thrown instanceof HostRefusedError && thrown.detail ? thrown.detail : "Could not delete.",
      ),
  });

  const holders =
    role.members === 0
      ? "Nobody holds it."
      : `${role.members} ${role.members === 1 ? "person holds" : "people hold"} it and will lose what it gives.`;

  return (
    <AlertDialog
      open={open}
      onOpenChange={(value) => (remove.isPending ? undefined : onOpenChange(value))}
    >
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>Delete {role.name}?</AlertDialogTitle>
          <AlertDialogDescription>{holders}</AlertDialogDescription>
        </AlertDialogHeader>
        {error ? (
          <p role="alert" className="text-destructive text-sm">
            {error}
          </p>
        ) : null}
        <AlertDialogFooter>
          <AlertDialogCancel disabled={remove.isPending}>Cancel</AlertDialogCancel>
          <PendingButton
            variant="destructive"
            pending={remove.isPending}
            onClick={() => remove.mutate()}
          >
            Delete role
          </PendingButton>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  );
}
