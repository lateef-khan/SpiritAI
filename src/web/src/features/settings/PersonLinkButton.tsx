/**
 * One app cell in the People table: a "Linked" badge once the app has a ready user, otherwise the
 * button that creates or resumes one.
 *
 * A `pendingRef` guards the call rather than only `mutation.isPending`: two clicks fired back to
 * back, before React has had a chance to re-render the disabled button, must still reach the
 * server once.
 */
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { LoaderIcon } from "lucide-react";
import { useRef, useState } from "react";

import { linkPerson } from "@/api/sdk.gen";
import type { LinkState } from "@/api/types.gen";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { HostRefusedError } from "@/lib/apiClient";

import { PEOPLE_QUERY_KEY } from "./peopleQueryKey";

export function PersonLinkButton({
  personId,
  app,
  state,
}: {
  personId: string;
  app: "desk" | "crm";
  state: LinkState;
}) {
  const queryClient = useQueryClient();
  const pendingRef = useRef(false);
  const [error, setError] = useState<string | null>(null);

  const mutation = useMutation({
    mutationFn: () => linkPerson({ throwOnError: true, path: { id: personId, app } }),
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
    return <Badge variant="secondary">Linked</Badge>;
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
      <Button size="sm" variant="outline" disabled={mutation.isPending} onClick={handleClick}>
        {mutation.isPending ? <LoaderIcon className="animate-spin" /> : null}
        {state === "unfinished" ? "Finish" : "Create"}
      </Button>
      {error ? (
        <p role="alert" className="text-destructive text-xs">
          {error}
        </p>
      ) : null}
    </div>
  );
}
