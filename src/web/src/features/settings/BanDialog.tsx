import { useState } from "react";

import { banPerson } from "@/api/sdk.gen";
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

export function BanDialog({ person, open, onOpenChange }: Props) {
  const guard = useCloseGuard(onOpenChange);
  return (
    <AlertDialog open={open} onOpenChange={guard.onOpenChange}>
      <AlertDialogContent {...guard.contentProps}>
        <BanBody setBusy={guard.setBusy} person={person} onOpenChange={onOpenChange} />
      </AlertDialogContent>
    </AlertDialog>
  );
}

function BanBody({
  setBusy,
  person,
  onOpenChange,
}: Omit<Props, "open"> & { setBusy(value: boolean): void }) {
  const { states, running, succeeded, run } = useRun();
  useReportBusy(running, setBusy);
  const inDesk = person.desk !== "none";
  const [answer, setAnswer] = useState<BanAnswer | null>(null);

  async function ban() {
    setAnswer(null);
    const ok = await run([
      {
        label: "Ban",
        run: async () => {
          const banned = (
            await banPerson({
              throwOnError: true,
              path: { id: person.id },
              body: { reason: "Left Spirit (Settings)" },
            })
          ).data;
          setAnswer(banned);
          throwIfDeskFailed(banned);
        },
      },
    ]);
    if (ok) onOpenChange(false);
  }

  return (
    <>
      <AlertDialogHeader>
        <AlertDialogTitle>Ban {person.name}?</AlertDialogTitle>
        <AlertDialogDescription>
          Use this when a person leaves. You can unban them later and nothing is lost.
        </AlertDialogDescription>
      </AlertDialogHeader>
      <ul className="ml-4 list-disc text-sm">
        <li>They cannot sign in to Spirit, Desk or CRM. Spirit refuses them at once.</li>
        {inDesk ? (
          <li>
            Desk: they leave the Spirit account now, so no new chats go to them. Their open chats
            lose their assignee. Old messages keep their name.
          </li>
        ) : null}
        <li>
          CRM: nothing changes. Their records keep them as owner, so you can hand the records on
          later.
        </li>
        <li>Their roles stay, ready for an unban.</li>
      </ul>
      <StepList
        states={answer ? shownOf(banStepsOf("Ban", "Leave Desk", answer), answer.detail) : states}
      />
      <AlertDialogFooter>
        <AlertDialogCancel disabled={running}>Cancel</AlertDialogCancel>
        <PendingButton variant="destructive" pending={running} onClick={() => void ban()}>
          {succeeded === false ? "Try again" : "Ban"}
        </PendingButton>
      </AlertDialogFooter>
    </>
  );
}
