---
name: orders
description: >-
  An order's status and tracking, and a Sage invoice, for a dealer. Never with an owner's
  name, address, phone or email.
---

ORDERS AND INVOICES

THE LOOKUPS
  order_status   one order by its number, or the newest orders on one serial number: status,
                 ship date, carrier, tracking number, and the part lines. No money.
  get_invoice    Sage invoices by invoice number, sales order number, customer PO, or serial
                 number: date, items, quantities, prices, totals, packages. No names or
                 addresses, and the customer PO is never in the answer.

AN ORDER
order_status takes an order number or a serial number, never both. parse_serial a serial
number first. An order number is 'ServiceId-OrderId', as in 845435-1. A bare number with no
dash is not a whole one: say it looks incomplete and give the expected form.
Status is Open or Closed. Source 'Shipped' is a part that went out; 'Backordered' is a part
still on order. A shipped order with no TrackingNo: say it shipped on ShippedDate and that
no tracking number is on file.

AN INVOICE
get_invoice takes exactly one handle. RowKind 'line' rows are the items; 'tracking' rows
are the packages, with TrackingID and Carrier. InvoiceTotal is the whole invoice; never add
the lines yourself. ItemCode for a machine is its six-digit model number.
A serial number finds the invoice that shipped that machine, which is often an invoice for
many machines: SerialCount says how many a line shipped, and HasSerial 1 marks the line
that holds this one.
An invoice number or a sales order number may come without its leading zeros. Pass it as
given. A customer PO finds an invoice, but no answer carries a PO.
An answer holds the newest 20 invoices and at most 500 rows. An invoice with very many
lines can fill them: say the list may be cut, and ask for the invoice number.

OWNERS
These lookups carry no owner details, and you give none.
