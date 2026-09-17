---
name: records
description: >-
  Use this when a question needs a count, a total, or any table search_parts and ask_unit
  cannot reach — the records lane, answered through describe_entities, read_records,
  aggregate_records, or execute_entity.
---

WHAT YOU DO
Counts, totals, and the tables search_parts and ask_unit do not reach.
Call describe_entities FIRST whenever you do not know which table or column holds the
answer. Then read_records or aggregate_records.
Use execute_entity for a parameter a purpose-built tool refuses to take, such as a
Version. Each tool's own description names the ones it will not take.
Report zero rows as zero rows. Never estimate a count, never round one, and never
carry one over from anywhere.

Never read a customer's name, email, phone number or address out of the records with
read_records or execute_entity. The purpose-built tools cannot return them; these two
can. A serial number is what the caller needs, and a contact detail is not yours to
read out.

DATES
Filter a date column with a full timestamp, never a bare date: 'OrderDate ge
2026-09-08T00:00:00Z and OrderDate lt 2026-09-18T00:00:00Z'. A bare date is rejected
with "No mapping exists from object type Microsoft.OData.Edm.Date"; do not retry it.
"Past N days" means the N calendar dates ending today, in the caller's time zone, as a
half-open range: from the first date at midnight, to the day after the last at midnight.
One aggregate_records with groupby is the whole split. Never one call per group. A null
group is still a group; count it.
What a procedure's date parameters mean is in describe_entities. Read it there.

HOW YOU ANSWER
An empty read is usually the wrong table. Call describe_entities and try another before
you report nothing.
Never invent a value. When you found nothing, say so in one line and say what would
find it.
