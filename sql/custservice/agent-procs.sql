/*
================================================================================
 Spirit agent procedures on CustService (SPIRITSRV-024)

 Run this file top to bottom with sqlcmd -b, after sql/custservice/logins.sql.
 Every object is CREATE OR ALTER, so the file can run again at any time. The
 core block comes before the procedures that read it.

 Two schemas, both owned by dbo:
   core    building blocks, inline table-valued functions. spiritai_dab holds
           no right on core. A procedure in agent reads them through ownership
           chaining, so only the procedures decide what leaves the database.
   agent   the doors. Each procedure is one ability for one audience and
           returns only the columns that audience may see. spiritai_dab holds
           EXECUTE on this schema and nothing else. DAB exposes these; which
           agent may call which is each agent's tool list in spirit.yaml.

 What is here, in order:
   DROPS                every procedure the agent no longer has
   INDEXES              five seeks the procedures depend on (no-ops once they exist)
   SCHEMAS              core and agent
   core.match_model     product name -> model numbers
   core.unit_warranty   serial -> each warranty term and when it runs out
   core.unit_orders     serial or order number -> orders with tracking and amounts
   core.owner_match     serial + phone or email -> a row when they match the registration
   agent.match_model    core.match_model under its old name, for SpiritReadOnlyUser
   agent.find_model, find_units, get_unit, get_service_history_by_sn,
   get_work_orders, search_parts, count_units      staff and up
   agent.check_unit      serial -> machine and warranty, no owner     everyone
   agent.my_unit         serial + phone or email -> own machine       guest
   agent.dealer_prices   part numbers -> dealer and retail price      dealer and up
   agent.order_status    order or serial -> where it is, no money     dealer

 The DAB entities that expose the agent procedures are in dab/dab-config.json.
 The design notes and measurements behind each procedure are in the local file
 docs/spirit-agent-schema-dba-2.sql (git-ignored) and in
 docs/superpowers/specs/.

 COLLATION. CustService is Chinese_Taiwan_Stroke_CI_AS. PURCHASE.EMAIL and
 PURCHASE.PHONE2 are SQL_Latin1_General_CP1_CI_AS. tempdb is Latin1, so a
 table variable's string column takes Latin1 unless it says otherwise.
 Two rules, and the second one is the one that bites quietly:
   - every string column in a table variable is COLLATE DATABASE_DEFAULT
   - COLLATE goes on the VARIABLE, never on the column. On the column the
     query still returns the right rows and scans all 944k of them.

 ONE RESULT SHAPE PER PROCEDURE. DAB calls sp_describe_first_result_set once
 to learn a procedure's columns. A procedure whose branches project different
 column lists fails that call (Msg 11509) and never registers as a tool. So
 every procedure below ends in one SELECT, and an opt-in column is masked to
 NULL with CASE rather than left out.
================================================================================
*/

USE [CustService];
GO
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO


/*==============================================================================
  DROPS — procedures the agent no longer has


  get_parts_by_sn, get_model_sp   superseded by search_parts (2026-09-08)
  get_model                       superseded by find_model @ModelNo (2026-09-19)
  get_work_orders_by_model        superseded by get_work_orders @Name / @ModelNo (2026-09-19)
  find_units, get_work_orders and the rest are NOT dropped here: CREATE OR
  ALTER below replaces them in place, so the agent is never without a tool.
==============================================================================*/

IF OBJECT_ID(N'agent.get_parts_by_sn', N'P') IS NOT NULL
BEGIN
    DROP PROCEDURE agent.get_parts_by_sn;
    PRINT 'Dropped agent.get_parts_by_sn.';
END
ELSE PRINT 'agent.get_parts_by_sn is already gone.';
GO

IF OBJECT_ID(N'agent.get_model_sp', N'P') IS NOT NULL
BEGIN
    DROP PROCEDURE agent.get_model_sp;
    PRINT 'Dropped agent.get_model_sp.';
END
ELSE PRINT 'agent.get_model_sp is already gone.';
GO

IF OBJECT_ID(N'agent.get_model', N'P') IS NOT NULL
BEGIN
    DROP PROCEDURE agent.get_model;
    PRINT 'Dropped agent.get_model.';
END
ELSE PRINT 'agent.get_model is already gone.';
GO

IF OBJECT_ID(N'agent.get_work_orders_by_model', N'P') IS NOT NULL
BEGIN
    DROP PROCEDURE agent.get_work_orders_by_model;
    PRINT 'Dropped agent.get_work_orders_by_model.';
END
ELSE PRINT 'agent.get_work_orders_by_model is already gone.';
GO


/*==============================================================================
  INDEXES — five seeks the procedures depend on


  IX_SERVICEDESCRIPTION_SERIALNO  serial -> its calls; without it every model-to-
                                  orders and person-to-orders query scans 790k rows
  IX_PURCHASE_MODELNO             model -> its units
  IX_PURCHASE_EMAIL, IX_PURCHASE_PHONE, IX_PURCHASE_PHONE2
                                  a person -> their units. PHONE and PHONE2 both need
                                  one: an OR across an indexed and an unindexed column
                                  still scans.
  Standard Edition: no ONLINE = ON. Each build takes a table lock for its
  duration (PURCHASE 944k rows, SERVICEDESCRIPTION 790k). All five have been
  live since 2026-09-09; the guards make this block a no-op now.
==============================================================================*/

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_SERVICEDESCRIPTION_SERIALNO')
BEGIN
    CREATE NONCLUSTERED INDEX IX_SERVICEDESCRIPTION_SERIALNO
        ON dbo.SERVICEDESCRIPTION (SERIALNO)
        INCLUDE (SERVICEID, CALLDATE, [STATUS], OrderID);
    PRINT 'Created IX_SERVICEDESCRIPTION_SERIALNO.';
END
ELSE PRINT 'IX_SERVICEDESCRIPTION_SERIALNO already exists.';
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_PURCHASE_MODELNO')
BEGIN
    CREATE NONCLUSTERED INDEX IX_PURCHASE_MODELNO
        ON dbo.PURCHASE (MODELNO)
        INCLUDE (SERIALNO, [VERSION], PURCHASEDDATE);
    PRINT 'Created IX_PURCHASE_MODELNO.';
END
ELSE PRINT 'IX_PURCHASE_MODELNO already exists.';
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_PURCHASE_EMAIL')
BEGIN
    CREATE NONCLUSTERED INDEX IX_PURCHASE_EMAIL
        ON dbo.PURCHASE (EMAIL)
        INCLUDE (SERIALNO, MODELNO);
    PRINT 'Created IX_PURCHASE_EMAIL.';
END
ELSE PRINT 'IX_PURCHASE_EMAIL already exists.';
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_PURCHASE_PHONE')
BEGIN
    CREATE NONCLUSTERED INDEX IX_PURCHASE_PHONE
        ON dbo.PURCHASE (PHONE)
        INCLUDE (SERIALNO, MODELNO);
    PRINT 'Created IX_PURCHASE_PHONE.';
END
ELSE PRINT 'IX_PURCHASE_PHONE already exists.';
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_PURCHASE_PHONE2')
BEGIN
    CREATE NONCLUSTERED INDEX IX_PURCHASE_PHONE2
        ON dbo.PURCHASE (PHONE2)
        INCLUDE (SERIALNO, MODELNO);
    PRINT 'Created IX_PURCHASE_PHONE2.';
END
ELSE PRINT 'IX_PURCHASE_PHONE2 already exists.';
GO


/*==============================================================================
  SCHEMAS — core and agent, both owned by dbo

  logins.sql makes them too; this block lets the file run on its own.
==============================================================================*/

IF SCHEMA_ID(N'core') IS NULL  EXEC (N'CREATE SCHEMA core AUTHORIZATION dbo');
IF SCHEMA_ID(N'agent') IS NULL EXEC (N'CREATE SCHEMA agent AUTHORIZATION dbo');
GO


/*==============================================================================
  core.match_model — product name -> model numbers


  "CT900" is a product name. It matches seven MODEL rows and only three are the
  treadmill someone means. The rule:
    normalise   trim; strip a LEADING 'SPIRIT ' or 'SOLE '; remove . space - _ /;
                uppercase
    NoYear      normalise, then drop a trailing four-digit year
    exclude     any MODEL whose name starts with '.', a line item, not a machine
    band A      Norm = term, or NoYear = term   ('CU800 2014' IS 'CU800')
    band B      Norm starts with term           ('CU800ENT' is NOT 'CU800')
    band C      Norm contains term
    return      the single best band, nothing from a worse one
  The strip is LEADING only: 'Sole' is a substring of 'Console', and replacing
  it everywhere turned 'Console - Standard' into 'CONSTANDARD'.
  MODEL holds 1,255 rows, so the scan is cheap and needs no index.
==============================================================================*/

CREATE OR ALTER FUNCTION core.match_model (@Name varchar(100))
RETURNS TABLE
AS
RETURN
(
    WITH Term AS (
        SELECT UPPER(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(
            LTRIM(CASE WHEN LTRIM(RTRIM(ISNULL(@Name, ''))) LIKE 'SPIRIT %'
                       THEN STUFF(LTRIM(RTRIM(ISNULL(@Name, ''))), 1, 7, '')
                       WHEN LTRIM(RTRIM(ISNULL(@Name, ''))) LIKE 'SOLE %'
                       THEN STUFF(LTRIM(RTRIM(ISNULL(@Name, ''))), 1, 5, '')
                       ELSE LTRIM(RTRIM(ISNULL(@Name, ''))) END),
            ' ', ''), '-', ''), '_', ''), '/', ''), '.', '')) AS T
    ),
    Stripped AS (
        SELECT m.[MODELNO],
            LTRIM(CASE WHEN LTRIM(m.[MODEL]) LIKE 'SPIRIT %' THEN STUFF(LTRIM(m.[MODEL]), 1, 7, '')
                       WHEN LTRIM(m.[MODEL]) LIKE 'SOLE %'   THEN STUFF(LTRIM(m.[MODEL]), 1, 5, '')
                       ELSE LTRIM(m.[MODEL]) END) AS Nm
        FROM dbo.[MODEL] AS m
        WHERE LEFT(LTRIM(m.[MODEL]), 1) <> '.'
    ),
    Normalised AS (
        SELECT DISTINCT [MODELNO],
            UPPER(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(
                Nm, ' ', ''), '-', ''), '_', ''), '/', ''), '.', '')) AS Norm
        FROM Stripped
    ),
    Scored AS (
        SELECT n.[MODELNO],
            CASE WHEN n.Norm = t.T THEN 'A'
                 WHEN (CASE WHEN RIGHT(n.Norm, 4) LIKE '[12][09][0-9][0-9]'
                            THEN LEFT(n.Norm, LEN(n.Norm) - 4)
                            ELSE n.Norm END) = t.T THEN 'A'
                 WHEN n.Norm LIKE t.T + '%' THEN 'B'
                 ELSE 'C' END AS Band
        FROM Normalised AS n
        CROSS JOIN Term AS t
        WHERE t.T <> ''
          AND n.Norm LIKE '%' + t.T + '%'
    ),
    Best AS (
        SELECT [MODELNO], MIN(Band) AS Band
        FROM Scored
        GROUP BY [MODELNO]
    )
    SELECT [MODELNO] AS ModelNo, Band
    FROM Best
    WHERE Band = (SELECT MIN(Band) FROM Best)
);
GO
PRINT 'core.match_model is in place.';
GO


/*==============================================================================
  agent.match_model — core.match_model under its old name

  Another application reads agent.match_model as SpiritReadOnlyUser. The agent
  procedures read core.match_model.
==============================================================================*/

