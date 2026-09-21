/**
 * Where a typed OpenUI form survives its row scrolling out of view.
 *
 * The virtualizer unmounts a row once it leaves the overscan window, and `Renderer` keeps its
 * form state in its own component state — gone the moment React unmounts it. Reading and writing
 * this map around `Renderer`'s `initialState`/`onStateUpdate` is what makes a typed email still
 * be there when the reader scrolls back down.
 *
 * Keyed by message id, which is unique across every thread the caller has open, so a thread
 * switch needs no eviction of its own. The map is never pruned: one form's state is a few
 * fields, and a caller would have to fill out forms in hundreds of old threads before it weighed
 * anything.
 */
const formState = new Map<string, Record<string, unknown>>();

/** The form state last written for this message, or `undefined` if it never had one. */
export function readOpenUiFormState(messageId: string): Record<string, unknown> | undefined {
  return formState.get(messageId);
}

/** Records this message's current form state, replacing whatever was there. */
export function writeOpenUiFormState(messageId: string, state: Record<string, unknown>): void {
  formState.set(messageId, state);
}
