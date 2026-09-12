import { describe, expect, it } from "vitest";

import { clockTime, minutesBetween } from "./format";

describe("minutesBetween", () => {
  it("rounds down to whole minutes", () => {
    const from = new Date(2026, 8, 12, 12, 38, 0);
    const to = new Date(2026, 8, 12, 12, 52, 59);

    expect(minutesBetween(from, to)).toBe(14);
  });

  it("is zero for the same instant", () => {
    const at = new Date(2026, 8, 12, 12, 38, 0);

    expect(minutesBetween(at, at)).toBe(0);
  });
});

describe("clockTime", () => {
  it("writes the hour and minute, zero-padded", () => {
    expect(clockTime(new Date(2026, 8, 12, 9, 5))).toBe("09:05");
  });

  it("uses 24-hour time", () => {
    expect(clockTime(new Date(2026, 8, 12, 14, 38))).toBe("14:38");
  });
});