CREATE OR ALTER FUNCTION agent.match_model (@Name varchar(100))
RETURNS TABLE
AS
RETURN
(
    SELECT ModelNo, Band FROM core.match_model(@Name)
);
GO
GRANT SELECT ON agent.match_model TO SpiritReadOnlyUser;
GO


/*==============================================================================
  core.unit_warranty — serial -> each warranty term and when it runs out


  One row per term with Days > 0. A serial with no purchase record, or a model
  with no ModelWarranty row, gives no rows; a caller that must still show the
  unit uses OUTER APPLY.
  VERSION. The ModelWarranty row for the unit's PURCHASE.VERSION, else the
  latest version's row, per term. Same rule as UnitLookup.cs for the panel.
  TERMS. LaborPeriod is Labor, Part2Period is Parts, Part1Period is Wear
  parts, Part3Period is Frame; Deck, Motor, Electronics, Console, Brake and
  Flywheel keep their names. The *BBTS1Period columns are empty everywhere
  and not exposed.
  LIFETIME. 36500 days. Lifetime 1, Expires and DaysLeft NULL, InWarranty 1.
  With no purchase date, Expires, InWarranty and DaysLeft are NULL. Today is
  the server's UTC date; the day it expires it is still in. DaysLeft is
  negative once a term has run out.
==============================================================================*/

CREATE OR ALTER FUNCTION core.unit_warranty (@SerialNo varchar(50))
RETURNS TABLE
AS
RETURN
(
    WITH Unit AS (
        SELECT p.[MODELNO] AS ModelNo, p.[VERSION] AS ModelVersion, p.[PURCHASEDDATE] AS PurchasedDate
        FROM dbo.[PURCHASE] AS p
        WHERE p.[SERIALNO] = @SerialNo
    ),
    Terms AS (
        SELECT
            u.PurchasedDate,
            w.[Type] AS WarrantyType,
            t.Term,
            t.Days,
            ROW_NUMBER() OVER (PARTITION BY w.[Type], t.Term
                               ORDER BY CASE WHEN w.[Version] = u.ModelVersion THEN 0 ELSE 1 END,
                                        w.[Version] DESC) AS VersionRank
        FROM Unit AS u
        JOIN dbo.[ModelWarranty] AS w ON w.[ModelNo] = u.ModelNo
        CROSS APPLY (VALUES
            ('Labor',       w.[LaborPeriod]),
            ('Parts',       w.[Part2Period]),
            ('Wear parts',  w.[Part1Period]),
            ('Frame',       w.[Part3Period]),
            ('Deck',        w.[Deck]),
            ('Motor',       w.[Motor]),
            ('Electronics', w.[Electronics]),
            ('Console',     w.[Console]),
            ('Brake',       w.[Brake]),
            ('Flywheel',    w.[Flywheel])) AS t(Term, Days)
        WHERE t.Days > 0
    ),
    Today AS (
        SELECT CAST(SYSUTCDATETIME() AS date) AS D
    )
    SELECT
        t.WarrantyType,
        t.Term,
        t.Days,
        CASE WHEN t.Days >= 36500 THEN 1 ELSE 0 END AS Lifetime,
        CASE WHEN t.Days >= 36500 OR t.PurchasedDate IS NULL THEN NULL
             ELSE DATEADD(day, t.Days, t.PurchasedDate) END AS Expires,
        CASE WHEN t.Days >= 36500 THEN 1
             WHEN t.PurchasedDate IS NULL THEN NULL
             WHEN DATEADD(day, t.Days, t.PurchasedDate) >= d.D THEN 1
             ELSE 0 END AS InWarranty,
        CASE WHEN t.Days >= 36500 OR t.PurchasedDate IS NULL THEN NULL
             ELSE DATEDIFF(day, d.D, DATEADD(day, t.Days, t.PurchasedDate)) END AS DaysLeft
    FROM Terms AS t
    CROSS JOIN Today AS d
    WHERE t.VersionRank = 1
);
GO
PRINT 'core.unit_warranty is in place.';
GO


/*==============================================================================
  core.unit_orders — serial or order number -> orders with tracking and amounts


  By serial: every order on every service call for the serial. By order: the
  order's rows for @ServiceId-@OrderId (an order number can have two rows). A
  caller passes one way in and NULLs for the other, and must add
  OPTION (RECOMPILE) so the either/or filter gets a plan for the way it was
  called.
  Status is Closed when CaseStatus is the string 'True', else Open (a NULL
  status counts as open, as everywhere else).
  Carrier names the FedEx service for Shipment codes 1 to 5, the codes the
  warehouse pages use; any other code gives NULL.
  Freight is varchar on OrderTable; a value that is not a number gives NULL.
  PartsAmount is what the part lines were sold for (OrderDetail.SaleAmount),
  never Spirit's cost.
==============================================================================*/

CREATE OR ALTER FUNCTION core.unit_orders (@SerialNo varchar(50), @ServiceId int, @OrderId int)
RETURNS TABLE
AS
RETURN
(
    SELECT
        o.ServiceId,
        o.OrderId,
        CONVERT(varchar(30), o.ServiceId) + '-' + CONVERT(varchar(30), o.OrderId) AS OrderNumber,
        o.OrderDate,
        o.OrderType,
        CASE WHEN o.CaseStatus = 'True' THEN 'Closed' ELSE 'Open' END AS Status,
        o.Shippeddate AS ShippedDate,
        CASE LTRIM(RTRIM(o.Shipment))
            WHEN '1' THEN 'FedEx Ground'
            WHEN '2' THEN 'FedEx Priority Overnight'
            WHEN '3' THEN 'FedEx Standard Overnight'
            WHEN '4' THEN 'FedEx 2nd Day'
            WHEN '5' THEN 'FedEx, must go today'
        END AS Carrier,
        NULLIF(LTRIM(RTRIM(o.Trackno)), '') AS TrackingNo,
        TRY_CONVERT(decimal(10, 2), NULLIF(LTRIM(RTRIM(o.Freight)), '')) AS Freight,
        o.HandlingFee,
        o.Tax,
        (SELECT SUM(d.SaleAmount) FROM dbo.OrderDetail AS d
          WHERE d.SERVICEID = o.ServiceId AND d.OrderID = o.OrderId) AS PartsAmount
    FROM dbo.OrderTable AS o
    WHERE (@SerialNo IS NOT NULL
           AND o.ServiceId IN (SELECT s.[SERVICEID] FROM dbo.[SERVICEDESCRIPTION] AS s
                               WHERE s.[SERIALNO] = @SerialNo))
       OR (@SerialNo IS NULL AND o.ServiceId = @ServiceId AND o.OrderId = @OrderId)
);
GO
PRINT 'core.unit_orders is in place.';
GO


/*==============================================================================
  core.owner_match — serial + phone or email -> a row when they match


  The proof a stranger gives that a machine is theirs. A phone matches on its
  last ten digits against PURCHASE.PHONE or PHONE2, after both sides lose
  - ( ) space . + /; fewer than ten digits never matches. An email matches
  PURCHASE.EMAIL exactly, case-insensitive, and must look like an email. A ZIP
  code is not proof and is not taken.
  PLACEHOLDERS. Registrations carry fillers, and a filler is not proof. A phone
  whose ten digits are one digit repeated, or are 1234567890 or 0123456789, is
  refused. An email on more than 3 PURCHASE rows is refused (a real owner's
  address is on a few machines at most), and so is one whose part before the @
  is none, na, noemail or no.
  One serial is one PURCHASE seek, so the phone is compared as digits rather
  than through find_units' ten stored formats, which exist for index seeks.
==============================================================================*/

CREATE OR ALTER FUNCTION core.owner_match (@SerialNo varchar(50), @Phone varchar(50), @Email varchar(100))
RETURNS TABLE
AS
RETURN
(
    WITH Given AS (
        SELECT
            REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(
                ISNULL(@Phone, ''), '-', ''), '(', ''), ')', ''), ' ', ''), '.', ''), '+', ''), '/', '') AS PhoneDigits,
            LTRIM(RTRIM(ISNULL(@Email, ''))) AS Mail
    ),
    Candidate AS (
        SELECT
            CASE WHEN LEN(g.PhoneDigits) >= 10 AND g.PhoneDigits NOT LIKE '%[^0-9]%'
                 THEN RIGHT(g.PhoneDigits, 10) END AS Phone10,
            CASE WHEN g.Mail LIKE '_%@_%._%' THEN g.Mail END AS Mail
        FROM Given AS g
    ),
    Wanted AS (
        SELECT
            CASE WHEN c.Phone10 LIKE REPLICATE(LEFT(c.Phone10, 1), 10)
                      OR c.Phone10 IN ('1234567890', '0123456789')
                 THEN NULL ELSE c.Phone10 END AS Phone10,
            CASE WHEN LEFT(c.Mail, CHARINDEX('@', c.Mail) - 1) IN ('none', 'na', 'noemail', 'no')
                      OR (SELECT COUNT(*) FROM dbo.[PURCHASE] AS q
                           WHERE q.[EMAIL] = c.Mail COLLATE SQL_Latin1_General_CP1_CI_AS) > 3
                 THEN NULL ELSE c.Mail END AS Mail
        FROM Candidate AS c
    ),
    Registered AS (
        SELECT
            RIGHT(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(
                ISNULL(p.[PHONE], ''), '-', ''), '(', ''), ')', ''), ' ', ''), '.', ''), '+', ''), '/', ''), 10) AS Phone10,
            RIGHT(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(
                ISNULL(p.[PHONE2], ''), '-', ''), '(', ''), ')', ''), ' ', ''), '.', ''), '+', ''), '/', ''), 10) AS Phone2_10,
            LTRIM(RTRIM(p.[EMAIL])) AS Mail
        FROM dbo.[PURCHASE] AS p
        WHERE p.[SERIALNO] = @SerialNo
    )
    SELECT TOP (1) 1 AS Matched
    FROM Registered AS r
    CROSS JOIN Wanted AS w
    WHERE (w.Phone10 IS NOT NULL AND (r.Phone10 = w.Phone10 OR r.Phone2_10 = w.Phone10))
       OR (w.Mail IS NOT NULL AND r.Mail = w.Mail COLLATE SQL_Latin1_General_CP1_CI_AS)
);
GO
PRINT 'core.owner_match is in place.';
GO


/*==============================================================================
  agent.find_model — product name or model number -> model record


  One row per model number at its latest version. MatchBand says how the name
  matched: A exact (a year suffix allowed), B a prefix, C a substring; only one
  band ever comes back. @ModelNo skips the matcher.
  Carries everything the old get_model returned that an answer needs: Category
  (FG), Sole, FirstProduced (FP_DATE), SnBegin, SnEnd, UpdatedAt. LABOR_W is
  left out: a stale copy of ModelWarranty.LaborPeriod, which get_unit reads.
==============================================================================*/

