import { expect, test } from "vitest";

import { agentFrom } from "./agentFrom";
import { PERMISSIONS } from "./settingsFixtures";

test.each([
  [[], null],
  [["chat.agent.guest"], "Guest agent"],
  [["chat.agent.dealer", "chat.agent.staff"], "Staff agent"],
  [["lookup.units", "chat.agent.manager"], "Manager agent"],
  [["chat.agent.guest", "chat.agent.admin"], "Admin agent"],
  [["lookup.orders"], null],
] as const)("%j opens %s", (held, label) => {
  expect(agentFrom([...held], PERMISSIONS)?.label ?? null).toBe(label);
});
