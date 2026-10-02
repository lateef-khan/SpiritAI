/*
================================================================================
 Spirit agent procedures on Sage 100 (SPIRITSRV-021, database MAS_SFC)

 Run this file top to bottom with sqlcmd -b, after sql/sage/logins.sql. Every
 object is CREATE OR ALTER, so the file can run again at any time, and must
 run again after a Sage upgrade or a restore of MAS_SFC.

 MAS_SFC belongs to Sage. We add only two schemas, both owned by dbo, and the
 objects in them. No index, no table, no change to a Sage object.
   core    building blocks. spiritai_dab holds no right on core.
   agent   the doors. spiritai_dab holds EXECUTE on this schema and nothing
           else; the procedures read Sage's tables through ownership chaining.

 What is here:
   core.invoice_rows        invoice handle -> header, lines, packages
   agent.get_invoice        the same without who was billed    dealer
   agent.get_invoice_full   the same with who was billed       staff and up

 MAS_SFC is SQL_Latin1_General_CP1_CI_AS throughout; there is no collation
 rule here. One result shape per procedure, as on CustService.
================================================================================
*/

USE [MAS_SFC];
GO
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF SCHEMA_ID(N'core') IS NULL  EXEC (N'CREATE SCHEMA core AUTHORIZATION dbo');
IF SCHEMA_ID(N'agent') IS NULL EXEC (N'CREATE SCHEMA agent AUTHORIZATION dbo');
GO


/*==============================================================================
  core.invoice_rows — invoice handle -> header, lines, packages


  One handle in, the others NULL: an invoice number, a sales order number, a
  customer PO, or a serial number (AR_InvoiceHistoryLotSerial). The newest 20
  invoices that match. An invoice is InvoiceNo + HeaderSeqNo.
  ROWS. RowKind 'line' per AR_InvoiceHistoryDetail row, 'tracking' per
  AR_InvoiceHistoryTracking package; the header columns ride on every row.
  AMOUNTS. SalesAmt is TaxableSalesAmt + NonTaxableSalesAmt. InvoiceTotal is
  SalesAmt + FreightAmt + SalesTaxAmt - DiscountAmt. UnitPrice and
  ExtensionAmt are what the customer was charged. No *Cost* column is read.
  DEALER LINES. With @ForDealer = 1 a line whose ItemCode starts with '/' is
  left out. Sage uses those for miscellaneous and comment lines, and their
  description is free text that can hold a name or an address.
  SERIALS. SerialCount is how many serial numbers the line shipped. HasSerial
  is 1 on the line that shipped @SerialNo.
==============================================================================*/

