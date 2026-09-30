import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { AuthGate } from "./features/auth/AuthGate";
import { HubPage } from "./features/hub/HubPage";
import "./index.css";

createRoot(document.getElementById("root")!).render(
  <StrictMode>
    <AuthGate>
      <HubPage />
    </AuthGate>
  </StrictMode>,
);
