import { useCallback, useState } from "react";

import { HostRefusedError } from "@/lib/apiClient";

export type Step = { label: string; run: () => Promise<void> };
export type StepState = "waiting" | "running" | "done" | "failed";
type Shown = { label: string; state: StepState; error?: string };

function wordsOf(thrown: unknown): string {
  if (thrown instanceof HostRefusedError) {
    return thrown.detail ?? thrown.title ?? `The server answered ${thrown.status}.`;
  }
  return "Something went wrong.";
}

export function useSteps() {
  const [states, setStates] = useState<Shown[]>([]);
  const [running, setRunning] = useState(false);

  const start = useCallback(async (steps: Step[]) => {
    const shown: Shown[] = steps.map((step) => ({ label: step.label, state: "waiting" }));
    const show = () => setStates([...shown]);
    setRunning(true);
    show();
    try {
      for (const [index, step] of steps.entries()) {
        shown[index] = { ...shown[index], state: "running" };
        show();
        try {
          await step.run();
          shown[index] = { ...shown[index], state: "done" };
          show();
        } catch (thrown) {
          shown[index] = { ...shown[index], state: "failed", error: wordsOf(thrown) };
          show();
          return false;
        }
      }
      return true;
    } finally {
      setRunning(false);
    }
  }, []);

  return { states, running, start };
}