CREATE OR ALTER PROCEDURE agent.find_model
    @Name    varchar(100) = NULL,   -- product name, for example 'CT900'
    @ModelNo varchar(50)  = NULL,   -- exact model number; skips the matcher
    @Top     int          = 20      -- 1 to 100
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @N varchar(100) = NULLIF(LTRIM(RTRIM(@Name)), '');
    DECLARE @X varchar(50)  = NULLIF(LTRIM(RTRIM(@ModelNo)), '');

    IF (@N IS NULL AND @X IS NULL) OR (@N IS NOT NULL AND @X IS NOT NULL)
        THROW 51050, 'Pass exactly one of @Name and @ModelNo.', 1;

    SET @Top = CASE WHEN @Top IS NULL OR @Top < 1 THEN 20
                    WHEN @Top > 100 THEN 100 ELSE @Top END;

    DECLARE @Match TABLE (
        ModelNo varchar(50) COLLATE DATABASE_DEFAULT NOT NULL PRIMARY KEY,
        Band    char(1)     COLLATE DATABASE_DEFAULT NOT NULL);

    IF @X IS NOT NULL
        INSERT INTO @Match (ModelNo, Band)
        SELECT DISTINCT m.[MODELNO], 'A'
        FROM dbo.[MODEL] AS m
        WHERE m.[MODELNO] = @X;
    ELSE
        INSERT INTO @Match (ModelNo, Band)
        SELECT mm.ModelNo, mm.Band
        FROM core.match_model(@N) AS mm;

    SELECT TOP (@Top)
        v.ModelNo,
        v.LatestVersion,
        m.[MODEL]      AS ModelName,
        m.[DESC]       AS ModelDesc,
        m.[Brand]      AS Brand,
        m.[FG]         AS Category,
        m.[Commercial] AS Commercial,
        m.[SOLE]       AS Sole,
        m.[FP_DATE]    AS FirstProduced,
        m.[SNBegin]    AS SnBegin,
        m.[SNEnd]      AS SnEnd,
        m.[UpdatedAt]  AS UpdatedAt,
        v.Band         AS MatchBand,
        (SELECT COUNT(*) FROM @Match) AS TotalRows
    FROM (
        SELECT mm.ModelNo, mm.Band,
               (SELECT MAX(x.[VERSION]) FROM dbo.[MODEL] AS x WHERE x.[MODELNO] = mm.ModelNo) AS LatestVersion
        FROM @Match AS mm
    ) AS v
    JOIN dbo.[MODEL] AS m
        ON m.[MODELNO] = v.ModelNo AND m.[VERSION] = v.LatestVersion
    ORDER BY v.Band, v.ModelNo
    OPTION (RECOMPILE);
END
GO
PRINT 'agent.find_model is in place.';
GO


/*==============================================================================
  agent.find_units — person -> their machines


  Name, email or phone in; units out, with the contact record on every row so
  a rep can confirm the caller and read back the address on file. @State and
  @ModelNo only narrow: "my F80, my email is x" finds the one of three.
  PHONE. 783k of 839k stored numbers are bare digits; the formatted rest fall
  into six patterns. The parameter is expanded into ten exact forms and each
  one is sought, which reaches 97 percent of stored numbers with an index
  seek. The 6,171 rows shaped ###-###-##### are typing errors and unreachable.
  NAME AND EMAIL are '%term%' scans over all 944k rows (about 2.2 s CPU for
  a common surname), accepted on purpose: a surname is not a prefix of what
  was typed, and full-text was ruled a schema change. Prefer the phone when
  the caller has one. A term under 3 characters is refused.
  COLLATE goes on the @V variable and on the EMAIL/PHONE2 comparison, never
  on PHONE: see the header.
==============================================================================*/

CREATE OR ALTER PROCEDURE agent.find_units
    @Name    varchar(100) = NULL,   -- customer name, partial match
    @Email   varchar(100) = NULL,   -- email, partial match
    @Phone   varchar(50)  = NULL,   -- any common format; expanded and matched exactly
    @State   varchar(10)  = NULL,   -- narrows only; not a search on its own
    @ModelNo varchar(50)  = NULL,   -- narrows only; exact model number
    @Top     int          = 20,     -- 1 to 100
    @Skip    int          = 0
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @N varchar(100) = NULLIF(LTRIM(RTRIM(@Name)),  '');
    DECLARE @E varchar(100) = NULLIF(LTRIM(RTRIM(@Email)), '');
    DECLARE @P varchar(50)  = NULLIF(LTRIM(RTRIM(@Phone)), '');
    DECLARE @St varchar(10) = NULLIF(LTRIM(RTRIM(@State)), '');
    DECLARE @M varchar(50)  = NULLIF(LTRIM(RTRIM(@ModelNo)), '');
    IF @N IS NULL AND @E IS NULL AND @P IS NULL
        THROW 51060, 'Pass at least one of @Name, @Email and @Phone. @State and @ModelNo only narrow.', 1;
    IF (@N IS NOT NULL AND LEN(@N) < 3) OR (@E IS NOT NULL AND LEN(@E) < 3) OR (@P IS NOT NULL AND LEN(@P) < 3)
        THROW 51061, 'A search term must be 3 characters or more.', 1;
    SET @Top  = CASE WHEN @Top IS NULL OR @Top < 1 THEN 20 WHEN @Top > 100 THEN 100 ELSE @Top END;
    SET @Skip = CASE WHEN @Skip IS NULL OR @Skip < 0 THEN 0 ELSE @Skip END;

    DECLARE @V TABLE (v varchar(50) COLLATE DATABASE_DEFAULT NOT NULL PRIMARY KEY);
    IF @P IS NOT NULL
    BEGIN
        DECLARE @D varchar(50) = REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(
            @P,'-',''),'(',''),')',''),' ',''),'.',''),'+',''),'/','');
        IF LEN(@D) = 11 AND LEFT(@D,1) = '1' SET @D = RIGHT(@D,10);
        IF LEN(@D) = 10
            INSERT INTO @V (v) SELECT DISTINCT x FROM (VALUES
                (@D),
                (STUFF(STUFF(@D,7,0,'-'),4,0,'-')),
                (STUFF(STUFF(@D,7,0,'.'),4,0,'.')),
                (STUFF(STUFF(@D,7,0,' '),4,0,' ')),
                ('('+LEFT(@D,3)+') '+SUBSTRING(@D,4,3)+'-'+RIGHT(@D,4)),
                ('('+LEFT(@D,3)+')'+SUBSTRING(@D,4,3)+'-'+RIGHT(@D,4)),
                (LEFT(@D,3)+'/'+RIGHT(@D,7)),
                ('1'+@D),
                ('1-'+STUFF(STUFF(@D,7,0,'-'),4,0,'-')),
                ('+1'+@D)) t(x);
        ELSE
            INSERT INTO @V (v) SELECT DISTINCT x FROM (VALUES (@D),(@P)) t(x) WHERE x <> '';
    END

    DECLARE @NameLike varchar(320) = CASE WHEN @N IS NULL THEN NULL
        ELSE '%' + REPLACE(REPLACE(REPLACE(@N,'[','[[]'),'%','[%]'),'_','[_]') + '%' END;
    DECLARE @MailLike varchar(320) = CASE WHEN @E IS NULL THEN NULL
        ELSE '%' + REPLACE(REPLACE(REPLACE(@E,'[','[[]'),'%','[%]'),'_','[_]') + '%' END;

    ;WITH Units AS (
        SELECT p.SERIALNO, p.MODELNO, p.PURCHASEDDATE, p.RECEIVED,
               p.PURCHASEDBY, p.ADDRESS, p.CITY, p.STATE, p.ZIP,
               p.PHONE, p.PHONE2, p.EMAIL, p.DEALERNO
        FROM dbo.[PURCHASE] p
        WHERE (@NameLike IS NULL OR p.PURCHASEDBY LIKE @NameLike)
          AND (@MailLike IS NULL OR p.EMAIL LIKE @MailLike COLLATE SQL_Latin1_General_CP1_CI_AS)
          AND (@St       IS NULL OR p.STATE = @St)
          AND (@M        IS NULL OR p.MODELNO = @M)
          AND (@P IS NULL OR p.PHONE IN (SELECT v FROM @V)
                          OR p.PHONE2 IN (SELECT v COLLATE SQL_Latin1_General_CP1_CI_AS FROM @V))
    )
    SELECT
        u.SERIALNO AS SerialNo, u.MODELNO AS ModelNo,
        (SELECT TOP 1 m.[MODEL] FROM dbo.[MODEL] m WHERE m.MODELNO = u.MODELNO
          ORDER BY m.[VERSION] DESC) AS ModelName,
        u.PURCHASEDDATE AS PurchasedDate, u.RECEIVED AS SetupDate,
        (SELECT COUNT(DISTINCT CONVERT(varchar(30),o.ServiceId)+'-'+CONVERT(varchar(30),o.OrderId))
         FROM dbo.[SERVICEDESCRIPTION] s JOIN dbo.[OrderTable] o ON o.ServiceId = s.SERVICEID
         /* CaseStatus is varchar(50) NULL; a missing status counts as open,
            matching STEP 4's CaseStatus rendering, which folds NULL into 'OPEN'.
            Plain <> would evaluate UNKNOWN on NULL and silently drop it. */
         WHERE s.SERIALNO = u.SERIALNO AND ISNULL(o.CaseStatus, '') <> 'True') AS OpenOrders,
        (SELECT COUNT(DISTINCT CONVERT(varchar(30),o.ServiceId)+'-'+CONVERT(varchar(30),o.OrderId))
         FROM dbo.[SERVICEDESCRIPTION] s JOIN dbo.[OrderTable] o ON o.ServiceId = s.SERVICEID
         WHERE s.SERIALNO = u.SERIALNO) AS TotalOrders,
        u.PURCHASEDBY AS CustomerName,
        u.ADDRESS     AS Address,
        u.CITY        AS City,
        u.STATE       AS State,
        u.ZIP         AS Zip,
        u.PHONE       AS Phone,
        u.PHONE2      AS Phone2,
        u.EMAIL       AS Email,
        u.DEALERNO    AS DealerNo,
        COUNT(*) OVER () AS TotalRows
    FROM Units u
    ORDER BY u.PURCHASEDDATE DESC, u.SERIALNO
    OFFSET @Skip ROWS FETCH NEXT @Top ROWS ONLY
    OPTION (RECOMPILE);
END
GO
PRINT 'agent.find_units is in place.';
GO


/*==============================================================================
  agent.get_unit — serial -> the machine, its owner, its warranty


  "Is my treadmill under warranty" was three reads and a sum the agent did in
  its head. Now the database does it.
  SHAPE. One row per warranty term, the unit's columns repeated on every row.
  A model with a RES row and a COM row in ModelWarranty gives two sets, told
  apart by WarrantyType; nothing on PURCHASE says which one applies, so the
  caller sees both. A model with no ModelWarranty row gives one row with the
  warranty columns NULL. The terms and their dates come from
  core.unit_warranty.
  Errors: 51090 no serial, 51091 no purchase record for it.
==============================================================================*/

CREATE OR ALTER PROCEDURE agent.get_unit
    @SerialNo varchar(50) = NULL    -- required
