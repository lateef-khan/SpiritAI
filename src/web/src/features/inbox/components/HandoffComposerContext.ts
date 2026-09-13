import { createContext, useContext } from "react";

import type { Handoff } from "../api/handoffsApi";

/** What the reply box needs to know about the chat it sits under. */
export type HandoffComposerState = {
  handoff: Handoff;
  /**
   * Whether the viewer holds this chat and may reply — `status === "human"` and
   * `assignee.key === meKey`.
   */
  canReply: boolean;
  sendError: string | null;
};

export const HandoffComposerContext = createContext<HandoffComposerState | null>(null);

/**
 * Reads the reply box's state.
 *
 * Thrown rather than defaulted: a `HandoffComposer` rendered outside `HandoffChat` has no chat to
 * reply to, and a silent default would surface as a `TypeError` two hops away instead of here.
 *
 * @returns The chat the box replies to, who is looking at it, and the last send's failure, if any.
 */
export function useHandoffComposer(): HandoffComposerState {
  const value = useContext(HandoffComposerContext);

  if (value === null) {
    throw new Error("useHandoffComposer must be used within a HandoffComposerContext.Provider");
  }

  return value;
}
