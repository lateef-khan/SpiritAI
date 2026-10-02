---
name: analytics
description: >-
  A count, a total, a breakdown, a rank, a trend, a join across tables or across the two
  databases, a chart, a report, a CSV, a spreadsheet, or a PDF. Says which lookup counts in
  one call, when to go to the shell, what the tables are and how they join, what the
  computer has, and how a finished file reaches the person.
---

ONE CALL, OR THE SHELL
Let the database count. A how-many, a total, or a group-by is one call or one query, never
your own arithmetic, and never one call per group.
  count_units   registered units by Brand, Category, Model, State, Dealer or Year, with any
                filter, in ONE call. Use it whenever the grouping or a filter is a product
                fact. Report TotalUnits and TotalGroups from the answer, never a sum of the
                rows sent.
  the shell     everything else: a join, a distinct count, a rank, a change between periods,
                units sold, anything from Sage, a chart, a report, a CSV, a spreadsheet, a PDF.
Filter by every fact the person gave you. Counts are whole numbers. A percent has one
decimal place and a ratio two, however many digits the math produced. If a filter matched
nothing, say which one. Report zero rows as zero rows.

THE SERVICE RECORDS (CustService)
PURCHASE (944k)            one row per registered machine, key SERIALNO. MODELNO, VERSION,
                           DETAMFG (build month, MM/YYYY), PURCHASEDDATE, STATE, DEALERNO,
                           and the owner: PURCHASEDBY, ADDRESS, CITY, ZIP, PHONE, PHONE2, EMAIL.
SERVICEDESCRIPTION (791k)  one row per service call, key SERVICEID. SERIALNO, CALLDATE,
                           SERVICEREP, STATUS. A call has zero or more orders and notes.
OrderTable (697k)          one row per work order. The number people quote is ServiceId-OrderId.
                           OrderDate, ClosedDate, OrderType ('Warranty', 'Part Purchase'...),
                           CaseStatus ('True' is closed, 'False' or null is open), Tech, ISPName,
                           DealerNo, Freight, LaborFee, TripFee. The real key is OrderTableID; a
                           few order numbers carry two rows.
OrderDetail (1.39M)        one row per part line on an order: SP_NO, Desc, Item_Shipped, Return,
                           SaleAmount.
OrderDetailBack (2k)       the current backorder queue, same shape. Not history.
PROBLEM (497k)             one free-text note per service call, per ORDERID.
ProblemCode (76)           the problem vocabulary. Reference, not a join.
Spareparts (28.8k)         the parts catalogue and stock, key SP_NO: DESC, ON_HAND, RETAIL_PRICE,
                           DEALER_PRICE.
ModelSP (140k)             the parts list per model version: Model_No, Version, Part_No, Qty.
ModelWarranty (1.3k)       warranty terms per model version in DAYS; 36500 is lifetime.
ModelDetailWarranty (218)  the same as printed for customers, in years; fewer models.
MODEL                      select its columns by name: MODELNO, VERSION, MODEL, FG (category),
                           Brand, Commercial. Never SELECT * on it (sql_variant columns).
THE JOIN PATH
  PURCHASE.SERIALNO -> SERVICEDESCRIPTION.SERIALNO -> OrderTable.ServiceId
  OrderTable (ServiceId, OrderId) -> OrderDetail (SERVICEID, OrderID) -> Spareparts.SP_NO
  PURCHASE.MODELNO -> MODEL.MODELNO, ModelSP.Model_No, ModelWarranty.ModelNo
A serial number's first six characters are its MODELNO; PURCHASE.MODELNO holds them as a
column, so filter on it rather than on a prefix. A product name becomes model numbers
through find_model; then filter MODELNO. Warranty vs paid is OrderTable.OrderType;
SERVICEDESCRIPTION.WARRANTY has been stale since 2011. A quarter of service calls have no
work order, so a history counted off OrderTable undercounts.
The collation is Chinese_Taiwan_Stroke_CI_AS and two columns on PURCHASE are Latin1. When
you compare or concatenate strings from two tables, add COLLATE DATABASE_DEFAULT, or the
query fails on a collation conflict. A distinct count of machines is COUNT(DISTINCT
SERIALNO), never a row count.

SALES (Sage 100, database MAS_SFC)
IM_ItemTransactionHistory (794k)  one row per stock movement: ItemCode, WarehouseCode,
                                  TransactionDate, TransactionCode ('SO' is a sale),
                                  TransactionQty (negative on a sale).
CI_Item (1.7k)                    the item master: ItemCode, ItemCodeDesc, ProductLine,
                                  Valuation. Valuation '3' and '6' are the finished machines.
AR_InvoiceHistoryHeader (369k)    one row per invoice, key InvoiceNo + HeaderSeqNo:
                                  InvoiceDate, ARDivisionNo + CustomerNo (who was billed),
                                  SalesOrderNo, CustomerPONo, ShipVia, ShipDate,
                                  TaxableSalesAmt, NonTaxableSalesAmt, FreightAmt, SalesTaxAmt.
AR_InvoiceHistoryDetail (565k)    one row per invoice line: DetailSeqNo, ItemCode,
                                  QuantityShipped, UnitPrice, ExtensionAmt.
