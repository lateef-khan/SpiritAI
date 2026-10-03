import { useMutation, useQueryClient } from "@tanstack/react-query";
import { XIcon } from "lucide-react";
import { useRef, useState } from "react";

import { linkPerson } from "@/api/sdk.gen";
import type { LinkState, PersonRow } from "@/api/types.gen";
import { PendingButton } from "@/components/PendingButton";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Tooltip, TooltipContent, TooltipTrigger } from "@/components/ui/tooltip";
import { HostRefusedError } from "@/lib/apiClient";

import { PEOPLE_QUERY_KEY } from "./peopleQueryKey";
import { UnlinkDialog } from "./UnlinkDialog";

export function PersonLinkButton({
  person,
  app,
  state,
  disabled = false,
  isYou = false,
}: {
  person: PersonRow;
  app: "desk" | "crm";
  state: LinkState;
  disabled?: boolean;
  isYou?: boolean;
}) {
  const queryClient = useQueryClient();
  const pendingRef = useRef(false);
  const [error, setError] = useState<string | null>(null);
  const [unlinking, setUnlinking] = useState(false);

  const mutation = useMutation({
    mutationFn: () => linkPerson({ throwOnError: true, path: { id: person.id, app } }),
    onSuccess: () => {
      setError(null);
      void queryClient.invalidateQueries({ queryKey: PEOPLE_QUERY_KEY });
    },
    onError: (thrown: unknown) => {
      setError(
        thrown instanceof HostRefusedError && thrown.detail
          ? thrown.detail
          : "Could not reach the server.",
      );
    },
  });

  if (state === "ready") {
    return (
      <div className="flex items-center gap-1">
        <Badge variant="secondary">Linked</Badge>
        <Tooltip>
          <TooltipTrigger asChild>
            <Button
              variant="ghost"
              size="icon-xs"
              disabled={disabled || isYou}
              aria-label={app === "desk" ? "Unlink Desk" : "Unlink CRM"}
              onClick={() => setUnlinking(true)}
            >
              <XIcon />
            </Button>
          </TooltipTrigger>
          <TooltipContent>Unlink</TooltipContent>
        </Tooltip>
        {unlinking ? (
          <UnlinkDialog person={person} app={app} open onOpenChange={setUnlinking} />
        ) : null}
      </div>
    );
  }

  function handleClick() {
    if (pendingRef.current) return;
    pendingRef.current = true;
    setError(null);
    mutation.mutate(undefined, {
      onSettled: () => {
        pendingRef.current = false;
      },
    });
  }

  return (
    <div className="flex flex-col items-start gap-1">
      <PendingButton
        size="sm"
        variant="outline"
        pending={mutation.isPending}
        disabled={disabled}
        onClick={handleClick}
      >
        {state === "unfinished" ? "Finish" : "Create"}
      </PendingButton>
      {error ? (
        <p role="alert" className="text-destructive text-xs">
          {error}
        </p>
      ) : null}
    </div>
  );
}
