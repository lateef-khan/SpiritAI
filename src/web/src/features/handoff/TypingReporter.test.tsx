import { act, render } from "@testing-library/react";
import {
  AssistantRuntimeProvider,
  useExternalStoreRuntime,
  type AssistantRuntime,
  type ThreadMessageLike,
} from "@assistant-ui/react";
import { useEffect } from "react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import { TypingReporter, TypingRepeatMs } from "./TypingReporter";

/**
 * The reporter, one test per edge: "typing" when the box fills, again while it stays full,
 * "stopped" when it empties.
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
  it("says typing when the box fills, repeats while it stays full, and says stopped when it empties", () => {
    const said: boolean[] = [];
    let runtime: AssistantRuntime | undefined;
    render(<Harness sayTyping={(on) => said.push(on)} onRuntime={(r) => (runtime = r)} />);
    said.length = 0;

    act(() => runtime!.thread.composer.setText("hel"));
    expect(said).toEqual([true]);

    act(() => runtime!.thread.composer.setText("hello"));
    expect(said).toEqual([true]);

    act(() => vi.advanceTimersByTime(TypingRepeatMs + 1));
    expect(said).toEqual([true, true]);

    act(() => runtime!.thread.composer.setText(""));
    expect(said).toEqual([true, true, false]);
  });
});
