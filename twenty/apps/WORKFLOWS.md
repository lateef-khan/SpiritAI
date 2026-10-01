# Workflows made in the screen

Staff can build Twenty workflows in Settings → Workflows. List each one here, so we know what
runs, and who to ask before we change the data model under it.

**Rule: if turning the workflow off would make data wrong, it belongs in code.** Put it in an
app under `twenty/apps/` (for sales: `apps/spirit/src/sales/`), with a test. A workflow is for
a nudge or a convenience: a reminder task, an email to a person, a Slack message. A workflow
runs with an Admin role and sees every row, so it must never copy a rep's record to a place
other reps can read.

| Name | Trigger | What it does | Owner | Added |
| --- | --- | --- | --- | --- |
| _example:_ New lead reminder | Lead created | Makes a task "Call within 24 h" for the lead's owner | Sales manager | 2026-10-01 |