CREATE OR ALTER FUNCTION core.invoice_rows
(
    @InvoiceNo    varchar(20),
    @SalesOrderNo varchar(20),
    @CustomerPONo varchar(30),
    @SerialNo     varchar(40),
    @ForDealer    bit
)
RETURNS TABLE
AS
RETURN
(
    WITH Picked AS (
        SELECT TOP (20) h.InvoiceNo, h.HeaderSeqNo
        FROM dbo.AR_InvoiceHistoryHeader AS h
        WHERE (@InvoiceNo    IS NOT NULL AND h.InvoiceNo    = @InvoiceNo)
           OR (@SalesOrderNo IS NOT NULL AND h.SalesOrderNo = @SalesOrderNo)
           OR (@CustomerPONo IS NOT NULL AND h.CustomerPONo = @CustomerPONo)
           OR (@SerialNo     IS NOT NULL AND EXISTS (
                  SELECT 1 FROM dbo.AR_InvoiceHistoryLotSerial AS ls
                  WHERE ls.InvoiceNo = h.InvoiceNo AND ls.HeaderSeqNo = h.HeaderSeqNo
                    AND ls.LotSerialNo = @SerialNo))
        ORDER BY h.InvoiceDate DESC, h.InvoiceNo DESC, h.HeaderSeqNo DESC
    ),
    Head AS (
        SELECT
            h.InvoiceNo, h.HeaderSeqNo, h.InvoiceDate, h.InvoiceType, h.SalesOrderNo, h.CustomerPONo,
            h.ShipDate, h.ShipVia,
            CAST(ISNULL(h.TaxableSalesAmt, 0) + ISNULL(h.NonTaxableSalesAmt, 0) AS decimal(18, 2)) AS SalesAmt,
            CAST(h.FreightAmt  AS decimal(18, 2)) AS FreightAmt,
            CAST(h.SalesTaxAmt AS decimal(18, 2)) AS SalesTaxAmt,
            CAST(h.DiscountAmt AS decimal(18, 2)) AS DiscountAmt,
            CAST(ISNULL(h.TaxableSalesAmt, 0) + ISNULL(h.NonTaxableSalesAmt, 0) + ISNULL(h.FreightAmt, 0)
                 + ISNULL(h.SalesTaxAmt, 0) - ISNULL(h.DiscountAmt, 0) AS decimal(18, 2)) AS InvoiceTotal,
            h.ARDivisionNo, h.CustomerNo,
            h.BillToName, h.BillToAddress1, h.BillToAddress2, h.BillToAddress3,
            h.BillToCity, h.BillToState, h.BillToZipCode,
            h.ShipToName, h.ShipToAddress1, h.ShipToAddress2, h.ShipToAddress3,
            h.ShipToCity, h.ShipToState, h.ShipToZipCode,
            h.EmailAddress, h.TelephoneNo
        FROM Picked AS p
        JOIN dbo.AR_InvoiceHistoryHeader AS h
            ON h.InvoiceNo = p.InvoiceNo AND h.HeaderSeqNo = p.HeaderSeqNo
    )
    SELECT
        CAST('line' AS varchar(10)) AS RowKind,
        hd.InvoiceNo, hd.HeaderSeqNo, hd.InvoiceDate, hd.InvoiceType, hd.SalesOrderNo, hd.CustomerPONo,
        hd.ShipDate, hd.ShipVia, hd.SalesAmt, hd.FreightAmt, hd.SalesTaxAmt, hd.DiscountAmt, hd.InvoiceTotal,
        hd.ARDivisionNo, hd.CustomerNo,
        hd.BillToName, hd.BillToAddress1, hd.BillToAddress2, hd.BillToAddress3,
        hd.BillToCity, hd.BillToState, hd.BillToZipCode,
        hd.ShipToName, hd.ShipToAddress1, hd.ShipToAddress2, hd.ShipToAddress3,
        hd.ShipToCity, hd.ShipToState, hd.ShipToZipCode,
        hd.EmailAddress, hd.TelephoneNo,
        d.DetailSeqNo AS LineSeq,
        d.ItemCode,
        d.ItemCodeDesc,
        CAST(d.QuantityShipped AS decimal(18, 4)) AS QuantityShipped,
        CAST(d.UnitPrice       AS decimal(18, 4)) AS UnitPrice,
        CAST(d.ExtensionAmt    AS decimal(18, 2)) AS ExtensionAmt,
        (SELECT COUNT(*) FROM dbo.AR_InvoiceHistoryLotSerial AS ls
          WHERE ls.InvoiceNo = d.InvoiceNo AND ls.HeaderSeqNo = d.HeaderSeqNo
            AND ls.DetailSeqNo = d.DetailSeqNo) AS SerialCount,
        CASE WHEN @SerialNo IS NOT NULL AND EXISTS (
                  SELECT 1 FROM dbo.AR_InvoiceHistoryLotSerial AS ls
                  WHERE ls.InvoiceNo = d.InvoiceNo AND ls.HeaderSeqNo = d.HeaderSeqNo
                    AND ls.DetailSeqNo = d.DetailSeqNo AND ls.LotSerialNo = @SerialNo)
             THEN 1 ELSE 0 END AS HasSerial,
        CAST(NULL AS varchar(30)) AS TrackingID,
        CAST(NULL AS varchar(50)) AS Carrier
    FROM Head AS hd
    JOIN dbo.AR_InvoiceHistoryDetail AS d
        ON d.InvoiceNo = hd.InvoiceNo AND d.HeaderSeqNo = hd.HeaderSeqNo
    WHERE NOT (@ForDealer = 1 AND d.ItemCode LIKE '/%')
    UNION ALL
    SELECT
        CAST('tracking' AS varchar(10)),
        hd.InvoiceNo, hd.HeaderSeqNo, hd.InvoiceDate, hd.InvoiceType, hd.SalesOrderNo, hd.CustomerPONo,
        hd.ShipDate, hd.ShipVia, hd.SalesAmt, hd.FreightAmt, hd.SalesTaxAmt, hd.DiscountAmt, hd.InvoiceTotal,
        hd.ARDivisionNo, hd.CustomerNo,
        hd.BillToName, hd.BillToAddress1, hd.BillToAddress2, hd.BillToAddress3,
        hd.BillToCity, hd.BillToState, hd.BillToZipCode,
        hd.ShipToName, hd.ShipToAddress1, hd.ShipToAddress2, hd.ShipToAddress3,
        hd.ShipToCity, hd.ShipToState, hd.ShipToZipCode,
        hd.EmailAddress, hd.TelephoneNo,
        CAST(NULL AS varchar(6)),
        CAST(NULL AS varchar(30)),
        CAST(NULL AS varchar(30)),
        CAST(NULL AS decimal(18, 4)),
        CAST(NULL AS decimal(18, 4)),
        CAST(NULL AS decimal(18, 2)),
        CAST(NULL AS int),
        CAST(NULL AS int),
        t.TrackingID,
        t.StarshipShipVia
    FROM Head AS hd
    JOIN dbo.AR_InvoiceHistoryTracking AS t
        ON t.InvoiceNo = hd.InvoiceNo AND t.HeaderSeqNo = hd.HeaderSeqNo
);
GO
PRINT 'core.invoice_rows is in place.';
GO


