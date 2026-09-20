import { Thread } from "@/components/assistant-ui/thread";
// Warms the `React.lazy` chunk behind `openui-message` during collection, so
// the tests measure rendering rather than first-compile. Static on purpose:
// a dynamic `import()` here trips the 10s `beforeAll` hook timeout while the
// `@openuidev/react-ui` graph compiles.
import "@/components/assistant-ui/openui-renderer";

import { useAui, type AssistantClient } from "@assistant-ui/store";
import { useAgentCoreRuntime } from "../threads/AgentCoreRuntime.ts";
import { type FetchLike } from "../threads/transport.ts";
import { AssistantRuntimeProvider } from "@assistant-ui/react";
import { act, cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, describe, expect, test } from "vitest";
import { queryWrapper } from "@/test/query.tsx";
/**
 * The OpenUI text renderer, Renderer-only: every reply is one openui-lang program and the
 * part text goes straight to the Renderer. Plain prose lives inside a TextContent; the
 * prompt never emits bare markdown, so the markdown fallback is gone.
 */

afterEach(cleanup);

function created(conversation: string): string {
  return `event: response.created\ndata: {"type":"response.created","response":{"conversation":{"id":"${conversation}"}}}\n\n`;
}

function delta(text: string): string {
  return `event: response.output_text.delta\ndata: {"type":"response.output_text.delta","delta":${JSON.stringify(text)}}\n\n`;
}

function completed(conversation = "conv_1"): string {
  return `event: response.completed\ndata: {"type":"response.completed","response":{"metadata":{"message_id":"msg_1","is_terminal":"false"},"conversation":{"id":"${conversation}"}}}\n\n`;
}

function streaming(pieces: string[]): Response {
  return new Response(
    new ReadableStream({
      start(controller) {
        for (const piece of pieces) controller.enqueue(new TextEncoder().encode(piece));
        controller.close();
      },
    }),
    { headers: { "Content-Type": "text/event-stream" } },
  );
}

/** Drives the adapter with a scripted host and answers the turns it was sent. */
function scripted(responses: Response[]) {
  const sent: string[] = [];
  const queue = [...responses];
  const fetch: FetchLike = async (_url, init) => {
    const body = JSON.parse(String(init?.body ?? "{}")) as { input?: string };
    sent.push(body.input ?? "");
    const next = queue.shift();
    if (!next) throw new Error("the thread sent more turns than the script holds.");
    return next;
  };
  return { fetch, sent };
}

type Aui = AssistantClient;

