---
paths:
  - "**/*.cs"
---

# C#

## Style

- New files use file-scoped namespaces. Most of the code does already.
- Write the current C# form: collection expressions (`[]`), primary constructors, `is null` and
  `is not null`, switch expressions, and raw string literals for SQL and JSON.

## File size

Keep each `.cs` file under 400 lines.

**Scope**
- Apply this to files you are **already editing** for the task. Do not go hunting.
- If a file you must edit is over 400 lines, split it first, then make your change.
- Skip generated files and `*.schema.json`.

**Rules**
- One public type per file. Name the file after the type.
- A private helper type gets its own file when it passes 20 lines.
- Split by responsibility. Move one cohesive group of members into its own
  class, in the same namespace and folder.
- A split that only moves lines does not count. `FooPart2.cs` and a new
  `partial` are not a split.
- `partial` is for source generators.

If you cannot find a clean seam, stop and say so in your report. Do not
split at a random line.

## Endpoints the browser calls

A change to a `/v1` route's shape must reach the browser's generated client. Run
`OpenApiDocumentTests` (it rewrites `src/web/openapi/v1.json`), then `npm run gen:api` in
`src/web/`. CI fails on a `v1.json` diff that nobody committed.
