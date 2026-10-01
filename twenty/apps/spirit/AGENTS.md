# Spirit app

Twenty app (twenty-sdk 2.41.0, same as the server). Read `README.md` first.

- One folder per area under `src/` (`src/sales/…`); each area keeps its own universal ids in a
  `*-universal-identifiers.constant.ts`. New ids are random UUID v4, never changed after release.
- Pure logic goes in `utils/` with a vitest test next to it (`__tests__/*.test.ts`) that uses
  real example values. Logic functions stay thin: GraphQL calls and the util.
- Logic functions call GraphQL through `src/shared/twenty-graphql.ts`. Say which access each
  call uses: `'person'` (the clicking user, row rule applies) or `'application'` (the app role
  alone). Never make the app role see-all.
- Select values that code compares against live in `*-options.constant.ts`.
- Twenty app docs: https://docs.twenty.com/developers/extend/apps/getting-started/quick-start
