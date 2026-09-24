import { useCallback, useState } from "react";

import { HostRefusedError } from "@/lib/apiClient";
import type { HandoffState, WidgetApi } from "../api/widgetApi";

/**
 * Where the visitor's chat stands, as the widget holds it.
 */

/** A chat that has never asked for a person: no row on the host, nothing to show. */
export const WithBot: HandoffState = {
  status: "bot",
  assigneeName: null,
  staffOnline: false,
  phone: null,
  code: null,
};

/** The desk the runtime and the banner both read. */
export type HandoffDesk = {
  /** Where the chat stands. `WithBot` until the host says otherwise. */
  readonly state: HandoffState;
  /** Reads the state again for one call, and answers what it read. */
  refresh(callId: string): Promise<HandoffState>;
  /** Leaves a phone number on the waiting row of one call, then reads the state again. */
  leavePhone(callId: string, phone: string): Promise<void>;
  /** Moves the state the way a push said it moved, without a read. The next open reads the truth. */
  apply(change: Partial<HandoffState>): void;
};

/**
 * Holds the chat's handoff state, read from the host. The desk holds no call of its own: the
 * runtime refreshes it for the call it opens.
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

  // The host answers the number in its own display form, so the state is read back, not guessed.
  const leavePhone = useCallback(
    async (callId: string, phone: string) => {
      await api.leavePhone(callId, phone);
      await refresh(callId);
    },
    [api, refresh],
  );

  const apply = useCallback((change: Partial<HandoffState>) => {
    setState((held) => ({ ...held, ...change }));
  }, []);

  return { state, refresh, leavePhone, apply };
}
