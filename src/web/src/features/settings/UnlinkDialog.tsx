import { unlinkPerson } from "@/api/sdk.gen";
import type { PersonRow } from "@/api/types.gen";
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

import { useCloseGuard, useReportBusy } from "./useCloseGuard";
import { StepList } from "./StepList";
import { useRun } from "./useRun";

type Props = {
  person: PersonRow;
  app: "desk" | "crm";
  open: boolean;
  onOpenChange(open: boolean): void;
};

export function UnlinkDialog({ person, app, open, onOpenChange }: Props) {
  const guard = useCloseGuard(onOpenChange);
  return (
    <AlertDialog open={open} onOpenChange={guard.onOpenChange}>
      <AlertDialogContent {...guard.contentProps}>
        <UnlinkBody setBusy={guard.setBusy} person={person} app={app} onOpenChange={onOpenChange} />
      </AlertDialogContent>
    </AlertDialog>
  );
}

function UnlinkBody({
  setBusy,
  person,
  app,
  onOpenChange,
}: Omit<Props, "open"> & { setBusy(value: boolean): void }) {
  const { states, running, succeeded, run } = useRun();
  useReportBusy(running, setBusy);
  const desk = app === "desk";

  async function unlink() {
    const ok = await run([
      {
        label: desk ? "Take them out of the Desk account" : "Remove the CRM member",
        run: async () => {
          await unlinkPerson({ throwOnError: true, path: { id: person.id, app } });
        },
      },
    ]);
    if (ok) onOpenChange(false);
  }

  return (
    <>
      <AlertDialogHeader>
        <AlertDialogTitle>
          Unlink {desk ? "Desk" : "CRM"} for {person.name}?
        </AlertDialogTitle>
        <AlertDialogDescription>
          {desk
            ? "They leave the Spirit account in Chatwoot. Their open chats lose their assignee. Their old messages keep their name."
            : "Create makes a new CRM member. Old records do not come back to them."}
        </AlertDialogDescription>
      </AlertDialogHeader>
      {desk ? (
        <p className="text-muted-foreground text-sm">
          Create links them again as the same Chatwoot user.
        </p>
      ) : (
        <p className="rounded-md border border-amber-500/50 bg-amber-500/10 px-3 py-2 text-sm">
          <b>Their CRM records lose their owner.</b> Then only admins can see those records, until
          someone gives them a new owner. Do that first in Twenty.
        </p>
      )}
      <StepList states={states} />
      <AlertDialogFooter>
        <AlertDialogCancel disabled={running}>Cancel</AlertDialogCancel>
        <PendingButton variant="destructive" pending={running} onClick={() => void unlink()}>
          {succeeded === false ? "Try again" : "Unlink"}
        </PendingButton>
      </AlertDialogFooter>
    </>
  );
}
