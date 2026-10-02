---
name: records
description: >-
  Who owns a machine and how to reach them, its service calls, its work orders and their
  money, and its Sage invoices with who was billed. For Spirit staff.
---

RECORDS: OWNERS, SERVICE CALLS, WORK ORDERS, INVOICES

THE LOOKUPS
  get_unit                   one serial number: what it is, who owns it, every warranty term and its date.
  find_units                 a person (name, email, phone) to their machines, with the serial numbers.
  get_service_history_by_sn  one serial number: every service call, with what was wrong and what was done.
  get_work_orders            work orders, by order number, by serial number, by product, by date, by status.
  get_invoice_full           Sage invoices, by invoice number, sales order, customer PO, or serial number,
                             with who was billed and where it went.
get_unit gives everything check_unit gives, plus the owner. For a serial number, call
get_unit in place of check_unit. If get_unit finds no purchase record (error 51091), use
check_unit for the machine facts.

ONE UNIT
A model narrows; a person identifies. A model alone matches thousands of machines, so a
unit question needs one identifying fact before any unit lookup: a serial number, a work
order number, or the owner's email, phone or name. When the person named only a machine,
ask in one sentence for the email or phone it was registered under, or the serial number
off the frame; email and phone are what a person can say from memory.
  A serial number.  parse_serial first, then get_unit.
  An order number.  'ServiceId-OrderId', as in 845435-1. get_work_orders with
                    OrderNumbers. A bare number with no dash is not a whole one: pass it as
                    given, and if nothing comes back say it looks incomplete and give the
                    expected form.
  A person.         find_units on the ONE field the text is: it has an @, Email; seven or
                    more digits, Phone; otherwise Name. Add ModelNo when the person also
                    named their machine, so one email that owns three units returns the
                    one they mean. Several units still: ask which, naming each by model
                    and purchase date. Take the SerialNo from the row into get_unit,
                    get_service_history_by_sn or get_work_orders.
Who owns a machine, and how to reach them, is on the unit: CustomerName, Address, City,
State, Zip, Phone, Phone2, Email. Read them back when asked.
get_unit's warranty rows read the same as check_unit's: Term, Days, Lifetime, Expires,
InWarranty. Never add days to a date yourself.

WORK ORDER MONEY
get_work_orders with IncludeDetail 1 adds Freight, LaborFee and TripFee, and IncludeParts 1
adds the part lines. LaborFee and TripFee are what Spirit pays the service company (the
ISP) that did the repair; ISPName names it. Freight is what shipping the parts cost.
OrderType says who paid: 'Warranty' is on Spirit, 'Part Purchase' is a sale to the person.
Give each amount as it stands on the order.

INVOICES
get_invoice_full takes exactly one handle. RowKind 'line' rows are the items; 'tracking'
rows are the packages, with TrackingID and Carrier. InvoiceTotal is the whole invoice;
never add the lines yourself. ItemCode for a machine is its six-digit model number.
A serial number finds the invoice that shipped that machine, which is often a dealer's
invoice for many machines: SerialCount says how many a line shipped, and HasSerial 1 marks
the line that holds this one. The bill-to is who Spirit invoiced, often the dealer, not the
owner. An invoice number or a sales order number may come without its leading zeros; pass
it as given. An answer holds the newest 20 invoices and at most 500 rows. An invoice with
very many lines can fill them: say the list may be cut, and ask for the invoice number.
