import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";

import { HostRefusedError } from "@/lib/apiClient";
import type { HandoffState } from "../api/widgetApi";
import { HandoffBanner } from "./HandoffBanner";

/**
 * The banner, one test per thing it says. The words come from section 5 of the handoff spec and
 * section 3 of the phone callback spec.
 */
const waiting: HandoffState = {
  status: "waiting",
  assigneeName: null,
  staffOnline: true,
  phone: null,
  code: null,
};

const none = async () => {};

afterEach(cleanup);

/** The banner's words, with whitespace folded the way a reader sees them. */
function said(): string {
  return screen.getByRole("status").textContent?.replace(/\s+/g, " ") ?? "";
}

describe("HandoffBanner", () => {
  it("says nothing while the bot has the chat", () => {
    render(<HandoffBanner state={{ ...waiting, status: "bot" }} onLeavePhone={none} />);

    expect(screen.queryByRole("status")).toBeNull();
  });

  it("says a person is waited for and someone is online, and never the place in line", () => {
    render(<HandoffBanner state={waiting} onLeavePhone={none} />);

    expect(said()).toContain("Waiting for a person.");
    expect(said()).toContain("Someone is online.");
    expect(said()).not.toMatch(/in line|#\d/);
  });

  it("says nobody is online, and never how many there are", () => {
    render(<HandoffBanner state={{ ...waiting, staffOnline: false }} onLeavePhone={none} />);

    expect(said()).toContain("Nobody is online right now.");
  });

  it("asks for a phone number while waiting, whether or not staff are online, and sends it", async () => {
    const onLeavePhone = vi.fn(async () => {});
    render(
      <HandoffBanner state={{ ...waiting, staffOnline: false }} onLeavePhone={onLeavePhone} />,
    );

    const box = screen.getByLabelText("Your phone number");
    expect(box.getAttribute("type")).toBe("tel");
    expect(box.getAttribute("autocomplete")).toBe("tel");

    fireEvent.change(box, { target: { value: "(201) 555-0123" } });
    fireEvent.click(screen.getByRole("button", { name: "Send" }));

    await waitFor(() => expect(onLeavePhone).toHaveBeenCalledWith("(201) 555-0123"));
  });

  it("shows the host's words when it refuses the number", async () => {
    const onLeavePhone = vi.fn(async () => {
      throw new HostRefusedError(
        400,
        "/v1/public/handoff/c1/phone",
        "That phone number is not valid.",
      );
    });
    render(<HandoffBanner state={waiting} onLeavePhone={onLeavePhone} />);

    fireEvent.change(screen.getByLabelText("Your phone number"), { target: { value: "12" } });
    fireEvent.click(screen.getByRole("button", { name: "Send" }));

    expect(await screen.findByText("That phone number is not valid.")).toBeTruthy();
  });

  it("says which number will be called, and the code, once a number is left", () => {
    render(
      <HandoffBanner
        state={{ ...waiting, phone: "+1 201-555-0123", code: 1234 }}
        onLeavePhone={none}
      />,
    );

    expect(said()).toContain("We will call you at +1 201-555-0123. Your code is 1234.");
    expect(screen.queryByRole("textbox")).toBeNull();
  });

  it("stops asking once a person has the chat", () => {
    render(
      <HandoffBanner
        state={{ ...waiting, status: "human", assigneeName: "Dana R." }}
        onLeavePhone={none}
      />,
    );

    expect(screen.queryByRole("textbox")).toBeNull();
  });

  it("names the person who has the chat", () => {
    render(
      <HandoffBanner
        state={{ ...waiting, status: "human", assigneeName: "Dana R." }}
        onLeavePhone={none}
      />,
    );

    expect(said()).toContain("Dana R. is with you.");
  });
});
