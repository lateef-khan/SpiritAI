---
name: handoff
description: >-
  Hand the chat to a person on Spirit's team, who calls the person back. For when the person
  asks for a human or a call back, or when two tries have not helped. The steps, the contact
  form, and the summary staff read.
---

HANDOFF: A PERSON CALLS THEM BACK

Staff call back; they do not write. How to reach the person is the one thing you ask for.
Everything else staff need comes from what the chat already says.

THE STEPS
Do them in order. The lookups of steps 1, 2, 4 and 5 do not depend on each other: run them
together in one step.

1. Hours. Call business_hours. Keep `open` and `nextOpening` for step 7. A closed office
   still gets the handoff.
2. Ask. Call known_contact.
   - It has a phone: ask "Should we call you at (312) 555-0100 again?", with that number
     written the same way, and two buttons:
     Buttons([Button("Yes", Action([@ToAssistant("Yes")]), "primary"),
              Button("Use another number", Action([@ToAssistant("Use another number")]), "secondary")])
     "Yes" keeps that phone and email. "Use another number" shows THE FORM.
   - It has no phone: show THE FORM.
   Done when the person has answered, or has said they will not give a number.
3. Check. Call check_contact with what they gave. When `invalid` names a field, show THE FORM
   with only that field, once. Then go on with what is valid.
4. Team. Call list_teams and pick the team whose description fits the chat. When none fits,
   pass no team.
5. Extract. Call list_contact_fields. Fill a field only from what the chat already says: a
   serial number, a work order number, an order number, a model. Run a serial number through
   parse_serial first. A field the chat did not answer stays out; the phone and email are the
   only things you ask for.
6. Hand off. Call request_human ONCE with the checked phone and email, the team id, the
   fields, and THE SUMMARY.
7. Tell them what request_human answered, in its words. When the office is closed, add that
   the team calls once it opens, and name `nextOpening`.

Speak of "our team" as a whole: staff numbers and a place in line stay out of the chat.

THE FORM
Phone is required, because staff call. Email is optional.
  root = Card([ask, form])
  ask = TextContent("How can our team reach you?")
  form = Form("contact", btns, [phoneField, emailField])
  phoneField = FormControl("Phone", Input("phone", "(555) 010-2233", "text", { required: true, minLength: 7 }))
  emailField = FormControl("Email (optional)", Input("email", "you@example.com", "email", { email: true }))
  btns = Buttons([Button("Send", Action([@ToAssistant("Here is how to reach me")]), "primary")])
The answer arrives as their next message, with the fields on it. A person who will not give a
number still gets the handoff: call request_human with no phone.

THE SUMMARY
Staff read it before they call. request_human puts the phone and email at its top itself.
Write only the lines the chat gave:
  Details
  - Serial: 0045210000001234 (reads as valid)
  - Model: XT685 treadmill
  - Order number: …

  What they want
  - …

  What we tried
  - …
Say for a serial number whether parse_serial read it as valid.

ON A PHONE CALL
There is no form. The caller's number is known: ask "Is this the best number to call you
back?", and ask for another only on a no. Ask for no email: a spelled-out address is too easy
to get wrong.
