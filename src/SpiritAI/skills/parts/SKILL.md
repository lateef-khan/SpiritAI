---
name: parts
description: >-
  Use this when the person asks about a part, a part number, or a parts list, or reports a
  symptom — a noise, a fault, a code, or something that stopped working — that points at a
  part. Covers what to ask for and never ask for when calling search_parts, running a symptom
  down two lanes at once, how a machine's build year works, and reading a serial or model
  number out of what the person gave you.
---

A SYMPTOM RUNS DOWN TWO LANES
A noise, a fault, a code, a thing that stopped working — that is a symptom. Do both of
these in the SAME step, never one after the other:
  1. Search the manuals for the cause.
  2. Call search_parts once for each part the symptom points at, such as motor, then
     belt, then roller.
Then give the person the cause AND what the lookups came back with.

HOW YOU FIND THE PARTS
1. A product name. Call search_parts with Name and Search. One call.
2. A product name AND a year. Call find_model with the product name and look for that
   year in the model names it returns. A row named "SOLE F63 2016" IS the 2016 machine;
   use its model number and call search_parts with ModelNo. Only when no model name
   carries the year does lookup_model decide it, as in step 4.
3. OtherModelNos came back non-empty and no year has been given yet. The parts may
   differ by year. Call lookup_model with the product name for the authoritative year
   list from the manuals, and ask which year, naming those years.
4. No model name carries the year the person gave. lookup_model with the name and year
   gives a confirmed model number. Then call search_parts with ModelNo and Search.
5. A serial was offered. search_parts takes SerialNo directly.
If a word finds nothing, try the next word for the same part before you conclude the
machine does not list it: motor, then drive, then controller.

WHAT YOU ASK FOR, AND WHAT YOU NEVER ASK FOR
The ONLY thing you ever ask for to answer a parts question is the YEAR, and only when a
product name covers more than one model number.
Never ask for a serial number or a model number to answer a parts question. A person
standing at a machine can say the year; they cannot read out a sixteen digit number, and
they do not know their model number.
  Bad:  "Give me the 16-digit serial number and I will identify the parts."
  Bad:  "Which model number is yours? 563286, 563812, 563814, ..."
  Good: "That is usually the drive motor or the belt. What year is your F63? It was built
         in 2013, 2015, 2016 and 2019, and the parts differ."
Take a serial number when the person offers one, and pass it straight through.

THE YEAR A MACHINE WAS BUILT
The years a machine was built come from lookup_model. Never state a build year on the
strength of a parts row. The records name a year for some machines and not others, and
a machine missing from them was still built.
That rule is about TELLING a person which years a machine exists in: that list always
comes from lookup_model, never from a parts row. It is a different question from
MATCHING a year the person already gave you to one of find_model's own model numbers,
which is step 2 above — there you are not stating a build year, you are only reading
which row the person's own year points at, so find_model's model names are fine to use.
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
