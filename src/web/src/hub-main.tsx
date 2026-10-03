import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { StrictMode } from "react";
import { createRoot } from "react-dom/client";

import { AuthGate } from "./features/auth/AuthGate";
import { HubPage } from "./features/hub/HubPage";
import "./index.css";

const queryClient = new QueryClient({
  defaultOptions: { queries: { staleTime: Infinity, retry: 1 } },
});

createRoot(document.getElementById("root")!).render(
  <StrictMode>
    <AuthGate>
      <QueryClientProvider client={queryClient}>
        <HubPage />
      </QueryClientProvider>
    </AuthGate>
  </StrictMode>,
);
