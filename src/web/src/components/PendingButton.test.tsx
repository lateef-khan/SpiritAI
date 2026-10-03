import { cleanup, render, screen } from "@testing-library/react";
import { afterEach, expect, test } from "vitest";

import { PendingButton } from "./PendingButton";

afterEach(cleanup);

test("keeps the label in the tree while pending, hidden, with a spinner over it", () => {
  const { rerender } = render(<PendingButton pending={false}>Create</PendingButton>);
  const label = screen.getByText("Create");
  expect(label.className).not.toContain("invisible");
  expect(screen.queryByRole("status")).toBeNull();

  rerender(<PendingButton pending>Create</PendingButton>);
  expect(screen.getByText("Create").className).toContain("invisible");
  expect(screen.getByRole("status")).toBeTruthy();
  expect(screen.getByRole("button", { name: /Create/ }).hasAttribute("disabled")).toBe(true);
});
