import { Thread, type ThreadComponents } from "@/components/assistant-ui/thread";
import { useAui } from "@assistant-ui/store";
import { AssistantRuntimeProvider } from "@assistant-ui/react";
import { act, cleanup, render, screen, waitFor } from "@testing-library/react";
import { afterEach, describe, expect, test } from "vitest";
import { useAgentCoreRuntime } from "./AgentCoreRuntime.ts";
import { type FetchLike } from "./transport.ts";

/**
 * The approval round-trip, through the real kit: the host's ask pauses the run with the kit's
 * own Allow/Deny bar, and answering resumes it with the decision on the wire.
 *
 * The kit owns the pause — clicking Allow calls `respondToApproval`, which writes the decision
 * onto the part and resumes the run. The adapter only maps: ask onto a gated part, decision
 * onto an approval answer.
 */

afterEach(cleanup);

function created(conversation: string): string {
  return `event: response.created\ndata: ${JSON.stringify({
    type: "response.created",
    response: { id: "resp_1", conversation: { id: conversation } },
  })}\n\n`;
}

function delta(text: string): string {
  return `event: response.output_text.delta\ndata: ${JSON.stringify({
    type: "response.output_text.delta",
    delta: text,
  })}\n\n`;
}

function completed(conversation = "conv_1"): string {
  return `event: response.completed\ndata: ${JSON.stringify({
    type: "response.completed",
    response: { id: "resp_1", conversation: { id: conversation }, metadata: {} },
  })}\n\n`;
}

function approvalAsk(requestId = "req_1"): string {
  return `data: ${JSON.stringify({
    agentcore_approval: {
      request_id: requestId,
      tool: "send_email",
      arguments: { to: "a@b.com" },
    },
  })}\n\n`;
}

function toolCall(): string {
  return `data: ${JSON.stringify({
    agentcore_tool: {
      call_id: "c1",
      name: "send_email",
      phase: "call",
      arguments: { to: "a@b.com" },
    },
  })}\n\n`;
}

function streaming(pieces: string[]): Response {
  const body = new ReadableStream<Uint8Array>({
    start(controller) {
      const encoder = new TextEncoder();
      for (const piece of pieces) {
        controller.enqueue(encoder.encode(piece));
      }
      controller.close();
    },
  });
  return new Response(body, { status: 200 });
}

/** Drives the adapter with a scripted host and records every request body. */
function scripted(responses: Response[]) {
  const sent: unknown[] = [];
  let index = 0;
  const fetch: FetchLike = (_url, init) => {
    sent.push(JSON.parse(String(init.body)));
    const response = responses[index++];
    if (!response) throw new Error("the test scripted fewer answers than turns ran.");
    return Promise.resolve(response);
  };
  return { fetch, sent };
}

type Aui = ReturnType<typeof useAui>;

const EXPANDED_COMPONENTS: ThreadComponents = {
  ToolGroup: ({ children }) => <>{children}</>,
};

function mount(fetch: FetchLike) {
  const captured: { aui?: Aui } = {};
  function Harness() {
    const runtime = useAgentCoreRuntime("/v1/responses", (url, init) => fetch(url, init));
    return (
      <AssistantRuntimeProvider runtime={runtime}>
        <Capture captured={captured} />
        <Thread components={EXPANDED_COMPONENTS} />
      </AssistantRuntimeProvider>
    );
  }
  render(<Harness />);
  if (!captured.aui) throw new Error("the harness never captured its aui handle.");
  return captured.aui;
}

function Capture({ captured }: { captured: { aui?: Aui } }) {
  captured.aui = useAui();
  return null;
}

async function send(aui: Aui, text: string) {
  await act(async () => {
    aui.thread.composer().setText(text);
    aui.thread.composer().send();
    await new Promise<void>((resolve) => setTimeout(resolve, 0));
  });
}

describe("the approval round-trip", () => {
  test("the host's ask draws the kit's Allow/Deny bar", async () => {
    const { fetch } = scripted([
      streaming([created("conv_1"), toolCall(), approvalAsk(), completed()]),
    ]);
    const aui = mount(fetch);

    await send(aui, "send it");

    expect(await screen.findByText("Allow")).toBeTruthy();
    expect(await screen.findByText("Deny")).toBeTruthy();
  });

  test("Allow resumes with an approval answer and streams the reply", async () => {
    const { fetch, sent } = scripted([
      streaming([created("conv_1"), toolCall(), approvalAsk(), completed()]),
      streaming([created("conv_1"), delta("done."), completed()]),
    ]);
    const aui = mount(fetch);

    await send(aui, "send it");
    await screen.findByText("Allow");

    await act(async () => {
      const allow = screen.getByText("Allow");
      (allow as HTMLButtonElement).click();
      await new Promise<void>((resolve) => setTimeout(resolve, 0));
    });

    await waitFor(() => expect(screen.getByText("done.")).toBeTruthy());

    expect(sent).toHaveLength(2);
    expect(sent[1]).toMatchObject({
      conversation: "conv_1",
      input: [],
      agentcore: { approval: { request_id: "req_1", approved: true } },
    });
  });

  test("Deny answers false and the run ends", async () => {
    const { fetch, sent } = scripted([
      streaming([created("conv_1"), toolCall(), approvalAsk(), completed()]),
      streaming([created("conv_1"), delta("not sent."), completed()]),
    ]);
    const aui = mount(fetch);

    await send(aui, "send it");
    await screen.findByText("Deny");

    await act(async () => {
      const deny = screen.getByText("Deny");
      (deny as HTMLButtonElement).click();
      await new Promise<void>((resolve) => setTimeout(resolve, 0));
    });

    await waitFor(() => expect(screen.getByText("not sent.")).toBeTruthy());
    expect(sent[1]).toMatchObject({
      agentcore: { approval: { request_id: "req_1", approved: false } },
    });
  });
});
