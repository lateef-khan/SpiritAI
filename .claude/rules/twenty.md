---
paths:
  - "twenty/*"
  - "twenty/apps/**"
---

# Twenty

Read `twenty/README.md` before you change how Twenty runs, is released, or is extended.

- `twenty/source/` is a git submodule with its own `CLAUDE.md`. Commit inside it on branch
  `spirit`, then commit the new pointer in SpiritAI.
- Twenty's code is AGPL-3.0 except the enterprise files. Change only the AGPL code. Do not copy or
  unlock enterprise code.
- A release tag is `<Twenty version>-spirit.<n>`. Never move a tag.
- Production redis must stay `noeviction`. An evicted key is a lost job.
