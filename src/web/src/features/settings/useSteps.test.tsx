import { act, renderHook } from "@testing-library/react";
import { expect, test } from "vitest";

import { HostRefusedError } from "@/lib/apiClient";

import { useSteps } from "./useSteps";

test("runs steps in order and stops at the first that fails, with its words", async () => {
  const ran: string[] = [];
  const { result } = renderHook(() => useSteps());

  let finished!: boolean;
  await act(async () => {
    finished = await result.current.start([
      {
        label: "One",
        run: async () => {
          ran.push("one");
        },
      },
      {
        label: "Two",
        run: async () => {
          throw new HostRefusedError(403, "x", null, "Refused.");
        },
      },
      {
        label: "Three",
        run: async () => {
          ran.push("three");
        },
      },
    ]);
  });

  expect(finished).toBe(false);
  expect(ran).toEqual(["one"]);
  expect(result.current.states.map((s) => s.state)).toEqual(["done", "failed", "waiting"]);
  expect(result.current.states[1].error).toBe("Refused.");
});

test("a server refusal with no detail names the status, not a lost connection", async () => {
  const { result } = renderHook(() => useSteps());

  await act(async () => {
    await result.current.start([
      {
        label: "Save the roles",
        run: async () => {
          throw new HostRefusedError(404, "/v1/settings/people/x/roles");
        },
      },
    ]);
  });

  expect(result.current.states[0].error).toBe("The server answered 404.");
});