/*==============================================================================
  agent.get_invoice — invoices without who was billed


  For dealers. Exactly one handle. An invoice or sales order number of fewer
  than seven digits is padded with leading zeros, the way Sage stores them.
  No bill-to, ship-to, customer number, email, or phone. CustomerPONo finds an
  invoice but is not returned: it is free text and can hold a consumer's
  surname. Detail lines whose ItemCode starts with '/' are left out, because
  their description is free text too.
  At most 500 rows, newest invoice first.
  Error: 51200 pass exactly one handle.
==============================================================================*/

CREATE OR ALTER PROCEDURE agent.get_invoice
    @InvoiceNo    varchar(20) = NULL,
    @SalesOrderNo varchar(20) = NULL,
    @CustomerPONo varchar(30) = NULL,
    @SerialNo     varchar(40) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @FD bit = 1;
    DECLARE @I varchar(20) = NULLIF(LTRIM(RTRIM(@InvoiceNo)), '');
    DECLARE @S varchar(20) = NULLIF(LTRIM(RTRIM(@SalesOrderNo)), '');
    DECLARE @P varchar(30) = NULLIF(LTRIM(RTRIM(@CustomerPONo)), '');
    DECLARE @N varchar(40) = NULLIF(LTRIM(RTRIM(@SerialNo)), '');
    IF (CASE WHEN @I IS NULL THEN 0 ELSE 1 END + CASE WHEN @S IS NULL THEN 0 ELSE 1 END
      + CASE WHEN @P IS NULL THEN 0 ELSE 1 END + CASE WHEN @N IS NULL THEN 0 ELSE 1 END) <> 1
        THROW 51200, 'Pass exactly one of @InvoiceNo, @SalesOrderNo, @CustomerPONo and @SerialNo.', 1;
    IF @I NOT LIKE '%[^0-9]%' AND LEN(@I) < 7 SET @I = RIGHT('0000000' + @I, 7);
    IF @S NOT LIKE '%[^0-9]%' AND LEN(@S) < 7 SET @S = RIGHT('0000000' + @S, 7);

    SELECT TOP (500)
        r.RowKind, r.InvoiceNo, r.HeaderSeqNo, r.InvoiceDate, r.InvoiceType, r.SalesOrderNo,
        r.ShipDate, r.ShipVia, r.SalesAmt, r.FreightAmt, r.SalesTaxAmt, r.DiscountAmt, r.InvoiceTotal,
        r.LineSeq, r.ItemCode, r.ItemCodeDesc, r.QuantityShipped, r.UnitPrice, r.ExtensionAmt,
        r.SerialCount, r.HasSerial, r.TrackingID, r.Carrier
    FROM core.invoice_rows(@I, @S, @P, @N, @FD) AS r
    ORDER BY r.InvoiceDate DESC, r.InvoiceNo, r.HeaderSeqNo, r.RowKind, r.LineSeq, r.TrackingID
    OPTION (RECOMPILE);