AS
BEGIN
    SET NOCOUNT ON;

    IF NULLIF(LTRIM(RTRIM(@SerialNo)), '') IS NULL
        THROW 51090, '@SerialNo is required.', 1;

    SET @SerialNo = LTRIM(RTRIM(@SerialNo));

    IF NOT EXISTS (SELECT 1 FROM dbo.[PURCHASE] WHERE [SERIALNO] = @SerialNo)
        THROW 51091, 'No purchase record exists for that serial number.', 1;

    ;WITH Unit AS (
        SELECT
            p.[SERIALNO]      AS SerialNo,
            p.[MODELNO]       AS ModelNo,
            p.[VERSION]       AS ModelVersion,
            p.[DETAMFG]       AS MfgDate,
            p.[PURCHASEDDATE] AS PurchasedDate,
            p.[RECEIVED]      AS SetupDate,
            p.[PURCHASEDBY]   AS CustomerName,
            p.[ADDRESS]       AS Address,
            p.[CITY]          AS City,
            p.[STATE]         AS State,
            p.[ZIP]           AS Zip,
            p.[PHONE]         AS Phone,
            p.[PHONE2]        AS Phone2,
            p.[EMAIL]         AS Email,
            p.[DEALERNO]      AS DealerNo,
            m.[MODEL]         AS ModelName,
            m.[Brand]         AS Brand,
            m.[FG]            AS Category,
            m.[Commercial]    AS Commercial,
            (SELECT COUNT(*) FROM dbo.[SERVICEDESCRIPTION] s
              WHERE s.[SERIALNO] = p.[SERIALNO]) AS ServiceCalls,
            (SELECT COUNT(DISTINCT CONVERT(varchar(30), o.ServiceId) + '-' + CONVERT(varchar(30), o.OrderId))
               FROM dbo.[SERVICEDESCRIPTION] s JOIN dbo.[OrderTable] o ON o.ServiceId = s.[SERVICEID]
              WHERE s.[SERIALNO] = p.[SERIALNO] AND ISNULL(o.CaseStatus, '') <> 'True') AS OpenOrders,
            (SELECT COUNT(DISTINCT CONVERT(varchar(30), o.ServiceId) + '-' + CONVERT(varchar(30), o.OrderId))
               FROM dbo.[SERVICEDESCRIPTION] s JOIN dbo.[OrderTable] o ON o.ServiceId = s.[SERVICEID]
              WHERE s.[SERIALNO] = p.[SERIALNO]) AS TotalOrders
        FROM dbo.[PURCHASE] AS p
        OUTER APPLY (
            SELECT TOP 1 m.[MODEL], m.[Brand], m.[FG], m.[Commercial]
            FROM dbo.[MODEL] AS m
            WHERE m.[MODELNO] = p.[MODELNO]
            ORDER BY CASE WHEN m.[VERSION] = p.[VERSION] THEN 0 ELSE 1 END, m.[VERSION] DESC
        ) AS m
        WHERE p.[SERIALNO] = @SerialNo
    )
    SELECT
        u.SerialNo, u.ModelNo, u.ModelName, u.Brand, u.Category, u.Commercial, u.ModelVersion,
        u.MfgDate, u.PurchasedDate, u.SetupDate,
        u.CustomerName, u.Address, u.City, u.State, u.Zip, u.Phone, u.Phone2, u.Email, u.DealerNo,
        u.ServiceCalls, u.OpenOrders, u.TotalOrders,
        t.WarrantyType,
        t.Term,
        t.Days,
        t.Lifetime,
        t.Expires,
        t.InWarranty,
        COUNT(*) OVER () AS TotalRows
    FROM Unit AS u
    OUTER APPLY core.unit_warranty(u.SerialNo) AS t
    ORDER BY t.WarrantyType, t.Days DESC, t.Term
    OPTION (RECOMPILE);
END
GO
PRINT 'agent.get_unit is in place.';
GO


/*==============================================================================
  agent.get_service_history_by_sn — serial -> its service calls


  One row per service call, with the product facts, the owner's contact
  record and the proof-of-purchase flag repeated on each. Kept beside
  get_unit and get_work_orders because 195,407 of 790,983 calls (25 percent)
  have no work order under them: a history read off OrderTable would lose a
  quarter of what happened to a machine. UnitLookup.cs reads it for the
  /v1/units panel.
  DISTINCT is deliberate: the Problem and OrderTable joins can each match
  more than one row per call. TotalRows counts rows after DISTINCT and
  before paging, so it can exceed the number of calls.
  @IncludeDetail = 0 cuts Description and Solution to 200 characters and
  masks SetupBy to NULL. A unit never called about still returns one row.
  Error 51041 if no purchase record carries the serial.
==============================================================================*/

CREATE OR ALTER PROCEDURE agent.get_service_history_by_sn
    @SerialNo      varchar(50) = NULL,   -- required
    @Top           int         = 20,     -- 1 to 100
    @Skip          int         = 0,
    @IncludeDetail bit         = 0       -- unmasks SetupBy and stops truncating Description/Solution
AS
BEGIN
    SET NOCOUNT ON;

    IF NULLIF(LTRIM(RTRIM(@SerialNo)), '') IS NULL
        THROW 51040, '@SerialNo is required.', 1;

    SET @SerialNo = LTRIM(RTRIM(@SerialNo));

    IF NOT EXISTS (SELECT 1 FROM dbo.[PURCHASE] WHERE [SERIALNO] = @SerialNo)
        THROW 51041, 'No purchase record exists for that serial number.', 1;

    SET @Top  = CASE WHEN @Top IS NULL OR @Top < 1 THEN 20
                     WHEN @Top > 100 THEN 100 ELSE @Top END;
    SET @Skip = CASE WHEN @Skip IS NULL OR @Skip < 0 THEN 0 ELSE @Skip END;

    /* DISTINCT is deliberate. The Problem and OrderTable joins can each match
       more than one row per service call, and the C# uses DISTINCT for the
       same reason. TotalRows is COUNT(*) OVER () computed on Hist, i.e. after
       DISTINCT and before OFFSET/FETCH pages it, so it counts the same rows
       this procedure would return without a @Top.

       @IncludeDetail only masks SetupBy's value to NULL and switches whether
       Description/Solution are truncated; it must never turn SetupBy into a
       conditionally-omitted column or send this procedure down a branch that
       returns a different column list. DAB calls sp_describe_first_result_set
       once to learn this procedure's shape, and a stored procedure whose
       branches project different result shapes fails that call outright with
       Msg 11509 ("...is not compatible with the statement..."), which would
       keep this procedure from registering as a tool at all. So this is one
       CTE and one SELECT, always. */
    ;WITH Hist AS (
        SELECT DISTINCT
            @SerialNo            AS SerialNo,
            /* product */
            m.[MODELNO]          AS ModelNo,
            m.[MODEL]            AS ModelName,
            m.[Sole]             AS Sole,
            m.[FG]               AS FG,
            p.[VERSION]          AS ModelVersion,
            p.[DETAMFG]          AS MfgDate,
            p.[PURCHASEDDATE]    AS PurchasedDate,
            p.[RECEIVED]         AS SetupDate,
            CASE WHEN @IncludeDetail = 1 THEN p.[INPUTBY] END AS SetupBy,
            /* owner */
            p.[PURCHASEDBY]      AS CustomerName,
            p.[ADDRESS]          AS Address,
            p.[CITY]             AS City,
            p.[STATE]            AS State,
            p.[ZIP]              AS Zip,
            p.[PHONE]            AS Phone,
            p.[PHONE2]           AS Phone2,
            p.[EMAIL]            AS Email,
            p.[DEALERNO]         AS DealerNo,
            CASE WHEN pop.[SERIALNO] IS NOT NULL THEN 1 ELSE 0 END AS IsPopConfirming,
            /* service history */
            s.[SERVICEID]        AS ServiceId,
            s.[CALLDATE]         AS CallDate,
            s.[BYUSER]           AS ByUser,
            s.[SERVICEDATE]      AS ServiceDate,
            s.[SERVICEREP]       AS ServiceRep,
            CASE WHEN @IncludeDetail = 1 THEN s.[DESCRIPTION]
                 WHEN LEN(s.[DESCRIPTION]) > 200 THEN LEFT(s.[DESCRIPTION], 200) + '...'
                 ELSE s.[DESCRIPTION]
            END                  AS [Description],
            CASE WHEN @IncludeDetail = 1 THEN s.[SOLUTION]
                 WHEN LEN(s.[SOLUTION]) > 200 THEN LEFT(s.[SOLUTION], 200) + '...'
                 ELSE s.[SOLUTION]
            END                  AS Solution,
            /* The first branch is NOT in the C#. It is needed here because this
               procedure LEFT JOINs the history, so a unit with no service calls
               still returns one row. Without it, the s.Assistant IS NULL branch
               reports 'PENDING' for a unit that was never called about. */
            CASE WHEN s.[SERVICEID] IS NULL THEN NULL
                 WHEN s.[STATUS] = '1'      THEN 'CLOSED'
                 WHEN s.[Assistant] IS NULL THEN 'PENDING'
                 WHEN s.[STATUS] = '0'      THEN 'OPEN'
            END                  AS ServiceStatus,
            s.[ZenTicketId]      AS ZenTicketId,
            COALESCE(pr.[OrderId], ot.[OrderId]) AS OrderId,
            CASE WHEN ot.[CaseStatus] IS NULL     THEN NULL
                 WHEN ot.[CaseStatus] = 'True'    THEN 'CLOSED'
                 ELSE 'OPEN'
            END                  AS CaseStatus
        FROM dbo.[PURCHASE] AS p
        JOIN dbo.[MODEL] AS m
            ON m.[MODELNO] = p.[MODELNO] AND m.[VERSION] = p.[VERSION]
        LEFT JOIN dbo.[POP] AS pop
            ON pop.[SERIALNO] = p.[SERIALNO]
           AND pop.[Status]   = 1
           AND pop.[Scanfile] IS NULL
        LEFT JOIN dbo.[SERVICEDESCRIPTION] AS s
            ON s.[SERIALNO] = p.[SERIALNO]
        LEFT JOIN dbo.[Problem] AS pr
            ON pr.[ServiceId] = s.[SERVICEID]
        LEFT JOIN dbo.[OrderTable] AS ot
            ON ot.[ServiceId] = s.[SERVICEID]
        WHERE p.[SERIALNO] = @SerialNo
    )
    SELECT
        h.SerialNo, h.ModelNo, h.ModelName, h.Sole, h.FG, h.ModelVersion,
        h.MfgDate, h.PurchasedDate, h.SetupDate, h.SetupBy, h.IsPopConfirming,
        h.CustomerName, h.Address, h.City, h.State, h.Zip, h.Phone, h.Phone2,
        h.Email, h.DealerNo,
        h.ServiceId, h.CallDate, h.ByUser, h.ServiceDate, h.ServiceRep,
        h.[Description], h.Solution, h.ServiceStatus, h.ZenTicketId,
        h.OrderId, h.CaseStatus,
        COUNT(*) OVER () AS TotalRows
    FROM Hist AS h
    ORDER BY h.CallDate DESC, h.ServiceId
    OFFSET @Skip ROWS FETCH NEXT @Top ROWS ONLY
    OPTION (RECOMPILE);
END
GO
PRINT 'agent.get_service_history_by_sn is in place.';
GO


/*==============================================================================
  agent.get_work_orders — work orders, by any handle


  Ways in: order numbers, one serial number, a product name or model number
  (through match_model), a date range, status, ISP, tech, dealer, order type.
  At least one filter is required (THROW 51001): it is the only thing between
  the agent and a scan of 697k rows. Every filter is on that guard; @Skip,
  @IncludeParts and @IncludeDetail are not filters and must never satisfy it.
  The serial and model ways in are IN over SERVICEDESCRIPTION, which seeks
  the indexes and never fans an order out. SerialNo, ModelNo and ModelName
  ride on every row and are looked up AFTER the page is cut, on twenty rows:
  doing it per candidate row turned 0.5 s into 9 s on @Name = 'CT900'.
  OrderTable's real key is OrderTableID; a few order numbers carry two rows.
  CaseStatus is the string 'True' (closed) or 'False'/NULL (open), not a bit.
  @IncludeParts joins OrderDetail (shipped) and OrderDetailBack (on order)
  as one row per part line; @IncludeDetail unmasks Notes, Feedback and fees.
  TotalRows counts orders before paging, never part lines.
  Errors: 51001 no filter, 51002 bad status, 51003 no valid order number,
  51004 both @Name and @ModelNo, 51005 no model matches.
==============================================================================*/

