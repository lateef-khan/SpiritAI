import { useState } from "react";

import { unbanPerson } from "@/api/sdk.gen";
import type { BanAnswer, PersonRow } from "@/api/types.gen";
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

import { banStepsOf, throwIfDeskFailed } from "./banSteps";
import { useCloseGuard, useReportBusy } from "./useCloseGuard";
import { StepList } from "./StepList";
import { shownOf } from "./stepOutcomes";
import { useRun } from "./useRun";

type Props = { person: PersonRow; open: boolean; onOpenChange(open: boolean): void };

export function UnbanDialog({ person, open, onOpenChange }: Props) {
  const guard = useCloseGuard(onOpenChange);
  return (
    <AlertDialog open={open} onOpenChange={guard.onOpenChange}>
      <AlertDialogContent {...guard.contentProps}>
        <UnbanBody setBusy={guard.setBusy} person={person} onOpenChange={onOpenChange} />
      </AlertDialogContent>
    </AlertDialog>
  );
}

function UnbanBody({
  setBusy,
  person,
  onOpenChange,
}: Omit<Props, "open"> & { setBusy(value: boolean): void }) {
  const { states, running, succeeded, run } = useRun();
  useReportBusy(running, setBusy);
  const [answer, setAnswer] = useState<BanAnswer | null>(null);

  async function unban() {
    setAnswer(null);
    const ok = await run([
      {
        label: "Unban",
        run: async () => {
          const unbanned = (await unbanPerson({ throwOnError: true, path: { id: person.id } }))
            .data;
          setAnswer(unbanned);
          throwIfDeskFailed(unbanned);
        },
      },
    ]);
    if (ok) onOpenChange(false);
  }

  return (
    <>
      <AlertDialogHeader>
        <AlertDialogTitle>Unban {person.name}?</AlertDialogTitle>
        <AlertDialogDescription>
          They can sign in again with their old roles. Desk puts them back in the account and the
          inbox, as the same Chatwoot user. Old chats they had are not given back.
        </AlertDialogDescription>
      </AlertDialogHeader>
      <StepList
        states={
          answer ? shownOf(banStepsOf("Unban", "Rejoin Desk", answer), answer.detail) : states
        }
      />
      <AlertDialogFooter>
        <AlertDialogCancel disabled={running}>Cancel</AlertDialogCancel>
        <PendingButton pending={running} onClick={() => void unban()}>
          {succeeded === false ? "Try again" : "Unban"}
        </PendingButton>
      </AlertDialogFooter>
    </>
  );
}
