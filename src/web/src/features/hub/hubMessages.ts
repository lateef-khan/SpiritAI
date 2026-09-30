/**
 * Messages carried by `postMessage` between the Hub page and the frames it holds — chat, Desk,
 * CRM — and the sign-in page when it runs framed inside the Hub instead of at the top level.
 */
export type HubAppId = "desk" | "crm";

export type HubMessage =
  { type: "hub:needs-sign-in"; app: HubAppId } | { type: "hub:signed-out"; app: HubAppId };

const APP_IDS: readonly HubAppId[] = ["desk", "crm"];

/** Whether `value` names one of the Hub's apps. */
export function isHubAppId(value: unknown): value is HubAppId {
  return typeof value === "string" && (APP_IDS as readonly string[]).includes(value);
}

/** Narrows a `message` event's `data` to a `HubMessage`, since it may be anything a frame sends. */
export function isHubMessage(data: unknown): data is HubMessage {
  if (typeof data !== "object" || data === null) return false;
  const { type } = data as { type?: unknown };

  if (type === "hub:needs-sign-in" || type === "hub:signed-out") {
    return isHubAppId((data as { app?: unknown }).app);
  }
  return false;
}
