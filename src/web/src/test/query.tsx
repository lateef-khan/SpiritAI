import type { ReactNode } from "react";

import { QueryClient, QueryClientProvider } from "@tanstack/react-query";

/**
 * A cache for one test, and a wrapper that hands it to whatever is rendered.
 *
 * Retries are off so a refused request fails the way the test expects, at once, rather than
 * three times over with a wait between. Answers never go stale on their own, the way the app
 * sets it: only a push, or a reconnect, marks them stale.
 */
export function queryWrapper(): {
  client: QueryClient;
  wrapper: ({ children }: { children: ReactNode }) => ReactNode;
} {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false, staleTime: Infinity } },
  });

  return {
    client,
    wrapper: ({ children }) => (
      <QueryClientProvider client={client}>{children}</QueryClientProvider>
    ),
  };
}
