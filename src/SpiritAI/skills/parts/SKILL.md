---
name: parts
description: >-
  Use this when the person asks about a part, a part number, or a parts list, or reports a
  symptom — a noise, a fault, a code, or something that stopped working — that points at a
  part. Covers what to ask for and never ask for when calling lookup_parts, running a symptom
  down two lanes at once, how a machine's build year works, and reading a serial or model
  number out of what the person gave you.
---

A SYMPTOM RUNS DOWN TWO LANES
A noise, a fault, a code, a thing that stopped working — that is a symptom. Do both of
these in the SAME step, never one after the other:
  1. Search the manuals for the cause.
  2. Call lookup_parts once for EACH part the symptom points at, one word each, such as
     motor, then belt, then roller.
Then give the person the cause AND what the lookups came back with.

WHAT YOU ASK FOR, AND WHAT YOU NEVER ASK FOR
lookup_parts decides which model number a question is about. You never do.
So the ONLY thing you ever ask for to answer a parts question is the YEAR, and you ask it
only when lookup_parts comes back needs_year. Name the years it listed.
Never ask for a serial number or a model number to answer a parts question. A person
standing at a machine can say the year; they cannot read out a sixteen digit number, and
they do not know their model number.
  Bad:  "Give me the 16-digit serial number and I will identify the parts."
  Bad:  "Which model number is yours? 563286, 563812, 563814, ..."
  Good: "That is usually the drive motor or the belt. What year is your F63? It was built
         in 2013, 2015, 2016 and 2019, and the parts differ."
Take a serial number when the person offers one, and pass it straight through.

THE YEAR A MACHINE WAS BUILT
The years a machine was built come from lookup_model. Nothing else knows them, and the
'years' lookup_parts hands you are that same list, relayed.
Never work a build year out of a parts row yourself. The records name a year for some
machines and not others, and a machine missing from them was still built.
Never tell a person the year they gave you is wrong. They are standing at the machine.
If you cannot resolve their year, say what you need next, not what you could not find.
  Bad:  "Your stated 2023 year does not match the build year currently listed as 2019."
  Good: "Which console does it have, a touchscreen or a blue LCD? That tells me which
         parts list to pull."

Call parse_serial on any number offered as a serial, before any other tool. Never cut the
model number or the build month out of the digits yourself.
A serial number already carries the model number, so never ask for a model number when
a serial is in the question.
When parse_serial says the text is not a serial number, say so, and say how many
digits it counted.
Keep leading zeros. Treat serials and model numbers as text, never as arithmetic.
