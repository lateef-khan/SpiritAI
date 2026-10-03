import type { BanAnswer, StepResult } from "@/api/types.gen";
import { HostRefusedError } from "@/lib/apiClient";

/** A Ban or Unban answer as step outcomes: the ban itself, then the Desk step when there was one. */
export function banStepsOf(
  label: string,
  deskLabel: string,
  answer: BanAnswer,
): Record<string, StepResult> {
  return answer.desk === "none"
    ? { [label]: "done" }
    : { [label]: "done", [deskLabel]: answer.desk };
}

/** Fails the run when the Desk step failed, so the dialog stays open and offers Try again. */
export function throwIfDeskFailed(answer: BanAnswer) {
  if (answer.desk === "failed") {
    throw new HostRefusedError(503, "people", null, answer.detail ?? "Desk did not answer.");
  }
}
