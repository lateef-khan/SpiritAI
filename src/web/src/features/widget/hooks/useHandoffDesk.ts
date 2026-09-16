import { useCallback, useEffect, useState } from "react";

import { HostRefusedError } from "@/lib/apiClient";
import { readVisitorMemory } from "../api/visitorIdentity";
import type { HandoffState, WidgetApi } from "../api/widgetApi";

/**
 * Where the visitor's chat stands, as the widget holds it.
 */

/** A chat that has never asked for a person: no row on the host, nothing to show. */
export const WithBot: HandoffState = {
  status: "bot",
  position: null,
  assigneeName: null,
  staffOnline: 0,
  email: null,
};

/** The desk the runtime and the banner both read. */
export type HandoffDesk = {
  /** Where the chat stands. `WithBot` until the host says otherwise. */
  readonly state: HandoffState;
  /** Reads the state again for one call, and answers what it read. */
  refresh(callId: string): Promise<HandoffState>;
  /** Leaves an email on the waiting row of the remembered call. */
  leaveEmail(email: string): Promise<void>;
  /** Moves the state the way a push said it moved, without a read. The next open reads the truth. */
  apply(change: Partial<HandoffState>): void;
};

/**
 * Holds the chat's handoff state, read from the host.
 *
 * @param api The widget's routes.
 * @returns The state, and the two ways it moves.
 */
export function useHandoffDesk(api: WidgetApi): HandoffDesk {
  const [state, setState] = useState<HandoffState>(WithBot);

  const refresh = useCallback(
    async (callId: string) => {
      try {
        const read = await api.handoffState(callId);
        setState(read);
        return read;
      } catch (error) {
        // A call the host forgot has no row either; the runtime forgets the call on its own.
        if (error instanceof HostRefusedError && error.status === 404) {
          setState(WithBot);
          return WithBot;
        }
        throw error;
      }
    },
    [api],
  );

  useEffect(() => {
    const { callId } = readVisitorMemory();
    if (callId === null) return;

    let cancelled = false;

    api
      .handoffState(callId)
      .then((read) => {
        if (!cancelled) setState(read);
      })
      .catch(() => {
        // Nothing to show is the right answer to any refusal here; the next send finds out.
      });

    return () => {
      cancelled = true;
    };
  }, [api]);

  const leaveEmail = useCallback(
    async (email: string) => {
      const { callId } = readVisitorMemory();
      if (callId === null) return;

      await api.leaveEmail(callId, email);
      setState((held) => ({ ...held, email }));
    },
    [api],
  );

  const apply = useCallback((change: Partial<HandoffState>) => {
    setState((held) => ({ ...held, ...change }));
  }, []);

  return { state, refresh, leaveEmail, apply };
}
