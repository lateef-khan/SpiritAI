import { describe, expect, it } from "vitest";

import { createApiClient, HostRefusedError } from "./apiClient";

/**
 * The refusal seam, one test per shape a refused response comes in.
 *
 * `createApiClient` takes a `FetchLike` in place of `authFetch`, so a test can answer with
 * whatever `Response` it likes and reach no network. What is worth holding in place here is that
 * a problem-detail body's `title` and `detail` end up on the thrown error, and that a body with
 * neither — the shape most refusals actually have — still throws with both set to `null`.
 */
describe("createApiClient refusals", () => {
  it("reads title and detail off a problem+json body", async () => {
    const client = createApiClient(
      async () =>
        new Response(
          JSON.stringify({ title: "Somebody has this chat.", detail: "Dana took it." }),
          {
            status: 409,
            headers: { "content-type": "application/problem+json" },
          },
        ),
    );

    const refusal = await client
      .get({ url: "/v1/handoff/call-1/claim" })
      .then(() => null)
      .catch((error: unknown) => error);

    expect(refusal).toBeInstanceOf(HostRefusedError);
    expect((refusal as HostRefusedError).status).toBe(409);
    expect((refusal as HostRefusedError).title).toBe("Somebody has this chat.");
    expect((refusal as HostRefusedError).detail).toBe("Dana took it.");
  });

  it("leaves title and detail null on an empty body", async () => {
    const client = createApiClient(async () => new Response(null, { status: 404 }));

    const refusal = await client
      .get({ url: "/v1/handoff/call-1/claim" })
      .then(() => null)
      .catch((error: unknown) => error);

    expect(refusal).toBeInstanceOf(HostRefusedError);
    expect((refusal as HostRefusedError).status).toBe(404);
    expect((refusal as HostRefusedError).title).toBeNull();
    expect((refusal as HostRefusedError).detail).toBeNull();
  });
});