CREATE OR ALTER PROCEDURE agent.get_work_orders
    @OrderNumbers      nvarchar(max) = NULL,   -- comma separated 'ServiceId-OrderId', max 100
    @SerialNo          varchar(50)   = NULL,   -- exact serial number
    @Name              varchar(100)  = NULL,   -- product name, for example 'CT900'; not with @ModelNo
    @ModelNo           varchar(50)   = NULL,   -- exact model number; not with @Name
    @OrderDateFrom     datetime      = NULL,   -- inclusive
    @OrderDateTo       datetime      = NULL,   -- inclusive
    @CaseStatus        nvarchar(10)  = NULL,   -- 'Pending' or 'Closed'
    @ISPName           varchar(100)  = NULL,   -- partial match
    @Tech              varchar(50)   = NULL,   -- partial match
    @DealerNo          varchar(50)   = NULL,   -- partial match
    @OrderType         varchar(100)  = NULL,   -- partial match, e.g. 'Warranty', 'Part Purchase'
    @Top               int           = 20,     -- 1 to 100
    @Skip              int           = 0,
    @IncludeParts      bit           = 0,      -- also join part lines: PartNo, PartDesc, ItemShipped, IsReturned, Source
    @IncludeDetail     bit           = 0       -- also include Notes, Feedback, ISPStatus, Freight, LaborFee, TripFee, OrderTableID
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @S varchar(50)  = NULLIF(LTRIM(RTRIM(@SerialNo)), '');
    DECLARE @N varchar(100) = NULLIF(LTRIM(RTRIM(@Name)), '');
    DECLARE @X varchar(50)  = NULLIF(LTRIM(RTRIM(@ModelNo)), '');

    IF @N IS NOT NULL AND @X IS NOT NULL
        THROW 51004, 'Pass @Name or @ModelNo, not both.', 1;

    /* OrderTable.CaseStatus stores the strings 'True' (closed) and 'False'
       (pending), not a bit. Translate before comparing. */
    DECLARE @CaseStatusDb varchar(50) = NULL;
    IF @CaseStatus IS NOT NULL AND LTRIM(RTRIM(@CaseStatus)) <> ''
    BEGIN
        SET @CaseStatusDb =
            CASE LOWER(LTRIM(RTRIM(@CaseStatus)))
                WHEN 'closed'  THEN 'True'
                WHEN 'pending' THEN 'False'
                ELSE NULL
            END;
        IF @CaseStatusDb IS NULL
            THROW 51002, '@CaseStatus must be ''Pending'' or ''Closed''.', 1;
    END

    DECLARE @Keys TABLE (ServiceId int NOT NULL, OrderId int NOT NULL,
                         PRIMARY KEY (ServiceId, OrderId));

    IF @OrderNumbers IS NOT NULL AND LTRIM(RTRIM(@OrderNumbers)) <> ''
    BEGIN
        /* Split and discard malformed entries in a separate statement first.
           Doing the split and the LEFT/SUBSTRING in one query lets the engine
           run LEFT(v, -1) on an entry with no hyphen, which raises a confusing
           "Invalid length parameter" error instead of the message below. */
        DECLARE @Parts TABLE (v nvarchar(200) COLLATE DATABASE_DEFAULT NOT NULL, HyphenPos int NOT NULL);

        INSERT INTO @Parts (v, HyphenPos)
        SELECT s.v, CHARINDEX(N'-', s.v)
        FROM (SELECT LTRIM(RTRIM(value)) AS v
              FROM STRING_SPLIT(@OrderNumbers, N',')) s
        WHERE s.v <> N'';

        DELETE FROM @Parts WHERE HyphenPos <= 1;

        INSERT INTO @Keys (ServiceId, OrderId)
        SELECT DISTINCT TOP (100) x.ServiceId, x.OrderId
        FROM (
            SELECT
                TRY_CONVERT(int, LEFT(v, HyphenPos - 1))          AS ServiceId,
                TRY_CONVERT(int, SUBSTRING(v, HyphenPos + 1, 50)) AS OrderId
            FROM @Parts
        ) x
        WHERE x.ServiceId IS NOT NULL AND x.OrderId IS NOT NULL;

        IF NOT EXISTS (SELECT 1 FROM @Keys)
            THROW 51003, '@OrderNumbers had no valid entries. Expected ''ServiceId-OrderId'', for example ''845435-1''.', 1;
    END

    /* A product name or model number becomes a set of model numbers, the same
       way find_model and count_units do it. An empty set is an error here, not
       an empty page: "no work orders for the CT9000" would read as a fact. */
    DECLARE @Match TABLE (ModelNo varchar(50) COLLATE DATABASE_DEFAULT NOT NULL PRIMARY KEY);
    IF @X IS NOT NULL
        INSERT INTO @Match (ModelNo)
        SELECT DISTINCT m.[MODELNO] FROM dbo.[MODEL] AS m WHERE m.[MODELNO] = @X;
    ELSE IF @N IS NOT NULL
        INSERT INTO @Match (ModelNo)
        SELECT mm.ModelNo FROM core.match_model(@N) AS mm;
    IF (@X IS NOT NULL OR @N IS NOT NULL) AND NOT EXISTS (SELECT 1 FROM @Match)
        THROW 51005, 'No model matches that name or model number.', 1;
    DECLARE @HasModel bit = CASE WHEN EXISTS (SELECT 1 FROM @Match) THEN 1 ELSE 0 END;

    /* Guard against an unfiltered full-table scan of production.
       Every parameter must appear here. A new filter that is left out of this
       list silently re-opens the full-table scan. @Skip, @IncludeParts and
       @IncludeDetail are not filters and are deliberately excluded: paging
       and shape flags must never satisfy this guard. */
    IF NOT EXISTS (SELECT 1 FROM @Keys)
       AND @S IS NULL
       AND @HasModel = 0
       AND @OrderDateFrom   IS NULL AND @OrderDateTo   IS NULL
       AND @CaseStatusDb IS NULL
       AND NULLIF(LTRIM(RTRIM(@ISPName  )), '') IS NULL
       AND NULLIF(LTRIM(RTRIM(@Tech     )), '') IS NULL
       AND NULLIF(LTRIM(RTRIM(@DealerNo )), '') IS NULL
       AND NULLIF(LTRIM(RTRIM(@OrderType)), '') IS NULL
        THROW 51001, 'At least one filter is required.', 1;

    SET @Top  = CASE WHEN @Top IS NULL OR @Top < 1 THEN 20
                     WHEN @Top > 100 THEN 100
                     ELSE @Top END;
    SET @Skip = CASE WHEN @Skip IS NULL OR @Skip < 0 THEN 0 ELSE @Skip END;

    /* Same LIKE escaping the C# does, so a part number containing _ or %
       is matched literally. */
    DECLARE @ISPNameLike   varchar(320) = CASE WHEN NULLIF(LTRIM(RTRIM(@ISPName  )), '') IS NULL THEN NULL
        ELSE '%' + REPLACE(REPLACE(REPLACE(@ISPName  , '[', '[[]'), '%', '[%]'), '_', '[_]') + '%' END;
    DECLARE @TechLike      varchar(320) = CASE WHEN NULLIF(LTRIM(RTRIM(@Tech     )), '') IS NULL THEN NULL
        ELSE '%' + REPLACE(REPLACE(REPLACE(@Tech     , '[', '[[]'), '%', '[%]'), '_', '[_]') + '%' END;
    DECLARE @DealerNoLike  varchar(320) = CASE WHEN NULLIF(LTRIM(RTRIM(@DealerNo )), '') IS NULL THEN NULL
        ELSE '%' + REPLACE(REPLACE(REPLACE(@DealerNo , '[', '[[]'), '%', '[%]'), '_', '[_]') + '%' END;
    DECLARE @OrderTypeLike varchar(320) = CASE WHEN NULLIF(LTRIM(RTRIM(@OrderType)), '') IS NULL THEN NULL
        ELSE '%' + REPLACE(REPLACE(REPLACE(@OrderType, '[', '[[]'), '%', '[%]'), '_', '[_]') + '%' END;

    DECLARE @HasKeys bit = CASE WHEN EXISTS (SELECT 1 FROM @Keys) THEN 1 ELSE 0 END;

    /* Non-unique index, not a primary key: OrderTable is keyed on OrderTableID,
       so ServiceId + OrderId is not guaranteed unique. */
    DECLARE @Ord TABLE (
        OrderTableID int           NOT NULL,
        ServiceId    int           NOT NULL,
        OrderId      int           NOT NULL,
        Notes        varchar(500)  COLLATE DATABASE_DEFAULT NULL,
        Feedback     varchar(2000) COLLATE DATABASE_DEFAULT NULL,
        ISPName      varchar(100)  COLLATE DATABASE_DEFAULT NULL,
        LaborFee     varchar(50)   COLLATE DATABASE_DEFAULT NULL,
        TripFee      varchar(50)   COLLATE DATABASE_DEFAULT NULL,
        OrderDate    datetime      NULL,
        AppointDate  datetime      NULL,
        Shippeddate  datetime      NULL,
        ClosedDate   datetime      NULL,
        Trackno      varchar(50)   COLLATE DATABASE_DEFAULT NULL,
        Freight      varchar(50)   COLLATE DATABASE_DEFAULT NULL,
        CaseStatus   varchar(50)   COLLATE DATABASE_DEFAULT NULL,
        Tech         varchar(50)   COLLATE DATABASE_DEFAULT NULL,
        DealerNo     varchar(50)   COLLATE DATABASE_DEFAULT NULL,
        OrderType    varchar(100)  COLLATE DATABASE_DEFAULT NULL,
        ISPStatus    varchar(100)  COLLATE DATABASE_DEFAULT NULL,
        Locked       bit           NULL,
        TotalRows    int           NOT NULL,
        INDEX IX_Ord NONCLUSTERED (ServiceId, OrderId)
    );

    /* TotalRows is COUNT(*) OVER () on this unpaged, filtered set of orders,
       evaluated before OFFSET/FETCH slices it down to the page. It counts
       orders, never part lines: the parts join happens only in the final
       SELECT below, against the page already captured here. */
    INSERT INTO @Ord
    SELECT
        o.OrderTableID, o.ServiceId, o.OrderId, o.Notes, o.Feedback, o.ISPName,
        o.LaborFee, o.TripFee, o.OrderDate, o.AppointDate, o.Shippeddate,
        o.ClosedDate, o.Trackno, o.Freight, o.CaseStatus, o.Tech, o.DealerNo,
        o.OrderType, o.ISPStatus, o.Locked,
        COUNT(*) OVER () AS TotalRows
    FROM dbo.OrderTable AS o
    WHERE (@HasKeys = 0
           OR EXISTS (SELECT 1 FROM @Keys k
                      WHERE k.ServiceId = o.ServiceId AND k.OrderId = o.OrderId))
      AND (@S IS NULL
           OR o.ServiceId IN (SELECT s.[SERVICEID] FROM dbo.[SERVICEDESCRIPTION] AS s
                              WHERE s.[SERIALNO] = @S))
      AND (@HasModel = 0
           OR o.ServiceId IN (SELECT s.[SERVICEID]
                              FROM @Match AS mm
                              JOIN dbo.[PURCHASE] AS p ON p.[MODELNO] = mm.ModelNo
                              JOIN dbo.[SERVICEDESCRIPTION] AS s ON s.[SERIALNO] = p.[SERIALNO]))
      AND (@OrderDateFrom   IS NULL OR o.OrderDate   >= @OrderDateFrom)
      AND (@OrderDateTo     IS NULL OR o.OrderDate   <= @OrderDateTo)
      AND (@CaseStatusDb    IS NULL OR o.CaseStatus   = @CaseStatusDb)
      AND (@ISPNameLike     IS NULL OR o.ISPName   LIKE @ISPNameLike)
      AND (@TechLike        IS NULL OR o.Tech      LIKE @TechLike)
      AND (@DealerNoLike    IS NULL OR o.DealerNo  LIKE @DealerNoLike)
      AND (@OrderTypeLike   IS NULL OR o.OrderType LIKE @OrderTypeLike)
    ORDER BY o.OrderDate DESC, o.OrderTableID
    OFFSET @Skip ROWS FETCH NEXT @Top ROWS ONLY
    OPTION (RECOMPILE);

    /* @IncludeDetail and @IncludeParts only mask column VALUES to NULL; they
       must never turn into conditionally-omitted columns or a branch that
       returns a different column list. DAB calls sp_describe_first_result_set
       once to learn this procedure's shape, and a stored procedure whose
       branches project different result shapes fails that call outright with
       Msg 11509 ("...is not compatible with the statement..."), which would
       keep this procedure from registering as a tool at all. So this is one
       SELECT, always: the seven @IncludeDetail columns are masked with CASE,
       and the five part columns come from a LEFT JOIN gated by
       "AND @IncludeParts = 1" in the ON clause rather than a second SELECT --
       with OPTION (RECOMPILE) the optimizer resolves that predicate at
       compile time and skips reading OrderDetail/OrderDetailBack when the
       flag is 0, without changing the shape of what comes back.

       Source marks where a part line came from: 'OrderDetail' is shipped,
       'OrderDetailBack' is back-ordered. Orders with no lines, or with
       @IncludeParts = 0, still come back, with the part columns NULL.

       SERVICEDESCRIPTION.SERVICEID is unique (790,983 rows, 790,983 distinct,
       measured 2026-09-19), so each OUTER APPLY returns at most one row and
       cannot fan an order out. */
    SELECT
        o.ServiceId,
        o.OrderId,
        CONVERT(varchar(30), o.ServiceId) + '-' + CONVERT(varchar(30), o.OrderId) AS OrderNumber,
        sd.SERIALNO AS SerialNo,
        pu.MODELNO  AS ModelNo,
        (SELECT TOP 1 m.[MODEL] FROM dbo.[MODEL] AS m
          WHERE m.[MODELNO] = pu.MODELNO ORDER BY m.[VERSION] DESC) AS ModelName,
        CASE WHEN @IncludeDetail = 1 THEN o.OrderTableID END AS OrderTableID,
        o.OrderDate,
        o.AppointDate,
        o.Shippeddate,
        o.ClosedDate,
        o.CaseStatus,
        o.OrderType,
        o.Locked,
        o.ISPName,
        CASE WHEN @IncludeDetail = 1 THEN o.ISPStatus END AS ISPStatus,
        o.Tech,
        o.DealerNo,
        o.Trackno,
        CASE WHEN @IncludeDetail = 1 THEN o.Freight  END AS Freight,
        CASE WHEN @IncludeDetail = 1 THEN o.LaborFee END AS LaborFee,
        CASE WHEN @IncludeDetail = 1 THEN o.TripFee  END AS TripFee,
        CASE WHEN @IncludeDetail = 1 THEN o.Notes    END AS Notes,
        CASE WHEN @IncludeDetail = 1 THEN o.Feedback END AS Feedback,
        d.SP_NO           AS PartNo,
        d.PartDesc,
        d.Item_Shipped    AS ItemShipped,
        d.IsReturned,
        d.Source,
        o.TotalRows
    FROM @Ord AS o
    OUTER APPLY (SELECT TOP 1 s.[SERIALNO] FROM dbo.[SERVICEDESCRIPTION] AS s
                 WHERE s.[SERVICEID] = o.ServiceId) AS sd
    OUTER APPLY (SELECT TOP 1 p.[MODELNO] FROM dbo.[PURCHASE] AS p
                 WHERE p.[SERIALNO] = sd.SERIALNO) AS pu
    LEFT JOIN (
        SELECT SERVICEID AS ServiceId, OrderID AS OrderId, SP_NO,
               [Desc] AS PartDesc, Item_Shipped, [Return] AS IsReturned,
               'OrderDetail' AS Source
        FROM dbo.OrderDetail
        UNION ALL
        SELECT SERVICEID AS ServiceId, OrderID AS OrderId, SP_NO,
               [Desc] AS PartDesc, Item_Shipped, [Return] AS IsReturned,
               'OrderDetailBack' AS Source
        FROM dbo.OrderDetailBack
    ) AS d
        ON d.ServiceId = o.ServiceId AND d.OrderId = o.OrderId AND @IncludeParts = 1
    ORDER BY o.OrderDate DESC, o.ServiceId, o.OrderId, d.Source, d.SP_NO
    OPTION (RECOMPILE);
