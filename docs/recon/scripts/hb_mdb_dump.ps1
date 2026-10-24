# Dump every table of Huibao's 参数库.mdb through OLEDB into CSV + an index JSON.
param(
    [string]$Src = 'C:\Program Files (x86)\汇宝幕墙门窗结构计算软件V35.0\jss\参数库.mdb',
    [string]$Out = 'D:\Code\333\hb_mdb'
)
$ErrorActionPreference = 'Stop'
New-Item -ItemType Directory -Force -Path $Out | Out-Null

$conns = @(
    'Provider=Microsoft.ACE.OLEDB.12.0;Data Source={0};',
    'Provider=Microsoft.ACE.OLEDB.16.0;Data Source={0};',
    'Provider=Microsoft.Jet.OLEDB.4.0;Data Source={0};'
)
$conn = $null
foreach ($c in $conns) {
    try {
        $conn = New-Object System.Data.OleDb.OleDbConnection($c -f $Src)
        $conn.Open()
        Write-Host ("PROVIDER OK: " + $c.Split(';')[0])
        break
    } catch { Write-Host ("  provider fail: " + $_.Exception.Message.Split("`n")[0]) ; $conn = $null }
}
if (-not $conn) { throw 'no OLEDB provider could open the mdb' }

$tables = $conn.GetOleDbSchemaTable([System.Data.OleDb.OleDbSchemaGuid]::Tables, $null)
$index = @()
foreach ($t in $tables) {
    $name = $t['TABLE_NAME']
    $type = $t['TABLE_TYPE']
    if ($type -ne 'TABLE') { continue }
    $cols = $conn.GetOleDbSchemaTable([System.Data.OleDb.OleDbSchemaGuid]::Columns, [object[]]@($null, $null, $name, $null))
    $colNames = @()
    $colTypes = @()
    foreach ($r in $cols) {
        $colNames += [string]$r['COLUMN_NAME']
        $colTypes += [string]$r['DATA_TYPE']
    }
    $ad = New-Object System.Data.OleDb.OleDbDataAdapter("SELECT * FROM [$name]", $conn)
    $dt = New-Object System.Data.DataTable
    [void]$ad.Fill($dt)
    $ad.Dispose()
    $path = Join-Path $Out ($name + '.csv')
    $dt | Export-Csv -Path $path -NoTypeInformation -Encoding UTF8
    $index += [pscustomobject]@{ table = $name; rows = $dt.Rows.Count; columns = $colNames; types = $colTypes }
    Write-Host ("{0,-26} rows={1,-6} cols={2}" -f $name, $dt.Rows.Count, ($colNames -join ','))
}
$conn.Close()
$index | ConvertTo-Json -Depth 5 | Set-Content -Path (Join-Path $Out '_index.json') -Encoding UTF8
Write-Host ("TABLES=" + $index.Count + "  OUT=" + $Out)
