---
name: my-unit
description: >-
  The person's own machine: who it is registered to, its orders, their tracking, and what
  they were charged. Only after the person gives the phone number or email it was
  registered under.
---

THE PERSON'S OWN MACHINE

THE LOOKUP
  my_unit   a serial number plus the phone number or the email on file. When the machine
            and the details go together: the machine, the owner on file, each warranty
            term, and its newest 20 orders with tracking and what was charged. Otherwise:
            nothing.

WHEN TO USE IT
The person asks who their machine is registered to, about their order, where their parts
are, a tracking number, or what they were charged. Anything about a person or their orders
goes through my_unit and nothing else.
You need two things: the serial number off the frame, and the phone number or email the
machine was registered under. Ask for what is missing, in one sentence. If they give both
a phone and an email, pass both.
  Good: "What phone number or email did you register it with?"
A ZIP code, a name or an address is not proof. Do not take one in place of the phone or
email.
parse_serial the serial number first, like any other.

WHEN IT RETURNS NOTHING
Nothing back can mean many things, and you never know which. Never say or guess why: not
that a detail is wrong, not that the machine is unregistered, not which part did not fit.
Say in one sentence that you could not pull it up with those details, and ask if there is
another phone number or email it could be under. After two tries that return nothing, stop
asking and offer a person: load the `handoff` skill.
  Bad:  "The phone on file ends in 0100."
  Bad:  "That phone does not match the registration."
  Good: "I could not pull it up with those. Is there another email or phone it could be
         under?"
Never hint at what is on file, and never read back a detail my_unit did not return.

WHAT IT RETURNS
RowKind 'unit' is the machine and the owner on file. 'warranty' rows are the terms: read
Expires, InWarranty and DaysLeft the same way as check_unit's. 'order' rows are the orders:
Status, Carrier, TrackingNo, ShippedDate, and what was charged: PartsAmount for the parts,
then Freight, HandlingFee and Tax.
What it returns is the person's own: give them what they asked for.
