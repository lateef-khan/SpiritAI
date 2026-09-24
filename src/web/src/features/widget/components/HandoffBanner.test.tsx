import { cleanup, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it } from "vitest";

import type { HandoffState } from "../hooks/desk";
import { HandoffBanner } from "./HandoffBanner";

/**
 * The banner, one test per thing it says. The words come from section 5 of the handoff spec.
 */
const waiting: HandoffState = { status: "waiting", assigneeName: null, staffOnline: true };

afterEach(cleanup);

/** The banner's words, with whitespace folded the way a reader sees them. */
function said(): string {
  return screen.getByRole("status").textContent?.replace(/\s+/g, " ") ?? "";
}

describe("HandoffBanner", () => {
  it("says nothing while the bot has the chat", () => {
    render(<HandoffBanner state={{ ...waiting, status: "bot" }} />);

    expect(screen.queryByRole("status")).toBeNull();
  });

  it("says a person is waited for and someone is online, and never the place in line", () => {
    render(<HandoffBanner state={waiting} />);

    expect(said()).toContain("Waiting for a person.");
    expect(said()).toContain("Someone is online.");
    expect(said()).not.toMatch(/in line|#\d/);
  });

  it("says nobody is online, and never how many there are", () => {
    render(<HandoffBanner state={{ ...waiting, staffOnline: false }} />);

    expect(said()).toContain("Nobody is online right now.");
  });

  it("names the person who has the chat", () => {
    render(<HandoffBanner state={{ ...waiting, status: "human", assigneeName: "Dana R." }} />);

    expect(said()).toContain("Dana R. is with you.");
  });
});
