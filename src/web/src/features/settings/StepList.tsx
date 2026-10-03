import { CheckIcon, CircleIcon, XIcon } from "lucide-react";

import { Spinner } from "@/components/ui/spinner";

import type { useSteps } from "./useSteps";

function Mark({ state }: { state: ReturnType<typeof useSteps>["states"][number]["state"] }) {
  if (state === "running") return <Spinner />;
  if (state === "done") return <CheckIcon className="size-4 text-green-600" />;
  if (state === "failed") return <XIcon className="size-4" />;
  return <CircleIcon className="text-muted-foreground size-4" />;
}

export function StepList({ states }: { states: ReturnType<typeof useSteps>["states"] }) {
  if (states.length === 0) return null;
  return (
    <ul className="mt-3 flex flex-col gap-1.5 text-sm">
      {states.map((step) => (
        <li key={step.label} className="flex flex-col" data-state={step.state}>
          <span
            className={
              step.state === "failed"
                ? "text-destructive flex items-center gap-2"
                : "flex items-center gap-2"
            }
          >
            <Mark state={step.state} />
            {step.label}
          </span>
          {step.error ? (
            <span role="alert" className="text-destructive ml-6 text-xs">
              {step.error}
            </span>
          ) : null}
        </li>
      ))}
    </ul>
  );
}
