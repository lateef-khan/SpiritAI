# Twenty extension apps

Each folder here is one app, made with Twenty's app scaffolder and built on `twenty-sdk`.
An app can add objects, fields, views, UI parts, logic functions, and AI agents to a workspace.
It cannot change how Twenty reads data. That needs a change in `../source/`.

## Make an app

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
