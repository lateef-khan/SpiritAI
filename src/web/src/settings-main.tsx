import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { StrictMode } from "react";
import { createRoot } from "react-dom/client";

import { AuthGate } from "./features/auth/AuthGate";
import { SettingsPage } from "./features/settings/SettingsPage";
import "./index.css";

/**
 * The People list only changes through this page's own writes, and `PersonLinkButton` invalidates
 * it after each one — nothing here goes stale on a clock.
 */
const queryClient = new QueryClient({
  defaultOptions: { queries: { staleTime: Infinity, retry: 1 } },
});

createRoot(document.getElementById("root")!).render(
  <StrictMode>
    <AuthGate>
      <QueryClientProvider client={queryClient}>
        <SettingsPage />
      </QueryClientProvider>
    </AuthGate>
  </StrictMode>,
);
