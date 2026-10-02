<#
.SYNOPSIS
  Proves the SpiritAI SQL logins and agent procedures on CustService and Sage.
.DESCRIPTION
  Runs on SPIRITSRV-024. Reads six passwords from standard input, one per line, in this order:
  CustService spiritai_dab, spiritai_manager, spiritai_admin, then Sage spiritai_dab,
  spiritai_manager, spiritai_admin. Prints PASS and FAIL lines only and never a row, so no
  customer data leaves the server. Exits 1 when a check fails.
.PARAMETER Part
  logins           the logins and the cost DENYs (sql/*/logins.sql)
  snapshot-before  hashes what the existing procedures return, into snapshot.txt beside this file
  snapshot-after   hashes them again and compares with snapshot.txt
  custservice      the CustService procedures (sql/custservice/agent-procs.sql)
  sage             the Sage procedures (sql/sage/agent-procs.sql)
#>
param(
    [Parameter(Mandatory)]
    [ValidateSet('logins', 'snapshot-before', 'snapshot-after', 'custservice', 'sage')]
    [string] $Part
)

$ErrorActionPreference = 'Stop'

$Passwords = @{}
foreach ($key in 'cs_dab', 'cs_manager', 'cs_admin', 'sage_dab', 'sage_manager', 'sage_admin') {
    $line = [Console]::In.ReadLine()
    if ([string]::IsNullOrWhiteSpace($line)) {
        Write-Output "FAIL  no password for $key on standard input"
        exit 1
    }
    $Passwords[$key] = $line.Trim()
}

$Servers = @{
    cs   = @{ Server = 'localhost';     Database = 'CustService' }
    sage = @{ Server = 'SPIRITSRV-021'; Database = 'MAS_SFC' }
}

# Columns that name or reach a person. A dealer tool must return none of them.
$Personal = @(
    'CustomerName', 'Address', 'City', 'State', 'Zip', 'Phone', 'Phone2', 'Email',
    'ARDivisionNo', 'CustomerNo', 'BillToName', 'BillToAddress1', 'BillToAddress2', 'BillToAddress3',
    'BillToCity', 'BillToState', 'BillToZipCode', 'ShipToName', 'ShipToAddress1', 'ShipToAddress2',
    'ShipToAddress3', 'ShipToCity', 'ShipToState', 'ShipToZipCode', 'EmailAddress', 'TelephoneNo')

$script:Failed = 0
$SnapshotFile = Join-Path $PSScriptRoot 'snapshot.txt'

function Read-Table([string] $Side, [string] $Role, [string] $Sql, [hashtable] $Params = @{}) {
    $target = $Servers[$Side]
    $builder = New-Object System.Data.SqlClient.SqlConnectionStringBuilder
    $builder['Data Source'] = $target.Server
    $builder['Initial Catalog'] = $target.Database
    $builder['User ID'] = "spiritai_$Role"
    $builder['Password'] = $Passwords["${Side}_$Role"]
    $builder['Connect Timeout'] = 15
    $builder['TrustServerCertificate'] = $true
    $connection = New-Object System.Data.SqlClient.SqlConnection $builder.ConnectionString
    $connection.Open()
    try {
        $command = $connection.CreateCommand()
        $command.CommandText = $Sql
        $command.CommandTimeout = 120
        foreach ($name in $Params.Keys) {
            $value = if ($null -eq $Params[$name]) { [DBNull]::Value } else { $Params[$name] }
            [void] $command.Parameters.AddWithValue("@$name", $value)
        }
        $table = New-Object System.Data.DataTable
        $reader = $command.ExecuteReader()
        try { $table.Load($reader) } finally { $reader.Close() }
        return , $table
    }
    finally { $connection.Close() }
}

# SQL error text can quote data, so a FAIL line carries only the error number.
function Find-SqlException([Exception] $Exception) {
    while ($Exception) {
        if ($Exception -is [System.Data.SqlClient.SqlException]) { return $Exception }
        $Exception = $Exception.InnerException
    }
    return $null
}

function Check([string] $What, [scriptblock] $Test) {
    try {
        $detail = & $Test
        Write-Output "PASS  $What $detail"
    }
    catch {
        $script:Failed++
        $sqlError = Find-SqlException $_.Exception
        if ($sqlError) { Write-Output "FAIL  $What (SQL error $($sqlError.Number))" }
        else { Write-Output "FAIL  $What ($($_.Exception.Message))" }
    }
}

function Assert([bool] $Condition, [string] $Message) {
    if (-not $Condition) { throw $Message }
}

function Expect-Denied([string] $Side, [string] $Role, [string] $Sql) {
    try { [void] (Read-Table $Side $Role $Sql) }
    catch {
        if ($_.Exception.Message -match 'permission was denied') { return '(denied)' }
        throw
    }
    throw 'it was readable'
}

function Expect-Readable([string] $Side, [string] $Role, [string] $Sql) {
    $table = Read-Table $Side $Role $Sql
    return "($($table.Rows.Count) rows)"
}

function Expect-Refused([string] $Side, [string] $Sql, [hashtable] $Params, [int] $Number = 0) {
    try { [void] (Read-Table $Side 'dab' $Sql $Params) }
    catch {
        if ($Number -eq 0) { return '(refused)' }
        $sqlError = Find-SqlException $_.Exception
        Assert ($sqlError -and $sqlError.Number -eq $Number) "expected SQL error $Number, got $(if ($sqlError) { $sqlError.Number } else { 'none' })"
        return "(refused, $Number)"
    }
    throw 'it was not refused'
}

function Columns-Of([string] $Side, [string] $Batch) {
    $table = Read-Table $Side 'dab' 'SELECT name FROM sys.dm_exec_describe_first_result_set(@Batch, NULL, 0) WHERE error_number IS NULL AND name IS NOT NULL' @{ Batch = $Batch }
    return @($table.Rows | ForEach-Object { [string] $_['name'] })
}

function Expect-NoPersonal([string] $Side, [string] $Batch, [string[]] $AlsoBanned = @()) {
    $columns = Columns-Of $Side $Batch
    Assert ($columns.Count -gt 0) 'no columns described'
    $banned = $Personal + $AlsoBanned
    $hits = @($columns | Where-Object { $banned -contains $_ })
    Assert ($hits.Count -eq 0) "banned columns: $($hits -join ', ')"
    return "($($columns.Count) columns, none banned)"
}

# The login that owns the objects (admin) lists their names, because a login cannot see objects it
# has no right on and would count zero for the wrong reason. The login under test then reports its
# own rights over that list.
function Object-Names([string] $Side) {
    $table = Read-Table $Side 'admin' "SELECT QUOTENAME(SCHEMA_NAME(schema_id)) + N'.' + QUOTENAME(name) AS n FROM sys.objects WHERE type IN ('U', 'V') AND is_ms_shipped = 0"
    Assert ($table.Rows.Count -gt 0) 'no tables listed'
    return @($table.Rows | ForEach-Object { [string] $_['n'] })
}

function Expect-NoReadableObjects([string] $Side) {
    $names = Object-Names $Side
    $t = Read-Table $Side 'dab' "SELECT COUNT(*) AS n FROM STRING_SPLIT(@Names, NCHAR(10)) AS s WHERE HAS_PERMS_BY_NAME(s.value, 'OBJECT', 'SELECT') = 1" @{ Names = ($names -join "`n") }
    $count = [int] $t.Rows[0]['n']
    Assert ($count -eq 0) "dab can select from $count of $($names.Count) tables and views"
    return "(0 of $($names.Count) readable)"
}

function Expect-NoReadableCostColumns([string] $Side) {
    $cost = Read-Table $Side 'admin' "SELECT QUOTENAME(SCHEMA_NAME(o.schema_id)) + N'.' + QUOTENAME(o.name) + N'|' + c.name AS n FROM sys.columns AS c JOIN sys.objects AS o ON o.object_id = c.object_id WHERE o.type IN ('U', 'V') AND o.is_ms_shipped = 0 AND LOWER(c.name) LIKE N'%cost%'"
    $pairs = @($cost.Rows | ForEach-Object { [string] $_['n'] })
    Assert ($pairs.Count -gt 0) 'no cost columns listed'
    $t = Read-Table $Side 'manager' "SELECT COUNT(*) AS n FROM STRING_SPLIT(@Pairs, NCHAR(10)) AS s WHERE HAS_PERMS_BY_NAME(LEFT(s.value, CHARINDEX(N'|', s.value) - 1), 'OBJECT', 'SELECT', SUBSTRING(s.value, CHARINDEX(N'|', s.value) + 1, 400), 'COLUMN') = 1" @{ Pairs = ($pairs -join "`n") }
    $count = [int] $t.Rows[0]['n']
    Assert ($count -eq 0) "manager can select $count of $($pairs.Count) cost columns"
    return "(0 of $($pairs.Count) readable)"
}

function Test-Logins {
    Check 'CustService: manager cannot read Spareparts.STD_COST' { Expect-Denied cs manager 'SELECT TOP 1 STD_COST FROM dbo.Spareparts' }
    Check 'CustService: manager cannot SELECT * from Spareparts' { Expect-Denied cs manager 'SELECT TOP 1 * FROM dbo.Spareparts' }
    Check 'CustService: manager reads part prices' { Expect-Readable cs manager 'SELECT TOP 1 SP_NO, RETAIL_PRICE, DEALER_PRICE FROM dbo.Spareparts' }
    Check 'CustService: manager cannot read OrderDetail.Cost' { Expect-Denied cs manager 'SELECT TOP 1 Cost FROM dbo.OrderDetail' }
    Check 'CustService: manager cannot read the view ViewSoleFreightParts' { Expect-Denied cs manager 'SELECT TOP 1 * FROM dbo.ViewSoleFreightParts' }
    Check 'CustService: manager reads warranty orders' { Expect-Readable cs manager "SELECT TOP 5 ServiceId, OrderId, OrderType, Freight, LaborFee FROM dbo.OrderTable WHERE OrderType = 'Warranty'" }
    Check 'CustService: admin reads Spareparts.STD_COST' { Expect-Readable cs admin 'SELECT TOP 1 STD_COST FROM dbo.Spareparts' }
    Check 'CustService: dab cannot read PURCHASE' { Expect-Denied cs dab 'SELECT TOP 1 SERIALNO FROM dbo.PURCHASE' }
    Check 'CustService: dab runs agent.find_model' {
        $t = Read-Table cs dab 'EXEC agent.find_model @Name = @Name' @{ Name = 'CT900' }
        Assert ($t.Rows.Count -ge 1) 'no rows for CT900'
        "($($t.Rows.Count) rows)"
    }
    Check 'CustService: dab can select from no table or view' { Expect-NoReadableObjects cs }
    Check 'CustService: manager can select no cost column' { Expect-NoReadableCostColumns cs }
    Check 'Sage: manager cannot read CI_Item.StandardUnitCost' { Expect-Denied sage manager 'SELECT TOP 1 StandardUnitCost FROM dbo.CI_Item' }
    Check 'Sage: manager cannot read IM_ItemTransactionHistory.UnitCost' { Expect-Denied sage manager 'SELECT TOP 1 UnitCost FROM dbo.IM_ItemTransactionHistory' }
    Check 'Sage: manager reads items' { Expect-Readable sage manager 'SELECT TOP 1 ItemCode, ItemCodeDesc, Valuation FROM dbo.CI_Item' }
    Check 'Sage: manager counts units sold' { Expect-Readable sage manager "SELECT TOP 5 RTRIM(i.ItemCode) AS ItemCode, CAST(SUM(h.TransactionQty * -1) AS int) AS UnitsSold FROM dbo.IM_ItemTransactionHistory AS h JOIN dbo.CI_Item AS i ON i.ItemCode = h.ItemCode WHERE h.TransactionCode = 'SO' AND i.Valuation IN ('3', '6') GROUP BY RTRIM(i.ItemCode)" }
    Check 'Sage: admin reads CI_Item.StandardUnitCost' { Expect-Readable sage admin 'SELECT TOP 1 StandardUnitCost FROM dbo.CI_Item' }
    Check 'Sage: dab cannot read AR_InvoiceHistoryHeader' { Expect-Denied sage dab 'SELECT TOP 1 InvoiceNo FROM dbo.AR_InvoiceHistoryHeader' }
    Check 'Sage: dab can select from no table or view' { Expect-NoReadableObjects sage }
    Check 'Sage: manager can select no cost column' { Expect-NoReadableCostColumns sage }
}

# Columns added on purpose; the before/after hash leaves them out.
$NewColumns = @('RetailPrice')

function Hash-Of([System.Data.DataTable] $Table) {
    $kept = @($Table.Columns | Where-Object { $NewColumns -notcontains $_.ColumnName })
    $text = New-Object System.Text.StringBuilder
    foreach ($column in $kept) { [void] $text.Append($column.ColumnName).Append(':').Append($column.DataType.Name).Append('|') }
    foreach ($row in $Table.Rows) {
        [void] $text.Append("`n")
        foreach ($column in $kept) {
            $value = if ($row.IsNull($column)) { "`0" } else { [string] $row[$column] }
            [void] $text.Append($value).Append('|')
        }
    }
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($text.ToString())
    $hash = [System.Security.Cryptography.SHA256]::Create().ComputeHash($bytes)
    return ([System.BitConverter]::ToString($hash)).Replace('-', '').Substring(0, 16)
}

function Snapshot-Calls([string[]] $Serials) {
    $calls = [ordered] @{
        'find_model CT900'      = @('EXEC agent.find_model @Name = @Name', @{ Name = 'CT900' })
        'find_model F63'        = @('EXEC agent.find_model @Name = @Name', @{ Name = 'F63' })
        'count_units Brand'     = @('EXEC agent.count_units @GroupBy = @GroupBy, @PurchasedTo = @To', @{ GroupBy = 'Brand'; To = [datetime] '2026-09-01' })
        'get_work_orders CT900' = @('EXEC agent.get_work_orders @Name = @Name, @OrderDateTo = @To, @Top = 20', @{ Name = 'CT900'; To = [datetime] '2026-09-01' })
        'search_parts CT900'    = @('EXEC agent.search_parts @Name = @Name, @Top = 100', @{ Name = 'CT900' })
    }
    $i = 0
    foreach ($serial in $Serials) {
        $i++
        $calls["get_unit sample $i"] = @('EXEC agent.get_unit @SerialNo = @SerialNo', @{ SerialNo = $serial })
    }
    return $calls
}

# The sample serials are read as admin and kept only in snapshot.txt on the server. They are
# never printed.
function Snapshot-Before {
    Remove-Item $SnapshotFile -ErrorAction SilentlyContinue
    $script:SnapshotLines = @()
    Check 'sampled serials' {
        $sample = Read-Table cs admin "SELECT TOP 5 p.SERIALNO FROM dbo.PURCHASE AS p WHERE LEN(p.SERIALNO) = 16 AND p.PURCHASEDDATE < GETDATE() AND EXISTS (SELECT 1 FROM dbo.ModelWarranty AS w WHERE w.ModelNo = p.MODELNO) ORDER BY p.SERIALNO"
        Assert ($sample.Rows.Count -gt 0) 'no serials sampled'
        $script:Serials = @($sample.Rows | ForEach-Object { [string] $_['SERIALNO'] })
        "($($script:Serials.Count) serials)"
    }
    if ($script:Failed -gt 0) { return }
    $script:SnapshotLines = @("serials`t$($script:Serials -join ',')")
    $calls = Snapshot-Calls $script:Serials
    foreach ($key in $calls.Keys) {
        Check "hashed $key" {
            $table = Read-Table cs dab $calls[$key][0] $calls[$key][1]
            $script:SnapshotLines += "$key`t$(Hash-Of $table)"
            "($($table.Rows.Count) rows)"
        }
    }
    if ($script:Failed -eq 0) { Set-Content -Path $SnapshotFile -Value $script:SnapshotLines -Encoding UTF8 }
}

function Snapshot-After {
    if (-not (Test-Path $SnapshotFile)) {
        $script:Failed++
        Write-Output 'FAIL  no snapshot.txt: run snapshot-before first'
        return
    }
    $before = @{}
    $serials = @()
    foreach ($line in Get-Content -Path $SnapshotFile -Encoding UTF8) {
        $parts = $line -split "`t", 2
        if ($parts[0] -eq 'serials') { $serials = $parts[1] -split ',' } else { $before[$parts[0]] = $parts[1] }
    }
    $calls = Snapshot-Calls $serials
    foreach ($key in $calls.Keys) {
        Check "unchanged: $key" {
            Assert ($before.ContainsKey($key)) 'not in snapshot.txt'
            $table = Read-Table cs dab $calls[$key][0] $calls[$key][1]
            $hash = Hash-Of $table
            Assert ($hash -eq $before[$key]) "was $($before[$key]), now $hash"
            "($($table.Rows.Count) rows)"
        }
    }
    # snapshot.txt holds real serials, so it goes once the comparison has passed.
    if ($script:Failed -eq 0) { Remove-Item $SnapshotFile }
}

function Test-CustService {
    try {
        $unit = (Read-Table cs admin "SELECT TOP 1 SERIALNO, PHONE, EMAIL FROM dbo.PURCHASE WHERE LEN(SERIALNO) = 16 AND PHONE LIKE '[2-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9]' AND EMAIL LIKE '%_@_%._%' AND PURCHASEDDATE < GETDATE() AND (SELECT COUNT(*) FROM dbo.PURCHASE AS d WHERE d.SERIALNO = dbo.PURCHASE.SERIALNO) = 1 AND EXISTS (SELECT 1 FROM dbo.SERVICEDESCRIPTION AS sd JOIN dbo.OrderTable AS ot ON ot.ServiceId = sd.SERVICEID WHERE sd.SERIALNO = dbo.PURCHASE.SERIALNO) AND PHONE <> REPLICATE(LEFT(PHONE, 1), 10) AND PHONE NOT IN ('1234567890', '0123456789') AND (SELECT COUNT(*) FROM dbo.PURCHASE AS q WHERE q.EMAIL = dbo.PURCHASE.EMAIL) <= 3 AND LEFT(EMAIL, CHARINDEX('@', EMAIL + '@') - 1) NOT IN ('none', 'na', 'noemail', 'no') ORDER BY PURCHASEDDATE DESC").Rows[0]
        $serial = [string] $unit['SERIALNO']
        $phone = [string] $unit['PHONE']
        $email = ([string] $unit['EMAIL']).Trim()
        $loose = (Read-Table cs admin "SELECT TOP 1 o.SN FROM dbo.PeachtreeSNOther AS o WHERE LEN(o.SN) = 16 AND NOT EXISTS (SELECT 1 FROM dbo.PURCHASE AS p WHERE p.SERIALNO = o.SN) AND EXISTS (SELECT 1 FROM dbo.MODEL AS m WHERE m.MODELNO = LEFT(o.SN, 6)) ORDER BY o.InputTime DESC").Rows[0]
        $looseSerial = [string] $loose['SN']
        $part = (Read-Table cs admin 'SELECT TOP 1 SP_NO, DEALER_PRICE FROM dbo.Spareparts WHERE DEALER_PRICE > 0 ORDER BY SP_NO').Rows[0]
        $order = (Read-Table cs admin "SELECT TOP 1 CONVERT(varchar(30), ServiceId) + '-' + CONVERT(varchar(30), OrderId) AS OrderNumber, ServiceId FROM dbo.OrderTable WHERE OrderDate < GETDATE() ORDER BY OrderDate DESC").Rows[0]
    }
    catch {
        $script:Failed++
        $sqlError = Find-SqlException $_.Exception
        if ($sqlError) { Write-Output "FAIL  sample (SQL error $($sqlError.Number))" }
        else { Write-Output 'FAIL  sample (could not read the sample rows)' }
        return
    }

    Check 'check_unit returns a registered unit' {
        $t = Read-Table cs dab 'EXEC agent.check_unit @SerialNo = @SerialNo' @{ SerialNo = $serial }
        Assert ($t.Rows.Count -ge 1) 'no rows'
        Assert ([int] $t.Rows[0]['Registered'] -eq 1) 'Registered is not 1'
        "($($t.Rows.Count) rows)"
    }
    Check 'check_unit returns a unit nobody registered, with Registered 0' {
        $t = Read-Table cs dab 'EXEC agent.check_unit @SerialNo = @SerialNo' @{ SerialNo = $looseSerial }
        Assert ($t.Rows.Count -eq 1) "expected 1 row, got $($t.Rows.Count)"
        Assert ([int] $t.Rows[0]['Registered'] -eq 0) 'Registered is not 0'
        Assert ([string] $t.Rows[0]['ModelNo'] -eq $looseSerial.Substring(0, 6)) 'ModelNo is not the first six digits'
        '(1 row)'
    }
    Check 'check_unit returns no personal columns' { Expect-NoPersonal cs "EXEC agent.check_unit @SerialNo = N'1'" }
    Check 'my_unit with a wrong phone returns nothing' {
        $t = Read-Table cs dab 'EXEC agent.my_unit @SerialNo = @SerialNo, @Phone = @Phone' @{ SerialNo = $serial; Phone = '0000000000' }
        Assert ($t.Rows.Count -eq 0) "got $($t.Rows.Count) rows"
        '(0 rows)'
    }
    Check 'my_unit with a wrong email returns nothing' {
        $t = Read-Table cs dab 'EXEC agent.my_unit @SerialNo = @SerialNo, @Email = @Email' @{ SerialNo = $serial; Email = 'nobody@example.invalid' }
        Assert ($t.Rows.Count -eq 0) "got $($t.Rows.Count) rows"
        '(0 rows)'
    }
    Check 'my_unit with the phone on file, dashed, returns the owner' {
        $dashed = '{0}-{1}-{2}' -f $phone.Substring(0, 3), $phone.Substring(3, 3), $phone.Substring(6)
        $t = Read-Table cs dab 'EXEC agent.my_unit @SerialNo = @SerialNo, @Phone = @Phone' @{ SerialNo = $serial; Phone = $dashed }
        Assert ($t.Rows.Count -ge 1) 'no rows'
        Assert ([string] $t.Rows[0]['RowKind'] -eq 'unit') 'first row is not the unit'
        Assert ([string] $t.Rows[0]['SerialNo'] -eq $serial) 'another serial came back'
        "($($t.Rows.Count) rows)"
    }
    Check 'my_unit with the email on file, in capitals, returns the owner' {
        $t = Read-Table cs dab 'EXEC agent.my_unit @SerialNo = @SerialNo, @Email = @Email' @{ SerialNo = $serial; Email = $email.ToUpperInvariant() }
        Assert ($t.Rows.Count -ge 1) 'no rows'
        Assert ([string] $t.Rows[0]['RowKind'] -eq 'unit') 'first row is not the unit'
        "($($t.Rows.Count) rows)"
    }
    Check 'my_unit with neither a phone nor an email is refused' {
        Expect-Refused cs 'EXEC agent.my_unit @SerialNo = @SerialNo' @{ SerialNo = $serial } 51111
    }
    Check 'dealer_prices matches the catalogue' {
        $t = Read-Table cs dab 'EXEC agent.dealer_prices @PartNos = @PartNos' @{ PartNos = "$($part['SP_NO']), NO-SUCH-PART" }
        Assert ($t.Rows.Count -eq 2) "expected 2 rows, got $($t.Rows.Count)"
        $hit = @($t.Rows | Where-Object { [string] $_['SpNo'] -eq [string] $part['SP_NO'] })[0]
        Assert ([int] $hit['Found'] -eq 1) 'the part was not found'
        Assert ([decimal] $hit['DealerPrice'] -eq [decimal] $part['DEALER_PRICE']) 'DealerPrice differs from Spareparts.DEALER_PRICE'
        $miss = @($t.Rows | Where-Object { [string] $_['SpNo'] -eq 'NO-SUCH-PART' })[0]
        Assert ([int] $miss['Found'] -eq 0) 'a missing part was found'
        '(2 rows)'
    }
    Check 'order_status finds an order by its number' {
        $t = Read-Table cs dab 'EXEC agent.order_status @OrderNumber = @OrderNumber' @{ OrderNumber = [string] $order['OrderNumber'] }
        Assert ($t.Rows.Count -ge 1) 'no rows'
        Assert ([string] $t.Rows[0]['OrderNumber'] -eq [string] $order['OrderNumber']) 'another order came back'
        "($($t.Rows.Count) rows)"
    }
    Check 'order_status refuses an order number with no dash' {
        Expect-Refused cs 'EXEC agent.order_status @OrderNumber = @OrderNumber' @{ OrderNumber = [string] $order['ServiceId'] } 51121
    }
    Check 'order_status returns no personal or money columns' {
        Expect-NoPersonal cs "EXEC agent.order_status @OrderNumber = N'1-1'" @('Freight', 'HandlingFee', 'Tax', 'PartsAmount', 'LaborFee', 'TripFee', 'SaleAmount')
    }
    Check 'order_status by serial returns rows' {
        $t = Read-Table cs dab 'EXEC agent.order_status @SerialNo = @SerialNo' @{ SerialNo = $serial }
        Assert ($t.Rows.Count -ge 1) 'no rows'
        "($($t.Rows.Count) rows)"
    }
    Check 'order_status with both an order number and a serial is refused' {
        Expect-Refused cs 'EXEC agent.order_status @OrderNumber = @OrderNumber, @SerialNo = @SerialNo' @{ OrderNumber = [string] $order['OrderNumber']; SerialNo = $serial } 51120
    }
    Check 'order_status refuses an order number that ends in a dash' {
        Expect-Refused cs 'EXEC agent.order_status @OrderNumber = @OrderNumber' @{ OrderNumber = "$($order['ServiceId'])-" } 51121
    }
    Check 'dealer_prices returns no cost column' {
        $columns = Columns-Of cs "EXEC agent.dealer_prices @PartNos = N'1'"
        Assert ($columns.Count -gt 0) 'no columns described'
        Assert (@($columns | Where-Object { $_ -match 'cost' }).Count -eq 0) 'a cost column'
        "($($columns.Count) columns)"
    }
    Check 'dealer_prices refuses more than 50 part numbers' {
        Expect-Refused cs 'EXEC agent.dealer_prices @PartNos = @PartNos' @{ PartNos = ((1..51 | ForEach-Object { "P$_" }) -join ',') } 51131
    }
    Check 'search_parts carries RetailPrice' {
        $columns = Columns-Of cs "EXEC agent.search_parts @ModelNo = N'1'"
        Assert ($columns -contains 'RetailPrice') 'no RetailPrice column'
        "($($columns.Count) columns)"
    }
    Check 'core functions are closed to spiritai_dab' { Expect-Denied cs dab "SELECT * FROM core.unit_warranty('1')" }
}

function Test-Sage {
    try {
        $sample = (Read-Table sage admin "SELECT TOP 1 ls.LotSerialNo, h.InvoiceNo FROM dbo.AR_InvoiceHistoryLotSerial AS ls JOIN dbo.AR_InvoiceHistoryHeader AS h ON h.InvoiceNo = ls.InvoiceNo AND h.HeaderSeqNo = ls.HeaderSeqNo WHERE LEN(ls.LotSerialNo) = 16 AND h.InvoiceNo LIKE '0%' AND h.InvoiceDate < GETDATE() ORDER BY h.InvoiceDate DESC").Rows[0]
        $serial = [string] $sample['LotSerialNo']
        $invoice = ([string] $sample['InvoiceNo']).Trim()
    }
    catch {
        $script:Failed++
        $sqlError = Find-SqlException $_.Exception
        if ($sqlError) { Write-Output "FAIL  sample (SQL error $($sqlError.Number))" }
        else { Write-Output 'FAIL  sample (could not read the sample rows)' }
        return
    }

    Check 'get_invoice finds the invoice that shipped a serial' {
        $t = Read-Table sage dab 'EXEC agent.get_invoice @SerialNo = @SerialNo' @{ SerialNo = $serial }
        Assert ($t.Rows.Count -ge 1) 'no rows'
        $marked = @($t.Rows | Where-Object { [string] $_['RowKind'] -eq 'line' -and [int] $_['HasSerial'] -eq 1 })
        Assert ($marked.Count -ge 1) 'no line has HasSerial 1'
        "($($t.Rows.Count) rows)"
    }
    Check 'get_invoice finds an invoice number given without its leading zeros' {
        $t = Read-Table sage dab 'EXEC agent.get_invoice @InvoiceNo = @InvoiceNo' @{ InvoiceNo = $invoice.TrimStart('0') }
        Assert ($t.Rows.Count -ge 1) 'no rows'
        Assert (([string] $t.Rows[0]['InvoiceNo']).Trim() -eq $invoice) 'another invoice came back'
        "($($t.Rows.Count) rows)"
    }
    Check 'get_invoice returns no personal columns and no customer PO' { Expect-NoPersonal sage "EXEC agent.get_invoice @InvoiceNo = N'1'" @('CustomerPONo') }
    Check 'get_invoice and get_invoice_full return no cost column' {
        foreach ($proc in 'get_invoice', 'get_invoice_full') {
            $columns = Columns-Of sage "EXEC agent.$proc @InvoiceNo = N'1'"
            Assert ($columns.Count -gt 0) "$proc described no columns"
            Assert (@($columns | Where-Object { $_ -match 'cost' }).Count -eq 0) "$proc has a cost column"
        }
        '(none)'
    }
    Check 'get_invoice_full carries the bill-to' {
        $columns = Columns-Of sage "EXEC agent.get_invoice_full @InvoiceNo = N'1'"
        Assert ($columns -contains 'BillToName') 'no BillToName column'
        "($($columns.Count) columns)"
    }
    Check 'get_invoice refuses two handles at once' {
        Expect-Refused sage 'EXEC agent.get_invoice @InvoiceNo = @InvoiceNo, @SerialNo = @SerialNo' @{ InvoiceNo = $invoice; SerialNo = $serial } 51200
    }
    Check 'core functions are closed to spiritai_dab' { Expect-Denied sage dab "SELECT * FROM core.invoice_rows(NULL, NULL, NULL, '1', 1)" }
}

switch ($Part) {
    'logins'          { Test-Logins }
    'snapshot-before' { Snapshot-Before }
    'snapshot-after'  { Snapshot-After }
    'custservice'     { Test-CustService }
    'sage'            { Test-Sage }
}

Write-Output "$script:Failed failed"
if ($script:Failed -gt 0) { exit 1 }
exit 0
