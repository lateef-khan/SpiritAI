# Spirit

Our own Twenty app. Code by area under `src/`; a later area gets its own folder there.

| Area | Holds |
| --- | --- |
| `src/sales/lead/` | The Lead object: status, owner (the row rule field), source, campaign, buyer type, interest, quantity, budget, timeline, address (`siteAddress`), disqualify reason, first reply, person, company, opportunity. Tasks, notes, timeline and files work on it |
| `src/sales/lead-source/` | The Lead Source object: name, type (`sourceType`: Ad, Referral, Event, Web chat, Phone, Other), active. Staff add rows; the types are fixed here |
| `src/sales/meta-intake/` | On a Meta lead (Meta Leads app) that the Meta app has processed: one Lead with its person, an Ad source named after the campaign, the ad as campaign, status New, no owner |
| `src/sales/convert-lead/` | The Convert button on a lead: an opportunity owned by the clicking user, then the lead is Converted |
| `src/roles/` | The role the logic functions run with |

The application universal id must equal the server constant
`SPIRIT_ROW_ACCESS_OWN_APPLICATION_UNIVERSAL_IDENTIFIERS` and the row-access app's
`OWN_APPLICATION_UNIVERSAL_IDENTIFIERS`: it is how a row rule may sit on Lead.

## How it runs

- Convert runs as the clicking user (their role intersected with the app role), so the row
  rule applies: a rep cannot convert or touch another rep's lead or opportunity.
- Meta intake runs as the app alone, whoever made the Meta lead. The new lead has no owner,
  so only admins and see-all roles see it until someone sets the owner. `lead.sourceRecordId`
  (the Meta lead's id) is unique: a retry or a second event for the same Meta lead makes no
  second lead. Every source app uses this one field, so a new source adds no Lead column.
- Logic functions need `LOGIC_FUNCTION_TYPE=LOCAL` on the server and worker.

## Build and test

Node 24 (`.nvmrc`), from this folder:

```bash
yarn install --immutable
yarn lint && yarn typecheck && yarn test
yarn twenty dev:build --tarball   # .twenty/output/spirit-<version>.tgz
```

## Publish

Each publish needs a higher `version` in `package.json`; an equal one is refused.

```bash
yarn twenty remote:add --api-key <ADMIN_API_KEY> --url <SERVER_URL> --as <remote>
yarn twenty app:publish --private -r <remote>
yarn twenty app:install -r <remote>
```
