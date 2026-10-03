import type * as React from "react";

import { Button } from "@/components/ui/button";
import { Spinner } from "@/components/ui/spinner";
import { cn } from "@/lib/utils";

export function PendingButton({
  pending,
  disabled,
  children,
  ...props
}: React.ComponentProps<typeof Button> & { pending: boolean }) {
  return (
    <Button disabled={pending || disabled} aria-busy={pending} {...props}>
      <span className="grid place-items-center">
        <span
          className={cn(
            "col-start-1 row-start-1 inline-flex items-center gap-2",
            pending && "invisible",
          )}
        >
          {children}
        </span>
        {pending ? <Spinner className="col-start-1 row-start-1" /> : null}
      </span>
    </Button>
  );
}
