import { describe, expect, it } from "vitest";

import { safeReturnTo } from "./returnTo";

const origin = "https://hub.spiritfitnessapps.com";

describe("safeReturnTo", () => {
  it.each([
    ["/#desk", "/#desk"],
    ["/chat/", "/chat/"],
    ["/chat/settings.html?x=1", "/chat/settings.html?x=1"],
  ])("keeps the same-origin path %s", (raw, expected) => {
    expect(safeReturnTo(raw, origin)).toBe(expected);
  });

  it.each([
    null,
    "",
    "//evil.com",
    "https://evil.com/",
    "/\\evil.com",
    "javascript:alert(1)",
    "https://hub.spiritfitnessapps.com.evil.com/",
    // Ruling: a backslash anywhere in the value is refused, not only a leading one.
    "/chat/\\evil.com",
    // Ruling: a control character in the value is refused.
    "/chat/\n.html",
    // A dot segment collapses into a leading `//` once `URL` normalises the path, which a browser
    // then reads as protocol-relative to the host after it — even though none of these values
    // start with `//` or contain a backslash themselves.
    "/.//evil.com",
    "/%2e%2e//evil.com",
    "/a/..//evil.com",
  ])("sends %s to the Hub", (raw) => {
    expect(safeReturnTo(raw, origin)).toBe("/");
  });
});
