import js from "@eslint/js";
import prettier from "eslint-config-prettier";
import reactHooks from "eslint-plugin-react-hooks";
import globals from "globals";
import tseslint from "typescript-eslint";

/**
 * What the linter is here for, and what it deliberately is not.
 *
 * `tsc --strict` already rejects bad types, unused locals and fallthrough cases, so this adds only
 * what a type checker structurally cannot see: whether a hook is called under a condition, and
 * whether an effect lists everything it reads. Those produce stale data at runtime and type-check
 * perfectly.
 *
 * Prettier owns layout, so `eslint-config-prettier` goes last and turns off every rule that would
 * argue with it. A rule that fires here should always be about behaviour.
 */
export default tseslint.config(
  { ignores: ["../SpiritAI/wwwroot/**"] },

  js.configs.recommended,
  tseslint.configs.recommended,
  reactHooks.configs.flat["recommended-latest"],
  prettier,

  {
    files: ["**/*.{js,ts,tsx}"],
    languageOptions: {
      globals: globals.browser,
    },
  },

  {
    // This config and the Vite config run in Node, not in a browser.
    files: ["eslint.config.js", "vite.config.ts"],
    languageOptions: {
      globals: globals.node,
    },
  },

  {
    /*
     * The React Compiler rules, held at warning rather than error.
     *
     * They are advice about re-renders and memoisation, not reports of broken behaviour, and today
     * they fire almost entirely inside the vendored shadcn and assistant-ui components. Making them
     * fail the build would mean rewriting other people's components to get a green build, which is
     * a change to how the UI renders rather than a lint fix.
     *
     * They stay switched on because the advice is worth reading. Promote any one of them to "error"
     * once its findings are cleared.
     */
    files: ["**/*.{ts,tsx}"],
    rules: {
      "react-hooks/set-state-in-effect": "warn",
      "react-hooks/static-components": "warn",
      "react-hooks/immutability": "warn",
      "react-hooks/purity": "warn",
      "react-hooks/refs": "warn",
      "react-hooks/preserve-manual-memoization": "warn",
      "react-hooks/use-memo": "warn",
      "react-hooks/void-use-memo": "warn",
    },
  },

  {
    // A stub that stands in for a streaming host has nothing to yield, and must still be a
    // generator to match the type it replaces.
    files: ["**/*.test.{ts,tsx}"],
    rules: {
      "require-yield": "off",
    },
  },
);
