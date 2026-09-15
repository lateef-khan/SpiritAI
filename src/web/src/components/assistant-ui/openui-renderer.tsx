"use client";

import { Renderer, type ActionEvent } from "@openuidev/react-lang";
import type { FC } from "react";
import { spiritChatLibrary } from "./spirit-chat-library";

type OpenUIRendererProps = {
  response: string;
  isStreaming: boolean;
  onAction: (event: ActionEvent) => void;
};

/**
 * The heavy OpenUI draw call, alone in this module so the thread's initial
 * chunk never contains `@openuidev/react-ui`. Loaded through `React.lazy`
 * from `openui-message`; the wrapper there stays light (`@assistant-ui/react`
 * hooks and the markdown fallback, both already in the initial chunk) and
 * owns the click-to-turn logic.
 */
const OpenUIRenderer: FC<OpenUIRendererProps> = ({ response, isStreaming, onAction }) => (
  <Renderer
    response={response}
    library={spiritChatLibrary}
    isStreaming={isStreaming}
    onAction={onAction}
  />
);

export default OpenUIRenderer;
