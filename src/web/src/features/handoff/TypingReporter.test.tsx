import { act, render } from "@testing-library/react";
import {
  AssistantRuntimeProvider,
  useExternalStoreRuntime,
  type AssistantRuntime,
  type ThreadMessageLike,
} from "@assistant-ui/react";
import { useEffect } from "react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import { TypingReporter } from "./TypingReporter";

/**
 * The reporter speaks on each edge only: "typing" when the box fills, "stopped" when it empties.
 */
function Harness({
  sayTyping,
  onRuntime,
}: {
  sayTyping: (on: boolean) => void;
  onRuntime: (runtime: AssistantRuntime) => void;
}) {
  const runtime = useExternalStoreRuntime<ThreadMessageLike>({
    messages: [],
    onNew: async () => {},
    convertMessage: (message) => message,
  });
  // Handed out so the test can type into the composer.
  useEffect(() => {
    onRuntime(runtime);
  }, [onRuntime, runtime]);
  return (
    <AssistantRuntimeProvider runtime={runtime}>
      <TypingReporter sayTyping={sayTyping} />
    </AssistantRuntimeProvider>
  );
}

beforeEach(() => vi.useFakeTimers());
afterEach(() => vi.useRealTimers());

describe("TypingReporter", () => {
  it("says typing once when the box fills and stopped once when it empties", () => {
    const said: boolean[] = [];
    let runtime: AssistantRuntime | undefined;
    render(<Harness sayTyping={(on) => said.push(on)} onRuntime={(r) => (runtime = r)} />);
    expect(said).toEqual([]);

    act(() => runtime!.thread.composer.setText("hel"));
    act(() => runtime!.thread.composer.setText("hello"));
    act(() => vi.advanceTimersByTime(60_000));
    expect(said).toEqual([true]);

    act(() => runtime!.thread.composer.setText(""));
    expect(said).toEqual([true, false]);
  });
});