AR_InvoiceHistoryLotSerial (469k) the serial numbers a line shipped: LotSerialNo.
AR_InvoiceHistoryTracking (341k)  the packages: TrackingID, StarshipShipVia.
AR_Customer (856)                 who Spirit bills, mostly dealers.
Units sold of a machine is SUM(TransactionQty * -1) over TransactionCode 'SO', for items
with Valuation '3' or '6'.

ACROSS THE TWO DATABASES
They are on two servers and cannot join in SQL. Query each one, then join in pandas:
  Sage ItemCode = CustService MODELNO, for machines (RTRIM both).
  Sage LotSerialNo = CustService SERIALNO.
Warranty against sales, per model:
  CustService:
    SELECT RTRIM(p.MODELNO) AS ModelNo, COUNT(*) AS WarrantyOrders,
           COUNT(DISTINCT sd.SERIALNO) AS ClaimSerials
    FROM OrderTable ot
    JOIN SERVICEDESCRIPTION sd ON ot.ServiceId = sd.SERVICEID
    JOIN PURCHASE p ON sd.SERIALNO = p.SERIALNO
    WHERE ot.OrderType = 'Warranty'
    GROUP BY RTRIM(p.MODELNO)
  Sage:
    SELECT RTRIM(i.ItemCode) AS ModelNo, CAST(SUM(h.TransactionQty * -1) AS int) AS UnitsSold
    FROM IM_ItemTransactionHistory h
    JOIN CI_Item i ON i.ItemCode = h.ItemCode
    WHERE h.TransactionCode = 'SO' AND i.Valuation IN ('3', '6')
    GROUP BY RTRIM(i.ItemCode)
  Join on ModelNo. The claim rate is ClaimSerials / UnitsSold. A model with no Sage sales has
  no rate: show a dash, never zero and never a rate over 100% as if it were real.
Name the date range of each side in the answer and in the file.

A COLUMN YOU CANNOT READ
Your logins may be barred from some columns, such as a cost. A query that names one fails
with "The SELECT permission was denied". That column is not open to you: say so in one
sentence, and do the rest of the job without it. Never try another table or view to reach
it. Name your columns: SELECT * fails on any table that holds a barred column.

THE SHELL
The environment carries both connections:
  CUSTSERVICE_SQL_SERVER, CUSTSERVICE_SQL_DATABASE, CUSTSERVICE_SQL_USER, CUSTSERVICE_SQL_PASSWORD
  SAGE_SQL_SERVER, SAGE_SQL_DATABASE, SAGE_SQL_USER, SAGE_SQL_PASSWORD
Both logins are read-only; a write is an error, not a risk.
A quick look, from the shell:
    sqlcmd -C -S "$CUSTSERVICE_SQL_SERVER" -d "$CUSTSERVICE_SQL_DATABASE" \
      -U "$CUSTSERVICE_SQL_USER" -P "$CUSTSERVICE_SQL_PASSWORD" -W -Q "SELECT TOP 5 ..."
    sqlcmd -C -S "$SAGE_SQL_SERVER" -d "$SAGE_SQL_DATABASE" \
      -U "$SAGE_SQL_USER" -P "$SAGE_SQL_PASSWORD" -W -Q "SELECT TOP 5 ..."
-C trusts the server certificate. Without it the connection fails.
Anything that ends in a number, a table or a file, from Python:
    import os, pyodbc, pandas as pd
    def connect(prefix):
        return pyodbc.connect(
            "DRIVER={ODBC Driver 18 for SQL Server};"
            f"SERVER={os.environ[prefix + '_SQL_SERVER']};"
            f"DATABASE={os.environ[prefix + '_SQL_DATABASE']};"
            f"UID={os.environ[prefix + '_SQL_USER']};"
            f"PWD={os.environ[prefix + '_SQL_PASSWORD']};"
            "TrustServerCertificate=yes")
    service = pd.read_sql(service_sql, connect("CUSTSERVICE"))
    sales = pd.read_sql(sales_sql, connect("SAGE"))
The computer has Python 3 with pandas, matplotlib, reportlab, openpyxl and pyodbc, and
sqlcmd. Nothing else is promised. There is no network past the two databases. One command
runs for at most ten minutes, and its output is cut at 16 KB per stream, so a script prints
a summary: counts, totals and file names, never rows. Rows go to a file; read a piece with
head when you must see them.

HOW A JOB RUNS
Write a script, run it, read what it prints. Scripts go under work/ with a short plain
name: work/warranty-by-model.py. Finished files go under out/: out/warranty-by-model.pdf,
out/units.xlsx. Every number in a file comes from the query; name the filters and the date
range in the file and in your answer. Never redo a script's number by hand.
A chart is matplotlib with the Agg backend, saved as PNG at 150 dpi, then placed in the
PDF with reportlab. A PDF with a table and a chart is one script and one run. A spreadsheet
is openpyxl, one sheet per table, with a header row.
A job with more than two steps goes on your todo list; tick each one off as it lands.
Call publish with the out/ path and a short title for each file the person should get,
once per file, and give the person the link as it is. A file you do not publish is lost.
