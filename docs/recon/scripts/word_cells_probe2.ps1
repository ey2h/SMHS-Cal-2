# word_cells_probe2.ps1
# Find a PowerShell-reliable way to get each Word Cell's Row/Column index, and learn how merged
# positions behave for Table.Cell(r,c).
$ErrorActionPreference = 'Continue'
try { [Console]::OutputEncoding = [System.Text.Encoding]::UTF8 } catch {}
$src = 'C:\code\Glass\docs\recon\specs\raw\规范__表1.doc'

$word = New-Object -ComObject Word.Application
$word.Visible = $false
$word.DisplayAlerts = 0
$doc = $word.Documents.Open($src, $false, $true)
$t = $doc.Tables.Item(1)
Write-Host ('table 1: rows=' + $t.Rows.Count + ' cols=' + $t.Columns.Count + ' rangeCells=' + $t.Range.Cells.Count)

Write-Host '--- index accessor variants (first 8 cells) ---'
$i = 0
foreach ($c in $t.Range.Cells) {
  $i++
  if ($i -gt 8) { break }
  $txt = (($c.Range.Text) -replace ([string][char]13), '\r' -replace ([string][char]7), '\a')
  $r1 = try { [string]($c.Row.Index) } catch { 'ERR:' + $_.Exception.Message }
  $r2 = try { [string](($c.Row).Index) } catch { 'ERR:' + $_.Exception.Message }
  $r3 = try { [string]($c | Select-Object -ExpandProperty Row -ErrorAction Stop | Select-Object -ExpandProperty Index -ErrorAction Stop) } catch { 'ERR3:' + $_.Exception.Message }
  $r4 = try { [string]$c.RowIndex } catch { 'ERR:' + $_.Exception.Message }
  $c1 = try { [string]$c.Column.Index } catch { 'ERR:' + $_.Exception.Message }
  $c2 = try { [string]$c.ColumnIndex } catch { 'ERR:' + $_.Exception.Message }
  $c3 = try { [string]$c.Index } catch { 'ERR:' + $_.Exception.Message }
  $rowCellsCount = try { [string]($c.Row.Cells.Count) } catch { 'ERR:' + $_.Exception.Message }
  $colCellsCount = try { [string]($c.Column.Cells.Count) } catch { 'ERR:' + $_.Exception.Message }
  Write-Host ('#' + $i + ' Row.Index=[' + $r1 + '] (Row).Index=[' + $r2 + '] select=[' + $r3 + '] RowIndex=[' + $r4 + '] Column.Index=[' + $c1 + '] ColumnIndex=[' + $c2 + '] Index=[' + $c3 + '] row.cells.count=' + $rowCellsCount + ' col.cells.count=' + $colCellsCount + ' text=' + $txt)
}

Write-Host '--- Cell(r,c) behaviour per grid position ---'
$R = $t.Rows.Count; $C = $t.Columns.Count
for ($r = 1; $r -le $R; $r++) {
  $line = @()
  for ($c = 1; $c -le $C; $c++) {
    try {
      $cell = $t.Cell($r, $c)
      $txt = ($cell.Range.Text -replace ([string][char]13) -replace ([string][char]7) )
      if ($txt.Length -gt 14) { $txt = $txt.Substring(0, 14) }
      $line += ('[' + $r + ',' + $c + ']=OK:' + $txt)
    } catch {
      $line += ('[' + $r + ',' + $c + ']=FAIL:' + $_.Exception.Message)
    }
  }
  Write-Host ($line -join '  ')
}

Write-Host '--- linear Range.Cells order with (Row).Index/(Column).Index ---'
$i = 0
foreach ($c in $t.Range.Cells) {
  $i++
  $ri = try { ($c.Row).Index } catch { 'x' }
  $ci = try { ($c.Column).Index } catch { 'x' }
  $txt = ($c.Range.Text -replace ([string][char]13) -replace ([string][char]7) )
  if ($txt.Length -gt 20) { $txt = $txt.Substring(0, 20) }
  Write-Host ($i.ToString() + ' r=' + $ri + ' c=' + $ci + ' start=' + $c.Range.Start + ' text=' + $txt)
}

Write-Host '--- Rows.Item / Columns.Item availability ---'
try { Write-Host ('Rows.Item(1).Cells.Count=' + $t.Rows.Item(1).Cells.Count) } catch { Write-Host ('Rows.Item(1) FAIL: ' + $_.Exception.Message) }
for ($r = 1; $r -le $R; $r++) {
  try { Write-Host ('  row ' + $r + ' cells=' + $t.Rows.Item($r).Cells.Count) } catch { Write-Host ('  row ' + $r + ' FAIL: ' + $_.Exception.Message) }
}
try { Write-Host ('Columns.Item(1).Cells.Count=' + $t.Columns.Item(1).Cells.Count) } catch { Write-Host ('Columns.Item(1) FAIL: ' + $_.Exception.Message) }

$doc.Close(0)
$word.Quit()
Write-Host 'probe2 done'
