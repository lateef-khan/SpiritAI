import type { StepResult } from "@/api/types.gen";

import type { useSteps } from "./useSteps";

type Shown = ReturnType<typeof useSteps>["states"][number];

/**
 * A server's step outcomes, keyed by the label the step list draws, as step list rows. A step that
 * did not run shows as waiting; the reason goes on the first failure.
 */
export function shownOf(outcomes: Record<string, StepResult>, detail: string | null): Shown[] {
  let reasonGiven = false;
  return Object.entries(outcomes).map(([label, result]) => {
    const state = result === "done" ? "done" : result === "failed" ? "failed" : "waiting";
    const error =
      state === "failed" && !reasonGiven ? (detail ?? "A step did not finish.") : undefined;
    if (error) reasonGiven = true;
    return { label, state, error };
  });
}
