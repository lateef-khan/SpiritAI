# Twenty extension apps

Each folder here is one app, built on `twenty-sdk`. An app can add objects, fields, views, UI
parts, logic functions, and AI agents to a workspace. It cannot change how Twenty reads data.
That needs a change in `../source/`.

| App | Holds |
| --- | --- |
| `spirit/` | Our own app: every object and logic function we write, one folder per area under `src/` (`src/sales/`: Lead, Lead Source, Meta intake, Convert). A new area gets a new folder there, not a new app |
| `row-access/` | The settings page for the owner row rule. Separate on purpose: the server finds it by its universal id |

Marketplace apps (for example Meta Leads by Appunite) are installed from Settings →
Applications and never changed. To use their data, react to their events from `spirit`
(Meta intake listens to `metaLead.created`) or link through a standard object such as Person.
A relation field from our app to another app's object cannot be installed.

`WORKFLOWS.md` lists the workflows staff make in the screen.

## Rules for app code

- The server allows the row rule on objects of `spirit` by its universal id
  (`SPIRIT_ROW_ACCESS_OWN_APPLICATION_UNIVERSAL_IDENTIFIERS` in the fork, and the same list in
  `row-access`). Never change that id. A new app that needs a row rule must be added to both
  lists, and its logic functions reviewed like `spirit`'s: read rule objects only as the person
  who triggered the run, and never make the app role see-all.
- Logic functions need `LOGIC_FUNCTION_TYPE=LOCAL` on the server and worker
  (`TWENTY_LOGIC_FUNCTION_TYPE` in `secrets/<env>.env`). They run as child processes of the
  server and can read its environment, so install only apps whose code we trust.

## Make an app

Only when a new app is really needed; a new area of our own code goes into `spirit/src/`.

```bash
cd twenty/apps
npx create-twenty-app@latest <app-name>
```

Name the folder after the app, in kebab-case. The scaffolder writes the app's own
`package.json`, `.gitignore`, and README. Commit the whole folder.

The scaffolder starts a Twenty of its own in Docker, on `http://localhost:2020`. To point the
app at our local Twenty instead (`just twenty up`), pass `--url http://localhost:53001`.

Match the `twenty-sdk` version to the server version in `../compose.yaml`. Twenty releases the
SDK with the same number (tags `sdk/vX.Y.Z`).

Docs: https://docs.twenty.com/developers/extend/apps/getting-started/quick-start