END
GO
PRINT 'agent.get_work_orders is in place.';
GO


/*==============================================================================
  agent.search_parts — the parts list for one machine


  One way into a parts list, whichever identifier the person has: a serial
  number, a model number, or a product name. @Search matches the description,
  the spare part number and the Dyaco drawing number, with LIKE escaping so a
  part number holding _ or % matches literally.
  ModelSP averages 150 lines per model version and reaches 421, so the default
  is 20 rows and the cap 100; TotalRows says how many there are.
  A name matching several models does NOT merge their lists: the lowest model
  number is used and the rest are named in OtherModelNos. A part number that
  differs between two versions of a machine is exactly the mistake to avoid.
  The serial rule: characters 11 onward are the sequence, checked against
  MODEL.SNBegin/SNEnd to pick the version (51071, 51072 on failure).
  RetailPrice is Spareparts.RETAIL_PRICE, the list price a customer pays.
==============================================================================*/

CREATE OR ALTER PROCEDURE agent.search_parts
    @SerialNo      varchar(50)  = NULL,   -- exact serial number
    @ModelNo       varchar(50)  = NULL,   -- exact model number
    @Name          varchar(100) = NULL,   -- product name, for example 'CT900'
    @Version       int          = NULL,   -- NULL means latest version; ignored with @SerialNo
    @Search        varchar(100) = NULL,   -- matches Description, SpNo and DyacoNo
    @Top           int          = 20,     -- 1 to 100
    @Skip          int          = 0,
    @IncludeDetail bit          = 0
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Sn varchar(50)  = NULLIF(LTRIM(RTRIM(@SerialNo)), '');
    DECLARE @Mn varchar(50)  = NULLIF(LTRIM(RTRIM(@ModelNo)),  '');
    DECLARE @Nm varchar(100) = NULLIF(LTRIM(RTRIM(@Name)),     '');
    IF (CASE WHEN @Sn IS NULL THEN 0 ELSE 1 END
      + CASE WHEN @Mn IS NULL THEN 0 ELSE 1 END
      + CASE WHEN @Nm IS NULL THEN 0 ELSE 1 END) <> 1
        THROW 51070, 'Pass exactly one of @SerialNo, @ModelNo and @Name.', 1;
    SET @Top  = CASE WHEN @Top IS NULL OR @Top < 1 THEN 20 WHEN @Top > 100 THEN 100 ELSE @Top END;
    SET @Skip = CASE WHEN @Skip IS NULL OR @Skip < 0 THEN 0 ELSE @Skip END;

    DECLARE @Model varchar(50) = NULL, @V int = @Version, @Other varchar(500) = NULL;

    IF @Sn IS NOT NULL
    BEGIN
        SET @Model = SUBSTRING(@Sn, 1, 6);
        DECLARE @Seq int = TRY_CONVERT(int, SUBSTRING(@Sn, 11, 16));
        IF @Seq IS NULL
            THROW 51071, 'Serial number is malformed. Characters 11 onward must be a number.', 1;
        /* @V may already hold a caller-supplied @Version at this point. A
           SELECT TOP (1) @V = ... that matches zero rows leaves @V exactly as
           it was -- it does not set it to NULL -- so without this reset a
           serial number no model version covers would silently fall through
           to whatever @Version the caller passed, instead of throwing 51072. */
        SET @V = NULL;
        SELECT TOP (1) @V = m.[VERSION] FROM dbo.[MODEL] AS m
        WHERE m.[MODELNO] = @Model AND @Seq BETWEEN m.[SNBegin] AND COALESCE(m.[SNEnd], 999999)
        ORDER BY m.[VERSION] DESC;
        IF @V IS NULL THROW 51072, 'No model version covers that serial number.', 1;
    END
    ELSE IF @Mn IS NOT NULL SET @Model = @Mn;
    ELSE
    BEGIN
        DECLARE @Mt TABLE (ModelNo varchar(50) COLLATE DATABASE_DEFAULT NOT NULL PRIMARY KEY);
        INSERT INTO @Mt (ModelNo)
        SELECT mm.ModelNo FROM core.match_model(@Nm) AS mm;
        SELECT @Model = MIN(ModelNo) FROM @Mt;
        IF @Model IS NULL THROW 51073, 'No model matches that name.', 1;
        SELECT @Other = STRING_AGG(ModelNo, ',') FROM @Mt WHERE ModelNo <> @Model;
    END

    IF @V IS NULL SELECT @V = MAX(m.[VERSION]) FROM dbo.[MODEL] AS m WHERE m.[MODELNO] = @Model;
    IF @V IS NULL THROW 51074, 'No model exists with that model number.', 1;

    DECLARE @Like varchar(320) = CASE WHEN NULLIF(LTRIM(RTRIM(@Search)), '') IS NULL THEN NULL
        ELSE '%' + REPLACE(REPLACE(REPLACE(LTRIM(RTRIM(@Search)), '[', '[[]'), '%', '[%]'), '_', '[_]') + '%' END;

    ;WITH Lines AS (
        SELECT sp.[Model_No], sp.[Version], sp.[Seq], sp.[Parts], sp.[Part_No], sp.[Desc],
               sp.[Qty], sp.[Checked], sp.[ManagerChecked], sp.[UpdatedAt], sp.[UpdatedBy]
        FROM dbo.[ModelSP] AS sp
        WHERE sp.[Model_No] = @Model AND sp.[Version] = @V
          AND (@Like IS NULL OR sp.[Desc] LIKE @Like OR sp.[Part_No] LIKE @Like OR sp.[Parts] LIKE @Like)
    )
    SELECT
        l.[Model_No] AS ModelNo,
        l.[Version]  AS ModelVersion,
        (SELECT TOP 1 m.[MODEL] FROM dbo.[MODEL] AS m
          WHERE m.[MODELNO] = l.[Model_No] AND m.[VERSION] = l.[Version]) AS ModelName,
        l.[Seq]      AS Seq,
        l.[Parts]    AS DyacoNo,
        l.[Part_No]  AS SpNo,
        l.[Desc]     AS [Description],
        l.[Qty]      AS Qty,
        (SELECT TOP (1) s.[RETAIL_PRICE] FROM dbo.[Spareparts] AS s
          WHERE s.[SP_NO] = l.[Part_No]) AS RetailPrice,
        @Other AS OtherModelNos,
        CASE WHEN @IncludeDetail = 1 THEN l.[Checked]        END AS IsChecked,
        CASE WHEN @IncludeDetail = 1 THEN l.[ManagerChecked] END AS IsManagerChecked,
        CASE WHEN @IncludeDetail = 1 THEN l.[UpdatedAt]      END AS UpdatedAt,
        CASE WHEN @IncludeDetail = 1 THEN l.[UpdatedBy]      END AS UpdatedBy,
        COUNT(*) OVER () AS TotalRows
    FROM Lines AS l
    ORDER BY
        TRY_CONVERT(int, CASE WHEN SUBSTRING(l.[Parts], 1, 1) = '0' THEN '0'
                              WHEN PATINDEX('%[^0-9]%', l.[Parts]) = 0 THEN l.[Parts]
                              WHEN PATINDEX('%[^0-9]%', l.[Parts]) = 1 THEN '9999'
                              ELSE SUBSTRING(l.[Parts], 1, PATINDEX('%[^0-9]%', l.[Parts]) - 1) END),
        LEN(l.[Parts]), l.[Parts], l.[Seq]
    OFFSET @Skip ROWS FETCH NEXT @Top ROWS ONLY
    OPTION (RECOMPILE);
END
GO
PRINT 'agent.search_parts is in place.';
GO


