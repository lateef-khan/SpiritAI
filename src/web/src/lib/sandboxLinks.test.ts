import { describe, expect, it } from "vitest";

import { resolveSandboxLinks } from "./sandboxLinks";

const links = new Map([["chart.png", "https://files.test/chart.png?t=1"]]);

describe("resolveSandboxLinks", () => {
  it("points a markdown link at the kept file", () => {
    expect(resolveSandboxLinks("See [the chart](sandbox:/mnt/data/chart.png).", links)).toBe(
      "See [the chart](https://files.test/chart.png?t=1).",
    );
  });

  it("points an inline image at the kept file", () => {
    expect(resolveSandboxLinks("![chart](sandbox:/mnt/data/chart.png)", links)).toBe(
      "![chart](https://files.test/chart.png?t=1)",
    );
  });

  it("unwraps a link to a file the host did not keep", () => {
    expect(resolveSandboxLinks("Download [rows](sandbox:/mnt/data/rows.csv) now.", links)).toBe(
      "Download rows now.",
    );
  });

  it("turns a bare unkept link into the file name", () => {
    expect(resolveSandboxLinks("Saved to sandbox:/mnt/data/rows.csv", links)).toBe(
      "Saved to rows.csv",
    );
  });

  it("leaves text with no sandbox link alone", () => {
    const text = "Nothing to see [here](https://example.test).";
    expect(resolveSandboxLinks(text, links)).toBe(text);
  });

  it("reads the name from any sandbox directory", () => {
    expect(resolveSandboxLinks("[chart](sandbox:/tmp/outputs/chart.png)", links)).toBe(
      "[chart](https://files.test/chart.png?t=1)",
    );
  });

  it("turns a bare link in a deep directory into the file name when unkept", () => {
    expect(resolveSandboxLinks("Saved sandbox:/home/user/out/rows.csv", links)).toBe(
      "Saved rows.csv",
    );
  });

  it("reads a percent-encoded name", () => {
    expect(resolveSandboxLinks("[x](sandbox:/mnt/data/chart%20one.png)", new Map([["chart one.png", "https://f/1"]]))).toBe(
      "[x](https://f/1)",
    );
  });
});
