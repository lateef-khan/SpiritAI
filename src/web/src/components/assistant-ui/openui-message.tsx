"use client";

import { useAui, useAuiState } from "@assistant-ui/react";
import type { ActionEvent } from "@openuidev/react-lang";
import { lazy, Suspense, type FC } from "react";
import { readOpenUiFormState, writeOpenUiFormState } from "@/components/assistant-ui/openUiFormStore";
import { TypingIndicator } from "./elements/typing-indicator";
import { MarkdownText } from "./markdown-text";

const OpenUIRenderer = lazy(() => import("./openui-renderer"));

/**
 * The words a form's submit sends up: the button's label, then one `name: value` line per field.
 */

/** One field's worth as text, or `null` when it is empty or has no flat spelling. */
function scalarOf(state: unknown): string | null {
  const value =
    typeof state === "object" && state !== null && "value" in state
      ? (state as { value: unknown }).value
      : state;

  if (!["string", "number", "boolean"].includes(typeof value)) return null;

  const text = String(value).trim();
  
  return text.length > 0 ? text : null;
}

export function formMessage(
  event: Pick<ActionEvent, "humanFriendlyMessage" | "formState" | "formName">,
): string {
  const label = event.humanFriendlyMessage.trim();
  const fields = event.formName ? event.formState?.[event.formName] : undefined;

  if (typeof fields !== "object" || fields === null) return label;

  const lines = Object.entries(fields as Record<string, unknown>)
    .map(([name, state]) => [name, scalarOf(state)] as const)
    .filter((entry): entry is readonly [string, string] => entry[1] !== null)
    .map(([name, value]) => `${name}: ${value}`);

  return lines.length === 0 ? label : [label, ...lines].join("\n");
}

/**
 * One assistant text part through OpenUI, Renderer-only.
 */
const OpenUIAssistantMessage: FC = () => {
  const messageId = useAuiState((s) => s.message.id);
  
  const text = useAuiState((s) => (s.part.type === "text" ? s.part.text : null));

  const partStatusType = useAuiState((s) =>
    s.part.type === "text" ? s.part.status.type : undefined,
  );

  const messageStatusType = useAuiState((s) =>
    s.message.role === "assistant" ? s.message.status?.type : undefined,
  );

  const isRunning = useAuiState(
    (state) => state.thread.isRunning || messageStatusType === "requires-action",
  );

  const aui = useAui();

  if (text === null) return null;

  const onAction = (event: ActionEvent) => {
    // Appending mid-stream aborts the running turn rather than queueing behind it.
    if (aui.thread.getState().isRunning) return;

    // The Renderer never opens anything itself: `triggerAction` only fires `onAction`,
    // so the host opens safe links and says so on the same turn.
    if (event.type === "open_url") {
      openUrl(event.params?.url);
      return;
    }

    // `continue_conversation` is what FollowUpItem, ListItem, and bare Buttons fire:
    // the click text arrives as `humanFriendlyMessage` and becomes the next user turn.
    // Anything else for a scheme is the model misbehaving: dropped.
    if (event.type !== "continue_conversation") return;

    const text = formMessage(event);

    if (!text) return;

    aui.thread.append({
      role: "user",
      content: [{ type: "text", text }],
    });
  };

  const isProgram = /^\s*root\s*=/m.test(text);

  if (!isProgram) {
    return <MarkdownText />;
  }

  return (
    // The guard above stops the click landing; `inert` stops the caller believing it did, and takes
    // the controls out of the tab order rather than only ignoring the mouse.
    <div
      inert={isRunning}
      data-agentcore-drawing=""
      className={isRunning ? "opacity-60" : undefined}
    >
      <Suspense fallback={<TypingIndicator variant="bare" className="py-2" />}>
        <OpenUIRenderer
          response={text}
          isStreaming={partStatusType === "running"}
          onAction={onAction}
          initialState={readOpenUiFormState(messageId)}
          onStateUpdate={(state) => writeOpenUiFormState(messageId, state)}
        />
      </Suspense>
    </div>
  );
};

/** Opens an http(s) link in a new tab. Anything else is the model misbehaving: dropped. */
function openUrl(url: unknown): void {
  if (typeof url !== "string") return;
  const trimmed = url.trim();
  if (!/^https?:\/\//i.test(trimmed)) return;
  window.open(trimmed, "_blank", "noopener,noreferrer");
}

/** Draws one assistant message as OpenUI. Draws nothing itself. */
export const OpenUIAssistantMessagePart: FC = OpenUIAssistantMessage;