/*==============================================================================
  agent.count_units — units per group, in one call


  Registered units (PURCHASE rows) counted per group: Brand, Category (FG),
  Model (with the name as GroupLabel), State, Dealer, Year of purchase. The
  agent's aggregate tool works on one table and cannot join, and MODEL cannot
  be a DAB entity (sql_variant columns), so the join lives here.
  Every filter is optional: product name or model number, brand, category,
  state, partial dealer, and a half-open purchase-date range (@PurchasedTo
  is EXCLUSIVE). No filter at all is allowed: the whole table by brand
  measured 1.6 s.
  The MODEL join is a LEFT JOIN: 2,492 units point at no MODEL row and come
  back as the null group instead of vanishing. Brand and category are UPPERed
  and state and dealer trimmed, because the stored values are not clean.
  Groups come largest first, capped at @Top (max 100); TotalUnits and
  TotalGroups cover ALL groups.
  Errors: 51080 bad @GroupBy, 51081 both @Name and @ModelNo, 51082 bad date
  range, 51083 no model matches.
==============================================================================*/

CREATE OR ALTER PROCEDURE agent.count_units
    @GroupBy       varchar(20),           -- REQUIRED: Brand | Category | Model | State | Dealer | Year
    @Name          varchar(100) = NULL,   -- product name, for example 'CT900'; via match_model
    @ModelNo       varchar(50)  = NULL,   -- exact model number; not with @Name
    @Brand         varchar(100) = NULL,   -- exact, case-insensitive
    @Category      varchar(100) = NULL,   -- MODEL.FG, exact, case-insensitive
    @State         varchar(10)  = NULL,   -- PURCHASE.STATE, exact
    @DealerNo      varchar(100) = NULL,   -- partial match on PURCHASE.DEALERNO
    @PurchasedFrom datetime     = NULL,   -- PURCHASEDDATE >= this
    @PurchasedTo   datetime     = NULL,   -- PURCHASEDDATE <  this (EXCLUSIVE)
    @Top           int          = 100     -- groups returned, 1 to 100
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @G  varchar(20)  = LOWER(LTRIM(RTRIM(@GroupBy)));
    DECLARE @Nm varchar(100) = NULLIF(LTRIM(RTRIM(@Name)), '');
    DECLARE @Mn varchar(50)  = NULLIF(LTRIM(RTRIM(@ModelNo)), '');
    DECLARE @Br varchar(100) = NULLIF(LTRIM(RTRIM(@Brand)), '');
    DECLARE @Ct varchar(100) = NULLIF(LTRIM(RTRIM(@Category)), '');
    DECLARE @St varchar(10)  = NULLIF(LTRIM(RTRIM(@State)), '');
    DECLARE @Dl varchar(100) = NULLIF(LTRIM(RTRIM(@DealerNo)), '');

    IF @G IS NULL OR @G NOT IN ('brand', 'category', 'model', 'state', 'dealer', 'year')
        THROW 51080, 'Pass @GroupBy as one of Brand, Category, Model, State, Dealer, Year.', 1;
    IF @Nm IS NOT NULL AND @Mn IS NOT NULL
        THROW 51081, 'Pass @Name or @ModelNo, not both.', 1;
    IF @PurchasedFrom IS NOT NULL AND @PurchasedTo IS NOT NULL AND @PurchasedTo <= @PurchasedFrom
        THROW 51082, '@PurchasedTo must be after @PurchasedFrom. To is exclusive: one day is From that date, To the next date.', 1;

    SET @Top = CASE WHEN @Top IS NULL OR @Top < 1 THEN 100 WHEN @Top > 100 THEN 100 ELSE @Top END;

    DECLARE @Models TABLE (ModelNo varchar(50) COLLATE DATABASE_DEFAULT NOT NULL PRIMARY KEY);
    DECLARE @UseModels bit = 0;
    IF @Mn IS NOT NULL
    BEGIN
        INSERT INTO @Models (ModelNo) VALUES (@Mn);
        SET @UseModels = 1;
    END
    ELSE IF @Nm IS NOT NULL
    BEGIN
        INSERT INTO @Models (ModelNo) SELECT mm.ModelNo FROM core.match_model(@Nm) AS mm;
        IF NOT EXISTS (SELECT 1 FROM @Models)
            THROW 51083, 'No model matches that name.', 1;
        SET @UseModels = 1;
    END

    DECLARE @Like varchar(120) = CASE WHEN @Dl IS NULL THEN NULL
        ELSE '%' + REPLACE(REPLACE(REPLACE(@Dl, '[', '[[]'), '%', '[%]'), '_', '[_]') + '%' END;

    DECLARE @Groups TABLE (
        GroupValue varchar(100) COLLATE DATABASE_DEFAULT NULL,
        Units      int NOT NULL);

    /* One pass over PURCHASE. The CASE on @G is the same expression in SELECT
       and GROUP BY, so the plan groups on the chosen key; RECOMPILE lets the
       optimiser drop the filters that are NULL for this call. */
    INSERT INTO @Groups (GroupValue, Units)
    SELECT
        CASE @G
            WHEN 'brand'    THEN UPPER(m.[Brand])
            WHEN 'category' THEN UPPER(m.[FG])
            WHEN 'model'    THEN p.[MODELNO]
            WHEN 'state'    THEN UPPER(LTRIM(RTRIM(p.[STATE])))
            WHEN 'dealer'   THEN UPPER(LTRIM(RTRIM(p.[DEALERNO])))
            WHEN 'year'     THEN CONVERT(varchar(4), YEAR(p.[PURCHASEDDATE]))
        END,
        COUNT(*)
    FROM dbo.[PURCHASE] AS p
    LEFT JOIN dbo.[MODEL] AS m
        ON m.[MODELNO] = p.[MODELNO] AND m.[VERSION] = p.[VERSION]
    WHERE (@UseModels = 0 OR p.[MODELNO] IN (SELECT ModelNo FROM @Models))
      AND (@Br IS NULL OR m.[Brand] = @Br)
      AND (@Ct IS NULL OR m.[FG] = @Ct)
      AND (@St IS NULL OR LTRIM(RTRIM(p.[STATE])) = @St)
      AND (@Like IS NULL OR p.[DEALERNO] LIKE @Like)
      AND (@PurchasedFrom IS NULL OR p.[PURCHASEDDATE] >= @PurchasedFrom)
      AND (@PurchasedTo   IS NULL OR p.[PURCHASEDDATE] <  @PurchasedTo)
    GROUP BY
        CASE @G
            WHEN 'brand'    THEN UPPER(m.[Brand])
            WHEN 'category' THEN UPPER(m.[FG])
            WHEN 'model'    THEN p.[MODELNO]
            WHEN 'state'    THEN UPPER(LTRIM(RTRIM(p.[STATE])))
            WHEN 'dealer'   THEN UPPER(LTRIM(RTRIM(p.[DEALERNO])))
            WHEN 'year'     THEN CONVERT(varchar(4), YEAR(p.[PURCHASEDDATE]))
        END
    OPTION (RECOMPILE);

    SELECT TOP (@Top)
        g.GroupValue,
        CASE WHEN @G = 'model' THEN
            (SELECT TOP (1) x.[MODEL] FROM dbo.[MODEL] AS x
              WHERE x.[MODELNO] = g.GroupValue ORDER BY x.[VERSION] DESC)
        END AS GroupLabel,
        g.Units,
        (SELECT SUM(Units) FROM @Groups) AS TotalUnits,
        (SELECT COUNT(*)   FROM @Groups) AS TotalGroups
    FROM @Groups AS g
    ORDER BY g.Units DESC, g.GroupValue;
END
GO
PRINT 'agent.count_units is in place.';
GO



/*==============================================================================
  agent.check_unit — serial -> the machine and its warranty, no owner


  For any audience: a guest, a dealer, staff. Never returns who owns it.
  REGISTERED. 1 when PURCHASE holds the serial. A machine that was built but
  never registered still comes back, Registered 0, with its model from the
  serial's first six digits (the MODEL version whose SNBegin..SNEnd covers the
  sequence in characters 11 onward, else the latest), and the warranty columns
  NULL: with no purchase date there is nothing to count from.
  SHAPE. One row per warranty term, the machine's columns on every row; a
  machine with no terms gives one row with the warranty columns NULL. No rows
  when no MODEL carries the first six digits.
  Error: 51100 no serial.
==============================================================================*/

CREATE OR ALTER PROCEDURE agent.check_unit
    @SerialNo varchar(50) = NULL    -- required
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Sn varchar(50) = NULLIF(LTRIM(RTRIM(@SerialNo)), '');
    IF @Sn IS NULL
        THROW 51100, '@SerialNo is required.', 1;

    ;WITH Reg AS (
        SELECT TOP (1)
            p.[MODELNO]       AS ModelNo,
            p.[VERSION]       AS ModelVersion,
            p.[DETAMFG]       AS MfgDate,
            p.[PURCHASEDDATE] AS PurchasedDate
        FROM dbo.[PURCHASE] AS p
        WHERE p.[SERIALNO] = @Sn
        ORDER BY p.[PURCHASEDDATE] DESC
    ),
    FromSerial AS (
        SELECT TOP (1) m.[MODELNO] AS ModelNo, m.[VERSION] AS ModelVersion
        FROM dbo.[MODEL] AS m
        WHERE m.[MODELNO] = LEFT(@Sn, 6)
        ORDER BY CASE WHEN TRY_CONVERT(int, SUBSTRING(@Sn, 11, 16))
                           BETWEEN m.[SNBegin] AND COALESCE(m.[SNEnd], 999999) THEN 0 ELSE 1 END,
                 m.[VERSION] DESC
    ),
    Unit AS (
        SELECT
            COALESCE(r.ModelNo, s.ModelNo)           AS ModelNo,
            COALESCE(r.ModelVersion, s.ModelVersion) AS ModelVersion,
            r.MfgDate,
            r.PurchasedDate,
            CASE WHEN r.ModelNo IS NULL THEN 0 ELSE 1 END AS Registered
        FROM (SELECT 1 AS One) AS x
        LEFT JOIN Reg AS r ON 1 = 1
        LEFT JOIN FromSerial AS s ON 1 = 1
    )
    SELECT
        @Sn             AS SerialNo,
        u.ModelNo,
        m.[MODEL]       AS ModelName,
        m.[Brand]       AS Brand,
        m.[FG]          AS Category,
        m.[Commercial]  AS Commercial,
        u.ModelVersion,
        u.MfgDate,
        u.PurchasedDate,
        u.Registered,
        t.WarrantyType,
        t.Term,
        t.Days,
        t.Lifetime,
        t.Expires,
        t.InWarranty,
        t.DaysLeft,
        COUNT(*) OVER () AS TotalRows
    FROM Unit AS u
    OUTER APPLY (
        SELECT TOP (1) mm.[MODEL], mm.[Brand], mm.[FG], mm.[Commercial]
        FROM dbo.[MODEL] AS mm
        WHERE mm.[MODELNO] = u.ModelNo
        ORDER BY CASE WHEN mm.[VERSION] = u.ModelVersion THEN 0 ELSE 1 END, mm.[VERSION] DESC
    ) AS m
    OUTER APPLY core.unit_warranty(CASE WHEN u.Registered = 1 THEN @Sn END) AS t
    WHERE u.ModelNo IS NOT NULL
    ORDER BY t.WarrantyType, t.Days DESC, t.Term
    OPTION (RECOMPILE);
END
GO
PRINT 'agent.check_unit is in place.';
GO


/*==============================================================================
  agent.my_unit — the guest's own machine, behind a proof


  The serial number plus the phone number or the email it was registered
  under (core.owner_match). On a match: one 'unit' row with the owner on
  file, one 'warranty' row per term, and one 'order' row per order, newest
  20, with tracking and what was charged. On no match: zero rows, the same
  shape, and nothing that says which part failed.
  The order amounts are what the person was charged: PartsAmount (the part
  lines' sale amounts), Freight, HandlingFee, Tax. LaborFee and TripFee are
  what Spirit pays the service company and are not here.
  A serial with more than one PURCHASE row gets zero rows whatever is given,
  because a phone or email from an earlier owner must not open the newest
  owner's details; such a guest is offered a person.
  Errors: 51110 no serial, 51111 neither a phone nor an email.
==============================================================================*/

