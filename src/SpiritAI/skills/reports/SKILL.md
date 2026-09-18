---
name: reports
description: >-
  Use this when a question needs a join, a distinct count, a rank, a chart, or a file —
  anything aggregate_records cannot do in one call. It says where the data is, what the
  computer has, and how a finished file reaches the person.
---

WHERE THE DATA IS
CustService is a SQL Server database. The shell's environment carries the connection:
CUSTSERVICE_SQL_SERVER, CUSTSERVICE_SQL_DATABASE, CUSTSERVICE_SQL_USER,
CUSTSERVICE_SQL_PASSWORD. The login is read-only. It cannot write, and a write is an error,
not a risk.
The schema is what describe_entities returns: every table, every column, and the join
hints. Read it there before you write SQL. Entity names are table names.
Sage 100 (units sold, item transactions) has no path from this computer yet. If the
question needs it, say so in one sentence and do the CustService part.

TWO WAYS TO QUERY
From Python, for anything that ends in pandas:
    import os, pyodbc, pandas as pd
    cn = pyodbc.connect(
        "DRIVER={ODBC Driver 18 for SQL Server};"
        f"SERVER={os.environ['CUSTSERVICE_SQL_SERVER']};"
        f"DATABASE={os.environ['CUSTSERVICE_SQL_DATABASE']};"
        f"UID={os.environ['CUSTSERVICE_SQL_USER']};"
        f"PWD={os.environ['CUSTSERVICE_SQL_PASSWORD']};"
        "TrustServerCertificate=yes")
    df = pd.read_sql(sql, cn)
From the shell, for a quick look:
    sqlcmd -C -S "$CUSTSERVICE_SQL_SERVER" -d "$CUSTSERVICE_SQL_DATABASE" \
      -U "$CUSTSERVICE_SQL_USER" -P "$CUSTSERVICE_SQL_PASSWORD" -W -Q "..."
-C trusts the server certificate. Without it the connection fails.

SQL THAT WORKS HERE
The database collation is Chinese_Taiwan_Stroke_CI_AS. When you concatenate or compare
strings from two tables, add COLLATE DATABASE_DEFAULT, or the query fails on a collation
conflict.
MODEL has sql_variant columns. Select the columns you need by name; SELECT * on it fails
in pandas.
SERVICEDESCRIPTION.WARRANTY is stale since 2011. Warranty vs paid is OrderTable.OrderType.

WHAT THE COMPUTER HAS
Python 3 with pandas, matplotlib, reportlab, openpyxl, pyodbc. sqlcmd. Nothing else is
promised. There is no network past the database.
One command runs for at most ten minutes. Each command's output is cut at 16 KB per
stream, so a script prints a summary, never rows. Write rows to a file and read a piece
with head, or with the file read tool, when you must see them.

WHERE FILES GO
Scripts go under work/ with a short plain name: work/warranty-by-model.py. Finished files
go under out/: out/warranty-by-model.pdf, out/units.csv. The Spirit agent and the worker
share this folder, so a script one writes, the other can run.

HOW A FILE REACHES THE PERSON
Only the Spirit agent publishes. It calls publish with the out/ path and a short title.
publish answers with a link; that link goes to the person as it is. Publish each file once.
A file you do not publish is deleted with the call.
Charts: matplotlib with the Agg backend, saved as PNG at 150 dpi, then placed in the PDF
with reportlab. A PDF with a table and a chart is one script and one run.

HOUSE RULES
Every number in the file must come from the query. Never fill a gap with a likely value.
A distinct count of serial numbers is COUNT(DISTINCT SERIALNO), never a row count.
Name the filters you applied and the date range, in the file and in your answer.
