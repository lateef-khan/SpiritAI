import { describe, expect, it } from "vitest";

import { isHubMessage } from "./hubMessages";

describe("isHubMessage", () => {
  it.each([
    { type: "hub:needs-sign-in", app: "desk" },
    { type: "hub:needs-sign-in", app: "crm" },
    { type: "hub:signed-out", app: "desk" },
    { type: "hub:signed-out", app: "crm" },
  ])("accepts %j", (message) => {
    expect(isHubMessage(message)).toBe(true);
  });

  it.each([
    null,
    undefined,
    "hub:sign-out",
    {},
    { type: "hub:needs-sign-in" },
    { type: "hub:needs-sign-in", app: "widget" },
    { type: "hub:needs-sign-in", app: 1 },
    { type: "not-a-hub-message" },
    { source: "agentcore-widget", type: "visitor", key: "abc" },
  ])("refuses %j", (data) => {
    expect(isHubMessage(data)).toBe(false);
  });
});
