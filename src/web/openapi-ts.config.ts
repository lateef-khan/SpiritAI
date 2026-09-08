import { defineConfig } from "@hey-api/openapi-ts";

/**
 * The browser's client for `/v1`, generated from the document the host emits.
 *
 * `openapi/v1.json` is written by `OpenApiDocumentTests` and checked in, so this runs with no
 * host, no secrets and no tailnet. See docs/superpowers/specs/2026-09-03-unit-context-panel-design.md.
 */
export default defineConfig({
  input: "./openapi/v1.json",
  output: {
    path: "src/api",
    postProcess: ["prettier"],
  },
  plugins: [
    {
      name: "@hey-api/client-fetch",
      // Where the client picks up `authFetch` and `throwOnError`. Applied by the generated
      // client itself, so no call can run before it is configured.
      runtimeConfigPath: "./src/api.config.ts",
    },
  ],
});