CREATE OR ALTER PROCEDURE agent.my_unit
    @SerialNo varchar(50)  = NULL,   -- required
    @Phone    varchar(50)  = NULL,   -- the phone on file, any common format; this or @Email
    @Email    varchar(100) = NULL    -- the email on file; this or @Phone
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Sn varchar(50)  = NULLIF(LTRIM(RTRIM(@SerialNo)), '');
    DECLARE @P  varchar(50)  = NULLIF(LTRIM(RTRIM(@Phone)), '');
    DECLARE @E  varchar(100) = NULLIF(LTRIM(RTRIM(@Email)), '');
    IF @Sn IS NULL
        THROW 51110, '@SerialNo is required.', 1;
    IF @P IS NULL AND @E IS NULL
        THROW 51111, 'Pass @Phone or @Email.', 1;

    DECLARE @Out TABLE (
        RowKind       varchar(10)   COLLATE DATABASE_DEFAULT NOT NULL,
        SerialNo      varchar(50)   COLLATE DATABASE_DEFAULT NOT NULL,
        ModelNo       varchar(50)   COLLATE DATABASE_DEFAULT NULL,
        ModelName     nvarchar(255) COLLATE DATABASE_DEFAULT NULL,
        PurchasedDate smalldatetime NULL,
        CustomerName  varchar(200)  COLLATE DATABASE_DEFAULT NULL,
        Address       varchar(200)  COLLATE DATABASE_DEFAULT NULL,
        City          varchar(200)  COLLATE DATABASE_DEFAULT NULL,
        State         varchar(10)   COLLATE DATABASE_DEFAULT NULL,
        Zip           varchar(50)   COLLATE DATABASE_DEFAULT NULL,
        Phone         varchar(50)   COLLATE DATABASE_DEFAULT NULL,
        Phone2        varchar(50)   COLLATE DATABASE_DEFAULT NULL,
        Email         varchar(100)  COLLATE DATABASE_DEFAULT NULL,
        WarrantyType  nvarchar(50)  COLLATE DATABASE_DEFAULT NULL,
        Term          varchar(11)   COLLATE DATABASE_DEFAULT NULL,
        Days          int           NULL,
        Lifetime      int           NULL,
        Expires       smalldatetime NULL,
        InWarranty    int           NULL,
        DaysLeft      int           NULL,
        OrderNumber   varchar(61)   COLLATE DATABASE_DEFAULT NULL,
        OrderDate     datetime      NULL,
        OrderType     varchar(100)  COLLATE DATABASE_DEFAULT NULL,
        Status        varchar(6)    COLLATE DATABASE_DEFAULT NULL,
        ShippedDate   datetime      NULL,
        Carrier       varchar(30)   COLLATE DATABASE_DEFAULT NULL,
        TrackingNo    varchar(50)   COLLATE DATABASE_DEFAULT NULL,
        Freight       decimal(10,2) NULL,
        HandlingFee   decimal(10,2) NULL,
        Tax           decimal(10,2) NULL,
        PartsAmount   decimal(38,2) NULL
    );

    IF (SELECT COUNT(*) FROM dbo.[PURCHASE] WHERE [SERIALNO] = @Sn) = 1
       AND EXISTS (SELECT 1 FROM core.owner_match(@Sn, @P, @E))
    BEGIN
        INSERT INTO @Out (RowKind, SerialNo, ModelNo, ModelName, PurchasedDate, CustomerName,
                          Address, City, State, Zip, Phone, Phone2, Email)
        SELECT TOP (1) 'unit', p.[SERIALNO], p.[MODELNO], m.[MODEL], p.[PURCHASEDDATE], p.[PURCHASEDBY],
               p.[ADDRESS], p.[CITY], p.[STATE], p.[ZIP], p.[PHONE], p.[PHONE2], p.[EMAIL]
        FROM dbo.[PURCHASE] AS p
        OUTER APPLY (
            SELECT TOP (1) mm.[MODEL]
            FROM dbo.[MODEL] AS mm
            WHERE mm.[MODELNO] = p.[MODELNO]
            ORDER BY CASE WHEN mm.[VERSION] = p.[VERSION] THEN 0 ELSE 1 END, mm.[VERSION] DESC
        ) AS m
        WHERE p.[SERIALNO] = @Sn
        ORDER BY p.[PURCHASEDDATE] DESC;

        INSERT INTO @Out (RowKind, SerialNo, WarrantyType, Term, Days, Lifetime, Expires, InWarranty, DaysLeft)
        SELECT 'warranty', @Sn, t.WarrantyType, t.Term, t.Days, t.Lifetime, t.Expires, t.InWarranty, t.DaysLeft
        FROM core.unit_warranty(@Sn) AS t;

        INSERT INTO @Out (RowKind, SerialNo, OrderNumber, OrderDate, OrderType, Status, ShippedDate,
                          Carrier, TrackingNo, Freight, HandlingFee, Tax, PartsAmount)
        SELECT TOP (20) 'order', @Sn, o.OrderNumber, o.OrderDate, o.OrderType, o.Status, o.ShippedDate,
               o.Carrier, o.TrackingNo, o.Freight, o.HandlingFee, o.Tax, o.PartsAmount
        FROM core.unit_orders(@Sn, NULL, NULL) AS o
        ORDER BY o.OrderDate DESC
        OPTION (RECOMPILE);
    END

    SELECT
        RowKind, SerialNo, ModelNo, ModelName, PurchasedDate,
        CustomerName, Address, City, State, Zip, Phone, Phone2, Email,
        WarrantyType, Term, Days, Lifetime, Expires, InWarranty, DaysLeft,
        OrderNumber, OrderDate, OrderType, Status, ShippedDate, Carrier, TrackingNo,
        Freight, HandlingFee, Tax, PartsAmount
    FROM @Out
    ORDER BY CASE RowKind WHEN 'unit' THEN 0 WHEN 'warranty' THEN 1 ELSE 2 END,
             OrderDate DESC, WarrantyType, Days DESC, Term
    OPTION (RECOMPILE);
END
GO
PRINT 'agent.my_unit is in place.';
GO


/*==============================================================================
  agent.dealer_prices — part numbers -> dealer and retail price


  For dealers and staff. Up to 50 part numbers, comma separated, as search_parts
  returns them in SpNo. One row per part number asked for; Found 0 and NULL
  prices when the catalogue has no such part. Spirit's own cost is not here.
  Errors: 51130 no part number, 51131 more than 50 part numbers.
==============================================================================*/

CREATE OR ALTER PROCEDURE agent.dealer_prices
    @PartNos varchar(2000) = NULL    -- required: SpNo values, comma separated, at most 50
AS
BEGIN
    SET NOCOUNT ON;

    IF NULLIF(LTRIM(RTRIM(@PartNos)), '') IS NULL
        THROW 51130, '@PartNos is required: one or more part numbers, comma separated.', 1;

    DECLARE @Want TABLE (SpNo varchar(50) COLLATE DATABASE_DEFAULT NOT NULL PRIMARY KEY);
    INSERT INTO @Want (SpNo)
    SELECT DISTINCT LEFT(LTRIM(RTRIM(s.value)), 50)
    FROM STRING_SPLIT(@PartNos, ',') AS s
    WHERE LTRIM(RTRIM(s.value)) <> '';
    IF (SELECT COUNT(*) FROM @Want) > 50
        THROW 51131, 'Pass at most 50 part numbers.', 1;

    SELECT
        w.SpNo,
        p.[DESC]         AS [Description],
        p.[RETAIL_PRICE] AS RetailPrice,
        p.[DEALER_PRICE] AS DealerPrice,
        CASE WHEN p.[SP_NO] IS NULL THEN 0 ELSE 1 END AS Found
    FROM @Want AS w
    LEFT JOIN dbo.[Spareparts] AS p ON p.[SP_NO] = w.SpNo
    ORDER BY w.SpNo;
END
GO
PRINT 'agent.dealer_prices is in place.';
GO


/*==============================================================================
  agent.order_status — where an order is, no money, no owner


  For dealers. One order by its number 'ServiceId-OrderId', or the newest @Top
  orders on one serial number. One row per part line, shipped (OrderDetail)
  or on order (OrderDetailBack); an order with no lines gives one row with the
  part columns NULL.
  The number is split in its own statements before any query touches it: a
  query that splits and converts together lets the engine run LEFT(v, -1) on
  a number with no dash.
  Errors: 51120 pass exactly one of @OrderNumber and @SerialNo, 51121 the
  order number is not ServiceId-OrderId.
==============================================================================*/

CREATE OR ALTER PROCEDURE agent.order_status
    @OrderNumber varchar(30) = NULL,   -- 'ServiceId-OrderId', for example '845435-1'; this or @SerialNo
    @SerialNo    varchar(50) = NULL,   -- exact serial number; this or @OrderNumber
    @Top         int         = 10      -- orders on a serial, 1 to 20, newest first
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @On varchar(30) = NULLIF(LTRIM(RTRIM(@OrderNumber)), '');
    DECLARE @Sn varchar(50) = NULLIF(LTRIM(RTRIM(@SerialNo)), '');
    IF (@On IS NULL AND @Sn IS NULL) OR (@On IS NOT NULL AND @Sn IS NOT NULL)
        THROW 51120, 'Pass exactly one of @OrderNumber and @SerialNo.', 1;
    SET @Top = CASE WHEN @Top IS NULL OR @Top < 1 THEN 10 WHEN @Top > 20 THEN 20 ELSE @Top END;

    DECLARE @ServiceId int = NULL, @OrderId int = NULL, @Dash int;
    IF @On IS NOT NULL
    BEGIN
        SET @Dash = CHARINDEX('-', @On);
        IF @Dash > 1 AND @Dash < LEN(@On)
        BEGIN
            SET @ServiceId = TRY_CONVERT(int, LEFT(@On, @Dash - 1));
            SET @OrderId   = TRY_CONVERT(int, SUBSTRING(@On, @Dash + 1, 30));
        END
        IF @ServiceId IS NULL OR @OrderId IS NULL
            THROW 51121, '@OrderNumber must look like 845435-1: the service id, a dash, the order id.', 1;
    END

    ;WITH Ord AS (
        SELECT TOP (@Top) o.ServiceId, o.OrderId, o.OrderNumber, o.OrderDate, o.OrderType, o.Status,
               o.ShippedDate, o.Carrier, o.TrackingNo
        FROM core.unit_orders(@Sn, @ServiceId, @OrderId) AS o
        ORDER BY o.OrderDate DESC
    )
    SELECT
        o.OrderNumber,
        o.OrderDate,
        o.OrderType,
        o.Status,
        o.ShippedDate,
        o.Carrier,
        o.TrackingNo,
        d.SP_NO        AS PartNo,
        d.[Desc]       AS PartDesc,
        d.Item_Shipped AS Qty,
        d.Source
    FROM Ord AS o
    LEFT JOIN (
        SELECT SERVICEID, OrderID, SP_NO, [Desc], Item_Shipped, 'Shipped' AS Source
        FROM dbo.OrderDetail
        UNION ALL
        SELECT SERVICEID, OrderID, SP_NO, [Desc], Item_Shipped, 'Backordered' AS Source
        FROM dbo.OrderDetailBack
    ) AS d
        ON d.SERVICEID = o.ServiceId AND d.OrderID = o.OrderId
    ORDER BY o.OrderDate DESC, o.OrderNumber, d.Source, d.SP_NO
    OPTION (RECOMPILE);
END
GO
PRINT 'agent.order_status is in place.';
GO
