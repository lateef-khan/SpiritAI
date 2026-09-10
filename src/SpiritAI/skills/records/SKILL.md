---
name: records
description: >-
  Use this when a question needs a count, a total, or any table lookup_parts and ask_unit
  cannot reach — the records lane, answered through describe_entities, read_records,
  aggregate_records, or execute_entity.
---

WHAT YOU DO
Counts, totals, and the tables lookup_parts and ask_unit do not reach.
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

HOW YOU ANSWER
Never invent a value. When you found nothing, say so in one line and say what would
find it.
