---
name: analytics
description: >-
  A count, a total, a breakdown, a rank, a trend, a join across tables, a chart, a report, a
  CSV, a spreadsheet, or a PDF. Says which lookup counts in one call, when to go to the shell,
  what the tables are and how they join, what the computer has, and how a finished file
  reaches the person.
---

ONE CALL, OR THE SHELL
Let the database count. A how-many, a total, or a group-by is one call, never your own
arithmetic, and never one call per group.
  count_units        units by Brand, Category, Model, State, Dealer or Year, with any filter,
                     in ONE call. Use it whenever the grouping or a filter is a product fact,
                     because PURCHASE cannot reach the model table any other way. Report
                     TotalUnits and TotalGroups from the answer, never a sum of the rows sent.
  aggregate_records  one table, one function, one group-by. A null group is still a group.
  read_records       a handful of rows from one table. Not a way to count.
  describe_entities  every table, column and join. It costs about 11k tokens: call it only for a
                     column you do not know. The map below covers the rest.
  the shell          everything else: a join, a distinct count, a rank, a change between
                     periods, a chart, a report, a CSV, a spreadsheet, a PDF.
Filter by every fact the person gave you. Counts are whole numbers. A percent has one
decimal place and a ratio two, however many digits the math produced. If a filter matched
nothing, say which one. Report zero rows as zero rows.

THE TABLES
PURCHASE (944k)            one row per registered machine, key SERIALNO. MODELNO, VERSION,
                           DETAMFG (build month, MM/YYYY), PURCHASEDDATE, STATE, DEALERNO,
                           and the owner: PURCHASEDBY, ADDRESS, CITY, ZIP, PHONE, PHONE2, EMAIL.
SERVICEDESCRIPTION (791k)  one row per service call, key SERVICEID. SERIALNO, CALLDATE,
                           SERVICEREP, STATUS. A call has zero or more orders and notes.
OrderTable (697k)          one row per work order. The number people quote is ServiceId-OrderId.
                           OrderDate, ClosedDate, OrderType ('Warranty', 'Part Purchase'...),
                           CaseStatus ('True' is closed, 'False' or null is open), Tech, ISPName,
                           DealerNo. The real key is OrderTableID; a few order numbers carry
                           two rows.
OrderDetail (1.39M)        one row per part line on an order: SP_NO, Desc, Item_Shipped, Return.
OrderDetailBack (2k)       the current backorder queue, same shape. Not history.
PROBLEM (497k)             one free-text note per service call, per ORDERID.
ProblemCode (76)           the problem vocabulary. Reference, not a join.
Spareparts (28.8k)         the parts catalogue and stock, key SP_NO: description, on hand, cost, price.
ModelSP (140k)             the parts list per model version: Model_No, Version, Part_No, Qty.
ModelWarranty (1.3k)       warranty terms per model version in DAYS; 36500 is lifetime.
ModelDetailWarranty (218)  the same as printed for customers, in years; fewer models.
MODEL                      not readable through the lookups (sql_variant columns). In SQL,
                           select its columns by name: MODELNO, VERSION, MODEL, FG (category),
                           Brand, Commercial. Never SELECT * on it.
THE JOIN PATH
  PURCHASE.SERIALNO -> SERVICEDESCRIPTION.SERIALNO -> OrderTable.ServiceId
  OrderTable (ServiceId, OrderId) -> OrderDetail (SERVICEID, OrderID) -> Spareparts.SP_NO
  PURCHASE.MODELNO -> MODEL.MODELNO, ModelSP.Model_No, ModelWarranty.ModelNo
A serial number's first six characters are its MODELNO; PURCHASE.MODELNO holds them as a
column, so filter on it rather than on a prefix. A product name becomes model numbers
through find_model; then filter MODELNO. Warranty vs paid is OrderTable.OrderType;
SERVICEDESCRIPTION.WARRANTY has been stale since 2011. A quarter of service calls have no
work order, so a history counted off OrderTable undercounts. In an OData filter a date is a
full timestamp, 'OrderDate ge 2026-09-08T00:00:00Z'; a bare date is rejected.

THE SHELL
CustService is a SQL Server database. The environment carries the connection:
CUSTSERVICE_SQL_SERVER, CUSTSERVICE_SQL_DATABASE, CUSTSERVICE_SQL_USER,
CUSTSERVICE_SQL_PASSWORD. The login is read-only; a write is an error, not a risk.
Sage 100 (units sold, item transactions) has no path from this computer. If the question
needs it, say so in one sentence and do the CustService part.
A quick look, from the shell:
    sqlcmd -C -S "$CUSTSERVICE_SQL_SERVER" -d "$CUSTSERVICE_SQL_DATABASE" \
      -U "$CUSTSERVICE_SQL_USER" -P "$CUSTSERVICE_SQL_PASSWORD" -W -Q "SELECT TOP 5 ..."
-C trusts the server certificate. Without it the connection fails.
Anything that ends in a number, a table or a file, from Python:
    import os, pyodbc, pandas as pd
    cn = pyodbc.connect(
        "DRIVER={ODBC Driver 18 for SQL Server};"
        f"SERVER={os.environ['CUSTSERVICE_SQL_SERVER']};"
        f"DATABASE={os.environ['CUSTSERVICE_SQL_DATABASE']};"
        f"UID={os.environ['CUSTSERVICE_SQL_USER']};"
        f"PWD={os.environ['CUSTSERVICE_SQL_PASSWORD']};"
        "TrustServerCertificate=yes")
    df = pd.read_sql(sql, cn)
The collation is Chinese_Taiwan_Stroke_CI_AS and two columns on PURCHASE are Latin1. When
you compare or concatenate strings from two tables, add COLLATE DATABASE_DEFAULT, or the
query fails on a collation conflict. A distinct count of machines is COUNT(DISTINCT
SERIALNO), never a row count.
The computer has Python 3 with pandas, matplotlib, reportlab, openpyxl and pyodbc, and
sqlcmd. Nothing else is promised. There is no network past the database. One command runs
for at most ten minutes, and its output is cut at 16 KB per stream, so a script prints a
summary: counts, totals and file names, never rows. Rows go to a file; read a piece with
head when you must see them.

HOW A JOB RUNS
Write a script, run it, read what it prints. Scripts go under work/ with a short plain
name: work/warranty-by-model.py. Finished files go under out/: out/warranty-by-model.pdf,
out/units.csv. Every number in a file comes from the query; name the filters and the date
range in the file and in your answer. Never redo a script's number by hand.
A chart is matplotlib with the Agg backend, saved as PNG at 150 dpi, then placed in the
PDF with reportlab. A PDF with a table and a chart is one script and one run.
A job with more than two steps goes on your todo list; tick each one off as it lands.
Call publish with the out/ path and a short title for each file the person should get,
once per file, and give the person the link as it is. A file you do not publish is lost.
