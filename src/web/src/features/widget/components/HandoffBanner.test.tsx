import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";

import type { HandoffState } from "../api/widgetApi";
import { HandoffBanner } from "./HandoffBanner";

/**
 * The banner, one test per thing it says. The words come from section 5 of the handoff spec.
 */
const waiting: HandoffState = {
  status: "waiting",
  assigneeName: null,
  staffOnline: true,
  email: null,
};

const none = async () => {};

afterEach(cleanup);

/** The banner's words, with whitespace folded the way a reader sees them. */
function said(): string {
  return screen.getByRole("status").textContent?.replace(/\s+/g, " ") ?? "";
}

describe("HandoffBanner", () => {
  it("says nothing while the bot has the chat", () => {
    render(<HandoffBanner state={{ ...waiting, status: "bot" }} onLeaveEmail={none} />);

    expect(screen.queryByRole("status")).toBeNull();
  });

  it("says a person is waited for and someone is online, and never the place in line", () => {
    render(<HandoffBanner state={waiting} onLeaveEmail={none} />);

    expect(said()).toContain("Waiting for a person.");
    expect(said()).toContain("Someone is online.");
    expect(said()).not.toMatch(/in line|#\d/);
  });

  it("says nobody is online, and never how many there are", () => {
    render(<HandoffBanner state={{ ...waiting, staffOnline: false }} onLeaveEmail={none} />);

    expect(said()).toContain("Nobody is online right now.");
  });

  it("asks for an email while waiting, whether or not staff are online, and sends it", async () => {
    const onLeaveEmail = vi.fn(async () => {});
    render(
      <HandoffBanner state={{ ...waiting, staffOnline: false }} onLeaveEmail={onLeaveEmail} />,
    );

    fireEvent.change(screen.getByLabelText("Your email"), { target: { value: "pat@example.com" } });
    fireEvent.click(screen.getByRole("button", { name: "Send" }));

    await waitFor(() => expect(onLeaveEmail).toHaveBeenCalledWith("pat@example.com"));
  });

  it("says where the reply will go once an email is left", () => {
    render(<HandoffBanner state={{ ...waiting, email: "pat@example.com" }} onLeaveEmail={none} />);

    expect(said()).toContain("We will email pat@example.com");
    expect(screen.queryByRole("textbox")).toBeNull();
  });

  it("stops asking once a person has the chat", () => {
    render(
      <HandoffBanner
        state={{ ...waiting, status: "human", assigneeName: "Dana R." }}
        onLeaveEmail={none}
      />,
    );

    expect(screen.queryByRole("textbox")).toBeNull();
  });

  it("names the person who has the chat", () => {
    render(
      <HandoffBanner
        state={{ ...waiting, status: "human", assigneeName: "Dana R." }}
        onLeaveEmail={none}
      />,
    );

    expect(said()).toContain("Dana R. is with you.");
  });
});
