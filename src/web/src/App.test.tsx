import { cleanup, render, screen } from "@testing-library/react";
import type { ReactNode } from "react";
import { afterEach, beforeEach, describe, expect, test, vi } from "vitest";

/**
 * Everything the shell lays out is replaced by a labelled stand-in, so the test sees only which
 * parts the shell draws for a person who does or does not hold `lookup.units`.
 */
vi.mock("@/features/auth/useMe", () => ({ useCan: vi.fn() }));
vi.mock("@/features/auth/AuthGate", () => ({
  AuthGate: ({ children }: { children: ReactNode }) => <>{children}</>,
}));
vi.mock("@assistant-ui/react", () => ({
  AssistantRuntimeProvider: ({ children }: { children: ReactNode }) => <>{children}</>,
  useRemoteThreadListRuntime: () => ({}),
}));
vi.mock("@/components/assistant-ui/thread", () => ({
  Thread: () => <div>chat thread</div>,
}));
vi.mock("@/features/chat/AgentCoreSidebar", () => ({ AgentCoreSidebar: () => null }));
vi.mock("@/features/unit/ThreadContextPanel", () => ({
  ThreadContextPanel: () => <div>unit panel</div>,
}));
vi.mock("@/features/threads/AgentCoreRuntime", () => ({ useAgentCoreRuntime: vi.fn() }));
vi.mock("@/features/threads/AgentCoreThreadListAdapter", () => ({
  createAgentCoreThreadListAdapter: () => ({}),
  useThreadSession: vi.fn(),
}));
vi.mock("@/features/threads/useThreadListOlderMessages", () => ({
  useThreadListOlderMessages: () => undefined,
}));
vi.mock("@/features/auth/authFetch", () => ({ authFetch: vi.fn() }));
vi.mock("@/hooks/use-mobile", () => ({ useIsMobile: () => false }));

const { useCan } = await import("@/features/auth/useMe");
const { App } = await import("./App");

function holds(...permissions: string[]) {
  vi.mocked(useCan).mockImplementation((permission) => permissions.includes(permission));
}

beforeEach(() => {
  vi.resetAllMocks();
  vi.stubGlobal(
    "ResizeObserver",
    class {
      observe() {}
      unobserve() {}
      disconnect() {}
    },
  );
});
afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
});

describe("App", () => {
  test("draws the unit panel for a person who holds lookup.units", async () => {
    holds("lookup.units");
    render(<App />);

    expect(await screen.findByText("unit panel")).toBeTruthy();
  });

  test("leaves the unit panel out for a person who does not", async () => {
    holds("chat.agent.staff");
    render(<App />);

    expect(await screen.findByText("chat thread")).toBeTruthy();
    expect(screen.queryByText("unit panel")).toBeNull();
  });
});
