"use client";

import { useAui, useAuiState } from "@assistant-ui/react";
import { Renderer, type ActionEvent } from "@openuidev/react-lang";
import { openuiLibrary } from "@openuidev/react-ui";
import type { FC } from "react";
/**
 * One assistant text part through OpenUI, Renderer-only.
 *
 * The reply is always an openui-lang program, so the part text goes straight to the
 * Renderer: no fence check, no markdown fallback. Blank-until-first-root is the loading
 * state — the parser returns null until `root` streams in, then reveals top-down.
 * Part scope, not message scope: the part text is already the whole reply so far. The
 * `inert` gate stays on message scope — the part is complete the moment its text arrives
 * whole, while the message still streams. requires-action (approval gate) is running for
 * this purpose too: the turn is paused on the caller, not finished.
 * A click becomes the next user turn. `open_url` opens an http(s) link in a new tab and
 * sends nothing: the Renderer never navigates itself, and anything else for a scheme is
 * dropped.
 */
const OpenUIAssistantMessage: FC = () => {
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

    const label = event.humanFriendlyMessage?.trim();
    const body =
      label && label.length > 0 ? label : JSON.stringify({ type: event.type, ...event.params });

    // Wrapped in prose. The wire layer flattens a user message to its text, and bare JSON would
    // read to the agent as something the caller typed.
    aui.thread.append({
      role: "user",
      content: [{ type: "text", text: `the caller clicked: ${body}` }],
    });
  };

  return (
    // The guard above stops the click landing; `inert` stops the caller believing it did, and takes
    // the controls out of the tab order rather than only ignoring the mouse.
    <div
      inert={isRunning}
      data-agentcore-drawing=""
      className={isRunning ? "opacity-60" : undefined}
    >
      <Renderer
        response={text}
        library={openuiLibrary}
        isStreaming={partStatusType === "running"}
        onAction={onAction}
      />
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
