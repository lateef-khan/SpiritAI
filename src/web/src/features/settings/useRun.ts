import { useQueryClient } from "@tanstack/react-query";
import { useState } from "react";

import { PEOPLE_QUERY_KEY } from "./peopleQueryKey";
import { type Step, useSteps } from "./useSteps";

/**
 * Runs steps, refreshes the people list whatever happened, and remembers if all steps passed.
 * `running` holds until the refresh is done, so a button cannot start a second run on stale rows.
 */
export function useRun() {
  const queryClient = useQueryClient();
  const { states, running: stepping, start } = useSteps();
  const [refreshing, setRefreshing] = useState(false);
  const [succeeded, setSucceeded] = useState<boolean | null>(null);

  async function run(steps: Step[]) {
    setSucceeded(null);
    setRefreshing(true);
    try {
      const ok = await start(steps);
      await queryClient.invalidateQueries({ queryKey: PEOPLE_QUERY_KEY });
      setSucceeded(ok);
      return ok;
    } finally {
      setRefreshing(false);
    }
  }

  return { states, running: stepping || refreshing, succeeded, run };
}
