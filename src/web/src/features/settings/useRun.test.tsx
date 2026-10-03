import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { act, renderHook, waitFor } from "@testing-library/react";
import type { ReactNode } from "react";
import { expect, test } from "vitest";

import { useRun } from "./useRun";

test("stays running until the people refetch has finished", async () => {
  const queryClient = new QueryClient();
  let finishRefetch!: () => void;
  queryClient.invalidateQueries = () =>
    new Promise<void>((resolve) => {
      finishRefetch = resolve;
    });
  const wrapper = ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
  );
  const { result } = renderHook(() => useRun(), { wrapper });

  let finished: Promise<boolean>;
  act(() => {
    finished = result.current.run([{ label: "One", run: async () => {} }]);
  });
  await waitFor(() => expect(result.current.states[0]?.state).toBe("done"));

  expect(result.current.running).toBe(true);

  await act(async () => {
    finishRefetch();
    await finished;
  });
  expect(result.current.running).toBe(false);
});
