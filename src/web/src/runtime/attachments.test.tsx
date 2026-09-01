import { Thread } from "@/components/assistant-ui/thread";
import { AssistantRuntimeProvider, type AssistantRuntime } from "@assistant-ui/react";
import { act, cleanup, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, test } from "vitest";
import { useAgentCoreRuntime } from "./AgentCoreRuntime.ts";

/**
 * That the composer accepts a file at all.
 *
 * assistant-ui reads the attachment capability off the presence of an adapter, and adding a file
 * without one throws an error the add-attachment button catches and drops. A missing adapter is
 * therefore silent: the picker opens, a file is chosen, and no chip ever appears. Nothing about the
 * markup changes, so only mounting the real composer catches it.
 */

// Testing-library only registers its own cleanup when vitest runs with `globals: true`. It does
// not here, so each test would otherwise leave its DOM in the body.
afterEach(cleanup);

function mount() {
  let runtime: AssistantRuntime | undefined;

  function Harness() {
    runtime = useAgentCoreRuntime("/v1/chat/completions", () => {
      throw new Error("no turn should run in this test");
    });

    return (
      <AssistantRuntimeProvider runtime={runtime}>
        <Thread />
      </AssistantRuntimeProvider>
    );
  }

  render(<Harness />);
  return () => runtime!;
}

async function attach(runtime: AssistantRuntime, file: File) {
  await act(async () => {
    await runtime.thread.composer.addAttachment(file);
  });
}

describe("the composer's attachments", () => {
  test("shows a chip for an image", async () => {
    const runtime = mount();
    await attach(runtime(), new File([new Uint8Array([1])], "belt.png", { type: "image/png" }));

    expect(await screen.findByLabelText("Image attachment")).toBeTruthy();
  });

  test("shows a chip for a text file", async () => {
    const runtime = mount();
    await attach(runtime(), new File(["boom"], "log.txt", { type: "text/plain" }));

    expect(await screen.findByLabelText("Document attachment")).toBeTruthy();
  });

  test("offers the caller both kinds in the file picker", () => {
    // The picker's filter comes from the composite adapter's own accept list. An image-only list
    // here would mean the text adapter never made it into the composite.
    const runtime = mount();
    const accept = runtime().thread.composer.getState().attachmentAccept;

    expect(accept).toContain("image/*");
    expect(accept).toContain("text/plain");
  });

  test("refuses a kind no adapter handles", async () => {
    const runtime = mount();

    await expect(
      attach(runtime(), new File(["%PDF"], "manual.pdf", { type: "application/pdf" })),
    ).rejects.toThrow();
  });
});
