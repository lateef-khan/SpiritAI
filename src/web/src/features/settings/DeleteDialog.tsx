import { useState } from "react";

import { deletePerson } from "@/api/sdk.gen";
import type { PersonDeletion, PersonRow, StepResult } from "@/api/types.gen";
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
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { HostRefusedError } from "@/lib/apiClient";

import { useCloseGuard, useReportBusy } from "./useCloseGuard";
import { StepList } from "./StepList";
import { shownOf } from "./stepOutcomes";
import { useRun } from "./useRun";

type Props = { person: PersonRow; open: boolean; onOpenChange(open: boolean): void };

/** The steps Delete ran, in the order the server runs them; a step it had no need for is left out. */
function stepsOf(removed: PersonDeletion): Record<string, StepResult> {
  const all: [string, StepResult][] = [
    ["Leave Desk", removed.desk],
    ["Delete in CRM", removed.crm],
    ["Delete the Spirit sign-in", removed.neon],
  ];
  return Object.fromEntries(all.filter(([, result]) => result !== "none"));
}

export function DeleteDialog({ person, open, onOpenChange }: Props) {
  const guard = useCloseGuard(onOpenChange);
  return (
    <AlertDialog open={open} onOpenChange={guard.onOpenChange}>
      <AlertDialogContent {...guard.contentProps}>
        <DeleteBody setBusy={guard.setBusy} person={person} onOpenChange={onOpenChange} />
      </AlertDialogContent>
    </AlertDialog>
  );
}

function DeleteBody({
  setBusy,
  person,
  onOpenChange,
}: Omit<Props, "open"> & { setBusy(value: boolean): void }) {
  const { states, running, succeeded, run } = useRun();
  useReportBusy(running, setBusy);
  const [typed, setTyped] = useState("");
  const confirmed = typed.trim().toLowerCase() === person.email.toLowerCase();
  const [answer, setAnswer] = useState<PersonDeletion | null>(null);

  async function remove() {
    setAnswer(null);
    const ok = await run([
      {
        label: "Delete",
        run: async () => {
          const removed = (await deletePerson({ throwOnError: true, path: { id: person.id } }))
            .data;
          setAnswer(removed);
          if ([removed.desk, removed.crm, removed.neon].includes("failed")) {
            throw new HostRefusedError(
              503,
              "people",
              null,
              removed.detail ?? "A step did not finish.",
            );
          }
        },
      },
    ]);
    if (ok) onOpenChange(false);
  }

  return (
    <>
      <AlertDialogHeader>
        <AlertDialogTitle>Delete {person.name}?</AlertDialogTitle>
        <AlertDialogDescription>
          Use this only for an account made by mistake. <b>You cannot undo it.</b>
        </AlertDialogDescription>
      </AlertDialogHeader>
      <div className="border-destructive/50 bg-destructive/10 rounded-md border px-3 py-2 text-sm">
        <p>
          Desk: they leave the Spirit account. Their Chatwoot user stays, so old messages keep their
          name.
        </p>
        <p>
          CRM: the Twenty member is deleted. Records they own lose their owner, and then only admins
          see them.
        </p>
        <p>Spirit: the sign-in, the roles and the links are deleted.</p>
      </div>
      <p className="text-sm">
        For a person who left, use <b>Ban</b> instead.
      </p>
      <div className="flex flex-col gap-2">
        <Label htmlFor="delete-confirm">
          Type <b>{person.email}</b> to confirm
        </Label>
        <Input
          id="delete-confirm"
          autoComplete="off"
          value={typed}
          disabled={running || succeeded === true}
          onChange={(event) => setTyped(event.target.value)}
        />
      </div>
      <StepList states={answer ? shownOf(stepsOf(answer), answer.detail) : states} />
      <AlertDialogFooter>
        <AlertDialogCancel disabled={running}>
          {succeeded === true ? "Close" : "Cancel"}
        </AlertDialogCancel>
        {succeeded === true ? null : (
          <PendingButton
            variant="destructive"
            pending={running}
            disabled={!confirmed}
            onClick={() => void remove()}
          >
            {succeeded === false ? "Try again" : "Delete for good"}
          </PendingButton>
        )}
      </AlertDialogFooter>
    </>
  );
}
