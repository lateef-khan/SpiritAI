import { useQuery } from "@tanstack/react-query";
import { useState } from "react";

import { addPerson, listRoles } from "@/api/sdk.gen";
import type { AddSteps, AddedPerson, StepResult } from "@/api/types.gen";
import { PendingButton } from "@/components/PendingButton";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { HostRefusedError } from "@/lib/apiClient";

import { RolePicker } from "./RolePicker";
import { ROLES_QUERY_KEY } from "./rolesQueryKey";
import { useCloseGuard, useReportBusy } from "./useCloseGuard";
import { usePermissions } from "./usePermissions";
import { StepList } from "./StepList";
import { shownOf } from "./stepOutcomes";
import { useRun } from "./useRun";

function stepsOf(steps: AddSteps, desk: boolean, crm: boolean) {
  return {
    "Make the Spirit sign-in": steps.neon,
    "Save the roles": steps.roles,
    ...(desk ? { "Link Desk": steps.desk } : {}),
    ...(crm ? { "Link CRM": steps.crm } : {}),
  } as Record<string, StepResult>;
}

type Props = { open: boolean; onOpenChange(open: boolean): void };

export function AddPersonDialog({ open, onOpenChange }: Props) {
  const guard = useCloseGuard(onOpenChange);
  return (
    <Dialog open={open} onOpenChange={guard.onOpenChange}>
      <DialogContent className="sm:max-w-xl" {...guard.contentProps}>
        <AddPersonForm setBusy={guard.setBusy} onOpenChange={onOpenChange} />
      </DialogContent>
    </Dialog>
  );
}

function AddPersonForm({
  setBusy,
  onOpenChange,
}: Omit<Props, "open"> & { setBusy(value: boolean): void }) {
  const [name, setName] = useState("");
  const [email, setEmail] = useState("");
  const [roles, setRoles] = useState<string[]>([]);
  const [desk, setDesk] = useState(false);
  const [crm, setCrm] = useState(false);
  const [formError, setFormError] = useState<string | null>(null);
  const { states, running, succeeded, run } = useRun();
  useReportBusy(running, setBusy);
  const roleRows = useQuery({
    queryKey: ROLES_QUERY_KEY,
    queryFn: async () => (await listRoles({ throwOnError: true })).data,
  });
  const permissions = usePermissions();
  const [answer, setAnswer] = useState<AddedPerson | null>(null);

  async function submit() {
    const cleanName = name.trim();
    const cleanEmail = email.trim().toLowerCase();
    if (!cleanName || !cleanEmail.includes("@")) {
      setFormError("Type a name and an email.");
      return;
    }
    setFormError(null);
    setAnswer(null);

    await run([
      {
        label: "Add the person",
        run: async () => {
          const added = (
            await addPerson({
              throwOnError: true,
              body: { name: cleanName, email: cleanEmail, roleIds: roles, desk, crm },
            })
          ).data;
          setAnswer(added);
          if (Object.values(stepsOf(added.steps, desk, crm)).includes("failed")) {
            throw new HostRefusedError(
              503,
              "people",
              null,
              added.steps.detail ?? "A step did not finish.",
            );
          }
        },
      },
    ]);
  }

  // Add again does only what is left while the sign-in or the roles failed. Once both are done, a
  // second Add would find the email taken, so a failed Desk or CRM step is finished from the row.
  const retryable =
    answer !== null && (answer.steps.neon === "failed" || answer.steps.roles === "failed");
  const made = answer?.person != null && !retryable;
  const locked = running || made;
  const identityLocked = running || answer?.person != null;

  return (
    <>
      <DialogHeader>
        <DialogTitle>Add person</DialogTitle>
        <DialogDescription>
          They sign in with an email code. Nobody gets a password.
        </DialogDescription>
      </DialogHeader>
      <div className="flex min-w-0 flex-col gap-4">
        <div className="flex flex-col gap-2">
          <Label htmlFor="add-person-name">Name</Label>
          <Input
            id="add-person-name"
            placeholder="Lateef Khan"
            value={name}
            disabled={identityLocked}
            onChange={(event) => setName(event.target.value)}
          />
        </div>
        <div className="flex flex-col gap-2">
          <Label htmlFor="add-person-email">Email</Label>
          <Input
            id="add-person-email"
            type="email"
            placeholder="lateef.khan@spiritfitness.com"
            value={email}
            disabled={identityLocked}
            onChange={(event) => setEmail(event.target.value)}
          />
        </div>
        <div className="flex flex-col gap-2">
          <Label>Roles</Label>
          <div className="max-h-64 min-w-0 overflow-auto rounded-md border p-2">
            {roleRows.data && permissions.data ? (
              <RolePicker
                roles={roleRows.data}
                permissions={permissions.data}
                value={roles}
                onChange={setRoles}
              />
            ) : (
              <p className="text-muted-foreground text-sm">Loading…</p>
            )}
          </div>
        </div>
        <div className="flex flex-col gap-2">
          <div className="flex items-center gap-2">
            <Checkbox
              id="add-person-desk"
              checked={desk}
              disabled={locked}
              onCheckedChange={(on) => setDesk(on === true)}
            />
            <Label htmlFor="add-person-desk" className="font-normal">
              Also link Desk
            </Label>
          </div>
          <div className="flex items-center gap-2">
            <Checkbox
              id="add-person-crm"
              checked={crm}
              disabled={locked}
              onCheckedChange={(on) => setCrm(on === true)}
            />
            <Label htmlFor="add-person-crm" className="font-normal">
              Also link CRM
            </Label>
          </div>
        </div>
      </div>
      {formError ? (
        <p role="alert" className="text-destructive text-sm">
          {formError}
        </p>
      ) : null}
      <StepList
        states={answer ? shownOf(stepsOf(answer.steps, desk, crm), answer.steps.detail) : states}
      />
      {made && succeeded === false ? (
        <p className="text-sm">Finish the rest from their row in the table.</p>
      ) : null}
      <DialogFooter>
        <Button variant="outline" disabled={running} onClick={() => onOpenChange(false)}>
          Cancel
        </Button>
        {succeeded === true || made ? (
          <Button onClick={() => onOpenChange(false)}>Done</Button>
        ) : (
          <PendingButton pending={running} onClick={() => void submit()}>
            {succeeded === false ? "Try again" : "Add person"}
          </PendingButton>
        )}
      </DialogFooter>
    </>
  );
}
