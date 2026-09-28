import {
  AssistantRuntimeProvider,
  useExternalStoreRuntime,
  type ThreadMessage,
} from "@assistant-ui/react";
import { cleanup, render, screen } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, test, vi } from "vitest";

import { ThreadContextPanel } from "./ThreadContextPanel";

/**
 * The panel beside the chat. `@/api/sdk.gen` is mocked the same way `UnitPanel.test.tsx` mocks
 * it; the panel runs under a bare external-store runtime, which is all it reads through
 * `useAuiState`.
 */
vi.mock("@/api/sdk.gen", () => ({ getUnit: vi.fn(), getOrder: vi.fn() }));

afterEach(cleanup);
beforeEach(() => vi.resetAllMocks());

function EmptyThread() {
  const runtime = useExternalStoreRuntime({
    messages: [] as ThreadMessage[],
    onNew: () => Promise.resolve(),
  });
  return (
    <AssistantRuntimeProvider runtime={runtime}>
      <ThreadContextPanel className="border-l-0" />
    </AssistantRuntimeProvider>
  );
}

describe("ThreadContextPanel", () => {
  test("shows the unit panel for a thread that names no unit yet", async () => {
    render(<EmptyThread />);

    expect(await screen.findByText("No information yet")).toBeTruthy();
  });
});
