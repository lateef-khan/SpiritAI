import assert from "node:assert/strict";
import { describe, it } from "vitest";

import { chipsOf, identifiersIn, type Said } from "./useIdentifiers";

const said = (role: string, text: string): Said => ({ role, text });

describe("identifiersIn", () => {
  it("finds a serial", () => {
    assert.deepEqual(identifiersIn("it is 5808881004036047 I think"), [
      { kind: "serial", value: "5808881004036047" },
    ]);
  });

  it("finds a work order", () => {
    assert.deepEqual(identifiersIn("order 845435-1 please"), [
      { kind: "order", value: "845435-1" },
    ]);
  });

  it("ignores a bare six-digit model number", () => {
    // A wrong match is worse than no match, and too many six-digit numbers in a service
    // conversation are not model numbers.
    assert.deepEqual(identifiersIn("model 100007"), []);
  });

  it("ignores a date, which is digits and dashes but not an order", () => {
    assert.deepEqual(identifiersIn("it broke on 2026-09-03"), []);
  });

  it("does not cut a serial out of a longer run of digits", () => {
    assert.deepEqual(identifiersIn("58088810040360471234"), []);
  });

  it("keeps both numbers in one message, in the order they were written", () => {
    assert.deepEqual(identifiersIn("was it 12345-1 or 5808881004036047?"), [
      { kind: "order", value: "12345-1" },
      { kind: "serial", value: "5808881004036047" },
    ]);
  });
});

describe("chipsOf", () => {
  it("reads only the person's own turns", () => {
    // An assistant message repeating a wrong number back would otherwise re-trigger the same
    // failed lookup.
    const chips = chipsOf([
      said("user", "5808881004036047"),
      said("assistant", "I could not find 9999999999999999."),
    ]);

    assert.deepEqual(
      chips.map((chip) => chip.value),
      ["5808881004036047"],
    );
  });

  it("puts the newest first, which is what a correction relies on", () => {
    const chips = chipsOf([
      said("user", "my order number is 12345-1"),
      said("assistant", "nothing found"),
      said("user", "sorry, 12344-1"),
    ]);

    assert.deepEqual(
      chips.map((chip) => chip.value),
      ["12344-1", "12345-1"],
    );
  });

  it("keeps the older number reachable after a correction", () => {
    const chips = chipsOf([said("user", "12345-1"), said("user", "12344-1")]);

    assert.equal(chips.length, 2);
  });

  it("names each number once, however often it is said", () => {
    const chips = chipsOf([
      said("user", "12345-1"),
      said("user", "12344-1"),
      said("user", "back to 12345-1"),
    ]);

    // Said again, so it moves to the front rather than staying where it first appeared.
    assert.deepEqual(
      chips.map((chip) => chip.value),
      ["12345-1", "12344-1"],
    );
  });

  it("is unchanged by a message with no number in it", () => {
    const before = chipsOf([said("user", "5808881004036047")]);
    const after = chipsOf([said("user", "5808881004036047"), said("user", "and the belt slips")]);

    // The person is still talking about the same machine, so the panel keeps it.
    assert.deepEqual(after, before);
  });

  it("is empty before anybody has pasted anything", () => {
    assert.deepEqual(chipsOf([said("user", "hello")]), []);
  });
});
