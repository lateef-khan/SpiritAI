import { useCallback, useEffect, useMemo, useState } from "react";

import { getOrder, getUnit } from "@/api/sdk.gen";
import type { OrderDocument, UnitDocument } from "@/api/types.gen";
import { HostRefusedError } from "@/apiClient";
import type { Identifier } from "@/hooks/useIdentifiers";

/**
 * What the panel is showing right now.
 *
 * A miss is its own state rather than an empty document. The panel beats the agent to a typo, and
 * "no unit with that number" is the whole point of it doing so — a blank panel would read as a slow
 * one.
 */
export type UnitView =
  | { readonly state: "idle" }
  | { readonly state: "loading"; readonly identifier: Identifier }
  | { readonly state: "unit"; readonly identifier: Identifier; readonly unit: UnitDocument }
  | { readonly state: "order"; readonly identifier: Identifier; readonly order: OrderDocument }
  | { readonly state: "missing"; readonly identifier: Identifier }
  | { readonly state: "failed"; readonly identifier: Identifier };

/**
 * Reads whichever document the selected identifier names.
 *
 * Two routes rather than one, because a work order is not a unit and the two documents share no
 * fields. Chasing a wrong number costs one cheap request: no model, no money.
 *
 * @param identifier What the chip row has selected, or `null` when nobody has pasted a number.
 * @returns What to render, and a way to ask again after a failure.
 */
export function useUnitDocument(identifier: Identifier | null): {
  readonly view: UnitView;
  readonly retry: () => void;
} {
  const [attempt, setAttempt] = useState(0);
  const [answer, setAnswer] = useState<{ readonly to: string; readonly view: UnitView } | null>(
    null,
  );

  const kind = identifier?.kind ?? null;
  const value = identifier?.value ?? null;
  const wanted = useMemo(() => (kind && value ? { kind, value } : null), [kind, value]);

  const asked = wanted ? `${wanted.kind}:${wanted.value}:${attempt}` : null;

  const retry = useCallback(() => setAttempt((n) => n + 1), []);

  useEffect(() => {
    if (!wanted || !asked) return;

    // Guards the answer rather than the request. Two lookups can be in flight after a quick
    // correction, and the one that started last is the one the person is waiting for.
    let current = true;

    void (async () => {
      let view: UnitView;

      try {
        view =
          wanted.kind === "serial"
            ? {
              state: "unit",
              identifier: wanted,
              unit: (await getUnit({ throwOnError: true, path: { serial: wanted.value } })).data,
            }
            : {
              state: "order",
              identifier: wanted,
              order: (await getOrder({ throwOnError: true, path: { orderNumber: wanted.value } }))
                .data,
            };
      } catch (refusal) {
        // A 404 is an answer: nothing carries that number. A 400 is the same answer from the other
        // direction. Anything else is the host having a problem, which is worth offering to ask
        // about again.
        const missing =
          refusal instanceof HostRefusedError && (refusal.status === 404 || refusal.status === 400);

        view = { state: missing ? "missing" : "failed", identifier: wanted };
      }

      if (current) setAnswer({ to: asked, view });
    })();

    return () => {
      current = false;
    };
  }, [wanted, asked]);

  // Derived, not stored. Writing "loading" from inside the effect would paint the old unit once
  // before replacing it, and cost a render to do it.
  const view: UnitView = !wanted
    ? { state: "idle" }
    : answer?.to === asked
      ? answer.view
      : { state: "loading", identifier: wanted };

  return { view, retry };
}
