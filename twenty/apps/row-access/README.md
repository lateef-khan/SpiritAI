# Row access

Settings for owner row access in our Twenty fork. A signed-in user sees only the records they
own on the objects an admin protects here. The server (`twenty/source`, module
`packages/twenty-server/src/engine/twenty-orm/spirit-row-access/`) enforces the rule; this app
only stores and edits its config. Design:
`docs/superpowers/specs/2026-09-27-twenty-owner-row-access-design.md`.

## What it holds

- `src/application-config.ts`: the app and its one variable, `ROW_ACCESS_CONFIG` (JSON, not
  secret). Installed value: `{"version":1,"rules":[],"seeAllRoleIds":[]}`, which filters
  nothing.
- `src/default-role.ts`: the app role, `APPLICATIONS` + `ROLES`, no record access.
- `src/front-components/`: the page under Settings → Applications → Row access → Settings. It
  offers Company, Opportunity, Task and the workspace's own custom objects that have an owner
  field (a relation to a workspace member), and the roles that see every row. Admin always
  sees every row.
- `src/utils/`: the page's config logic, unit tested with vitest.

The application universal id must equal the server constant
`SPIRIT_ROW_ACCESS_APPLICATION_UNIVERSAL_IDENTIFIER`.

## Build and test

Node 24 (`.nvmrc`), from this folder:

```bash
yarn install --immutable
yarn lint && yarn typecheck && yarn test
yarn twenty dev:build --tarball   # .twenty/output/row-access-<version>.tgz
```

The server integration specs install that tarball. They find it through
`SPIRIT_ROW_ACCESS_APP_DIR`, or by default in this folder's `.twenty/output`.

## Publish

Each publish needs a higher `version` in `package.json`; an equal one is refused.

```bash
yarn twenty remote:add --api-key <ADMIN_API_KEY> --url <SERVER_URL> --as <remote>
yarn twenty app:publish --private -r <remote>
yarn twenty app:install -r <remote>
```

Then save the config on the settings page. The server applies it only while
`SPIRIT_ROW_ACCESS_ENFORCED=true` is set on the server and the worker.