END
GO
PRINT 'agent.get_invoice is in place.';
GO


/*==============================================================================
  agent.get_invoice_full — invoices with who was billed


  For staff and up. get_invoice plus the customer number, bill-to, ship-to,
  email, and phone on the invoice, the customer PO, and every detail line.
  At most 500 rows, newest invoice first.
  Error: 51200 pass exactly one handle.
==============================================================================*/

CREATE OR ALTER PROCEDURE agent.get_invoice_full
    @InvoiceNo    varchar(20) = NULL,
    @SalesOrderNo varchar(20) = NULL,
    @CustomerPONo varchar(30) = NULL,
    @SerialNo     varchar(40) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @FD bit = 0;
    DECLARE @I varchar(20) = NULLIF(LTRIM(RTRIM(@InvoiceNo)), '');
    DECLARE @S varchar(20) = NULLIF(LTRIM(RTRIM(@SalesOrderNo)), '');
    DECLARE @P varchar(30) = NULLIF(LTRIM(RTRIM(@CustomerPONo)), '');
    DECLARE @N varchar(40) = NULLIF(LTRIM(RTRIM(@SerialNo)), '');
    IF (CASE WHEN @I IS NULL THEN 0 ELSE 1 END + CASE WHEN @S IS NULL THEN 0 ELSE 1 END
      + CASE WHEN @P IS NULL THEN 0 ELSE 1 END + CASE WHEN @N IS NULL THEN 0 ELSE 1 END) <> 1
        THROW 51200, 'Pass exactly one of @InvoiceNo, @SalesOrderNo, @CustomerPONo and @SerialNo.', 1;
    IF @I NOT LIKE '%[^0-9]%' AND LEN(@I) < 7 SET @I = RIGHT('0000000' + @I, 7);
    IF @S NOT LIKE '%[^0-9]%' AND LEN(@S) < 7 SET @S = RIGHT('0000000' + @S, 7);

    SELECT TOP (500)
        r.RowKind, r.InvoiceNo, r.HeaderSeqNo, r.InvoiceDate, r.InvoiceType, r.SalesOrderNo, r.CustomerPONo,
        r.ShipDate, r.ShipVia, r.SalesAmt, r.FreightAmt, r.SalesTaxAmt, r.DiscountAmt, r.InvoiceTotal,
        r.ARDivisionNo, r.CustomerNo,
        r.BillToName, r.BillToAddress1, r.BillToAddress2, r.BillToAddress3,
        r.BillToCity, r.BillToState, r.BillToZipCode,
        r.ShipToName, r.ShipToAddress1, r.ShipToAddress2, r.ShipToAddress3,
        r.ShipToCity, r.ShipToState, r.ShipToZipCode,
        r.EmailAddress, r.TelephoneNo,
        r.LineSeq, r.ItemCode, r.ItemCodeDesc, r.QuantityShipped, r.UnitPrice, r.ExtensionAmt,
        r.SerialCount, r.HasSerial, r.TrackingID, r.Carrier
    FROM core.invoice_rows(@I, @S, @P, @N, @FD) AS r
    ORDER BY r.InvoiceDate DESC, r.InvoiceNo, r.HeaderSeqNo, r.RowKind, r.LineSeq, r.TrackingID
    OPTION (RECOMPILE);
END
GO
PRINT 'agent.get_invoice_full is in place.';
GO
