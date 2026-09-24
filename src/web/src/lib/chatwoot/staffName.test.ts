import { describe, expect, it } from "vitest";
import { staffName } from "./staffName.ts";

/*
 * Chatwoot's `available_name` is the display name when one is set and the account name when not
 * (UserAttributeHelpers#available_name, 4.18.0). Spirit applies the same rule in
 * ChatwootStaffName.cs, with the same cases.
 */

describe("staffName", () => {
  it.each([
    ["Matthew Hsu", "Matthew Hsu", "Matthew"],
    ["Matthew Hsu", "Matt", "Matt"],
    ["Matthew Hsu", undefined, "Matthew"],
    ["  Matthew   Hsu ", "  Matthew   Hsu ", "Matthew"],
    ["Dana", "Dana", "Dana"],
    ["", "", null],
  ])("names %j with available name %j as %j", (name, available_name, expected) => {
    expect(staffName({ type: "user", name, available_name })).toBe(expected);
  });

  it("names nobody for the agent bot, a contact, or no one", () => {
    expect(
      staffName({ type: "agent_bot", name: "Spirit AI", available_name: "Spirit AI" }),
    ).toBeNull();
    expect(staffName({ type: "contact", name: "Probe Visitor" })).toBeNull();
    expect(staffName(null)).toBeNull();
    expect(staffName(undefined)).toBeNull();
  });
});