function mount(fetch: FetchLike) {
  const captured: { aui?: Aui } = {};
  const { wrapper } = queryWrapper();
  function Harness() {
    const runtime = useAgentCoreRuntime("/v1/responses", (url, init) => fetch(url, init));
    return (
      <AssistantRuntimeProvider runtime={runtime}>
        <Capture captured={captured} />
        <Thread />
      </AssistantRuntimeProvider>
    );
  }
  render(<Harness />, { wrapper });
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

describe("the OpenUI text renderer", () => {
  test("a prose program draws through the Renderer, not the markdown fallback", async () => {
    const lang = ['t1 = TextContent("Tighten the belt to spec.")', "root = Card([t1])"].join("\n");
    const { fetch } = scripted([streaming([created("conv_1"), delta(lang), completed()])]);
    const aui = mount(fetch);
    await send(aui, "how tight?");

    // The words draw inside the OpenUI wrapper; there is no separate markdown branch.
    expect(await screen.findByText(/Tighten the belt/)).toBeTruthy();
    expect(document.querySelector("[data-agentcore-drawing]")).not.toBeNull();
  });

  test("an openui-lang program renders its components", async () => {
    const lang = ['t1 = TextContent("Q3 revenue")', "root = Card([t1])"].join("\n");
    const { fetch } = scripted([streaming([created("conv_1"), delta(lang), completed()])]);
    const aui = mount(fetch);
    await send(aui, "show me revenue");

    expect(await screen.findByText("Q3 revenue")).toBeTruthy();
  });

  test("a click on a button sends its label back as the next user turn", async () => {
    const lang = ['btn = Button("Approve")', "grp = Buttons([btn])", "root = Card([grp])"].join(
      "\n",
    );
    const reply = ['t1 = TextContent("done.")', "root = Card([t1])"].join("\n");
    const { fetch, sent } = scripted([
      streaming([created("conv_1"), delta(lang), completed()]),
      streaming([created("conv_1"), delta(reply), completed()]),
    ]);
    const aui = mount(fetch);
    await send(aui, "approve this");

    const button = await screen.findByRole("button", { name: "Approve" });
    await act(async () => {
      button.click();
    });

    // Sent verbatim: a Button without an explicit Action fires continue_conversation with
    // the label as the message, and the label is already caller-facing prose.
    await waitFor(() => {
      expect(sent.some((text) => text === "Approve")).toBe(true);
    });
  });

  test("a form's submit sends its label and its fields as the next user turn", async () => {
    const lang = [
      "root = Card([form])",
      'form = Form("contact", btns, [emailField])',
      'emailField = FormControl("Email", Input("email", "you@example.com", "email", { required: true, email: true }))',
      'btns = Buttons([Button("Send", Action([@ToAssistant("Here is my email")]), "primary")])',
    ].join("\n");
    const reply = ['t1 = TextContent("done.")', "root = Card([t1])"].join("\n");
    const { fetch, sent } = scripted([
      streaming([created("conv_1"), delta(lang), completed()]),
      streaming([created("conv_1"), delta(reply), completed()]),
    ]);
    const aui = mount(fetch);
    await send(aui, "I want a person");

    const box = await screen.findByPlaceholderText("you@example.com");
    fireEvent.change(box, { target: { value: "pat@example.com" } });
    const button = await screen.findByRole("button", { name: "Send" });
    await act(async () => {
      button.click();
    });

    await waitFor(() => {
      expect(sent.some((text) => text === "Here is my email\nemail: pat@example.com")).toBe(true);
    });
  });

  test("a follow-up click sends its text as the next user turn", async () => {
    const lang = [
      't1 = TextContent("CT800 overview")',
      "followUps = FollowUpBlock([fu1, fu2])",
      'fu1 = FollowUpItem("What years was it made?")',
      'fu2 = FollowUpItem("Show me parts")',
      "root = Card([t1, followUps])",
    ].join("\n");
    const reply = ['t1 = TextContent("done.")', "root = Card([t1])"].join("\n");
    const { fetch, sent } = scripted([
      streaming([created("conv_1"), delta(lang), completed()]),
      streaming([created("conv_1"), delta(reply), completed()]),
    ]);
    const aui = mount(fetch);
    await send(aui, "tell me about the ct800");

    const followUp = await screen.findByRole("button", { name: "What years was it made?" });
    await act(async () => {
      followUp.click();
    });

    await waitFor(() => {
      expect(sent.some((text) => text === "What years was it made?")).toBe(true);
    });
  });

  test("the controls are inert while a turn is still running", async () => {
    const lang = ['t1 = TextContent("Q3 revenue")', "root = Card([t1])"].join("\n");
    // No closing event: the stream never ends, so the thread stays running. The hold
    // keeps the fetch open the way a live SSE stream stays open mid-turn.
    let release!: () => void;
    const held = new Promise<void>((resolve) => {
      release = resolve;
    });
    const hanging: FetchLike = ((_url: string, _init: RequestInit) => {
      const { fetch } = scripted([streaming([created("conv_1"), delta(lang)])]);
      return (async () => {
        const response = await fetch(_url, _init);
        const reader = response.body!.getReader();
        return new Response(
          new ReadableStream({
            async start(controller) {
              for (;;) {
                const { done, value } = await reader.read();
                if (done) break;
                controller.enqueue(value);
              }
              await held;
            },
          }),
          { headers: { "Content-Type": "text/event-stream" } },
        );
      })();
    }) as FetchLike;
    const aui = mount(hanging);
    await send(aui, "show me revenue");

    try {
      await screen.findByText("Q3 revenue");

      const drawing = document.querySelector("[data-agentcore-drawing]");
      expect(aui.thread.getState().isRunning).toBe(true);
      expect(drawing?.hasAttribute("inert")).toBe(true);
    } finally {
      release();
    }
  });

  test("an open_url action opens the link and sends no turn", async () => {
    const lang = [
      'btn = Button("Docs", Action([@OpenUrl("https://example.com/help")]))',
      "grp = Buttons([btn])",
      "root = Card([grp])",
    ].join("\n");
    const { fetch, sent } = scripted([streaming([created("conv_1"), delta(lang), completed()])]);
    const opened: string[] = [];
    const realOpen = window.open;
    window.open = ((url: string) => {
      opened.push(url);
      return null;
    }) as typeof window.open;
    const aui = mount(fetch);
    try {
      await send(aui, "show me docs");

      const button = await screen.findByRole("button", { name: "Docs" });
      await act(async () => {
        button.click();
      });

      expect(opened).toEqual(["https://example.com/help"]);
      expect(sent).toHaveLength(1);
    } finally {
      window.open = realOpen;
    }
  });
  test("an open_url action with an unsafe scheme opens nothing", async () => {
    const lang = [
      'btn = Button("Run", Action([@OpenUrl("javascript:alert(1)")]))',
      "grp = Buttons([btn])",
      "root = Card([grp])",
    ].join("\n");
    const { fetch, sent } = scripted([streaming([created("conv_1"), delta(lang), completed()])]);
    const opened: string[] = [];
    const realOpen = window.open;
    window.open = ((url: string) => {
      opened.push(url);
      return null;
    }) as typeof window.open;
    const aui = mount(fetch);
    try {
      await send(aui, "run it");

      const button = await screen.findByRole("button", { name: "Run" });
      await act(async () => {
        button.click();
      });

      expect(opened).toEqual([]);
      expect(sent).toHaveLength(1);
    } finally {
      window.open = realOpen;
    }
  });

  test("an unknown component renders nothing rather than failing the message", async () => {
    const lang = ['t1 = TextContent("still here")', "wob = Wombat([t1])", "root = Card([t1])"].join(
      "\n",
    );
    const { fetch } = scripted([streaming([created("conv_1"), delta(lang), completed()])]);
    const aui = mount(fetch);
    await send(aui, "show me");

    // The valid branch still draws; the unknown name is dropped, not thrown.
    expect(await screen.findByText("still here")).toBeTruthy();
  });
});
