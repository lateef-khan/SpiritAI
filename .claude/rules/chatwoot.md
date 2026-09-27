---
paths:
  - "chatwoot/**"
  - "src/SpiritAI/Chatwoot/**"
---

# Chatwoot

Read `chatwoot/README.md` before you change Chatwoot's setup or Spirit's code that talks to it.
These sections hold rules a change can break without an error:

- **The Spirit inbox**: no webhook, the agent bot stays connected, the out-of-office message stays
  empty.
- **The public contact guard**: `initializers/spirit_public_contact_guard.rb` closes a contact
  leak. Run `just chatwoot check-contact-guard <inbox identifier>` after every version
  change.

`chatwoot/staff-call-alerts.md` is written for staff, not developers. Keep its plain words when
you change the call alert flow.
