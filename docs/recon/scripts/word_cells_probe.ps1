# word_cells_probe.ps1
# Diagnose how to enumerate Word table cells through COM (which approach actually yields cells).
$ErrorActionPreference = 'Continue'
try { [Console]::OutputEncoding = [System.Text.Encoding]::UTF8 } catch {}
$src = 'C:\Program Files (x86)\汇宝幕墙门窗结构计算软件V35.0\规范\表1.doc'

$word = New-Object -ComObject Word.Application
$word.Visible = $false
$word.DisplayAlerts = 0
$doc = $word.Documents.Open($src, $false, $true)
Write-Host ('opened: ' + $src + ' tables=' + $doc.Tables.Count)

$t = $doc.Tables.Item(1)
Write-Host ('--- Table 1: Rows.Count=' + $t.Rows.Count + ' Columns.Count=' + $t.Columns.Count)

# A: Range.Cells.Count
try { Write-Host ('A. $t.Range.Cells.Count = ' + $t.Range.Cells.Count) } catch { Write-Host ('A failed: ' + $_.Exception.Message) }

# B: foreach over Range.Cells
$n = 0; $err = 0; $sample = 0
try {
  foreach ($c in $t.Range.Cells) {
    $n++
    if ($sample -lt 5) {
      $sample++
      $ri = '?'; $ci = '?'; $txt = '?'
      try { $ri = $c.Row.Index } catch { $ri = 'ERR:' + $_.Exception.Message }
      try { $ci = $c.Column.Index } catch { $ci = 'ERR:' + $_.Exception.Message }
      try { $txt = ($c.Range.Text -replace ([string][char]13), '\r' -replace ([string][char]7), '\a') } catch { $txt = 'ERR' }
      Write-Host ('   cell#' + $n + ' Row.Index=' + $ri + ' Column.Index=' + $ci + ' Text=' + $txt)
    }
  }
} catch { $err++; Write-Host ('B foreach threw: ' + $_.Exception.Message) }
Write-Host ('B. foreach($c in $t.Range.Cells) iterations=' + $n + ' errors=' + $err)

# C: Cells.Item(i)
try {
  $cnt = $t.Range.Cells.Count
  $got = 0
  for ($i = 1; $i -le [Math]::Min($cnt, 8); $i++) {
    try {
      $c = $t.Range.Cells.Item($i)
      $got++
      Write-Host ('   C.Item(' + $i + ') Row=' + $c.Row.Index + ' Col=' + $c.Column.Index + ' Text=' + (($c.Range.Text -replace ([string][char]13), '\r' -replace ([string][char]7), '\a')))
    } catch { Write-Host ('   C.Item(' + $i + ') failed: ' + $_.Exception.Message) }
  }
  Write-Host ('C. Items resolved=' + $got + ' of ' + $cnt)
} catch { Write-Host ('C failed: ' + $_.Exception.Message) }

# D: nested Cell(r,c)
try {
  $ok = 0; $bad = 0
  for ($r = 1; $r -le $t.Rows.Count; $r++) {
    for ($c = 1; $c -le $t.Columns.Count; $c++) {
      try { $null = $t.Cell($r, $c); $ok++ } catch { $bad++ }
    }
  }
  Write-Host ('D. $t.Cell(r,c) ok=' + $ok + ' fail=' + $bad)
  $c11 = $t.Cell(1, 1).Range.Text
  Write-Host ('   D sample Cell(1,1)=' + ($c11 -replace ([string][char]13), '\r' -replace ([string][char]7), '\a'))
} catch { Write-Host ('D failed: ' + $_.Exception.Message) }

# E: per-Row Cells enumeration
try {
  $rowCells = $t.Rows.Item(1).Cells.Count
  Write-Host ('E. Rows.Item(1).Cells.Count=' + $rowCells)
  $k = 0
  foreach ($c in $t.Rows.Item(1).Cells) {
    $k++
    if ($k -le 5) { Write-Host ('   E cell Column=' + $c.Column.Index + ' Row=' + $c.Row.Index + ' Text=' + (($c.Range.Text -replace ([string][char]13), '\r' -replace ([string][char]7), '\a'))) }
  }
  Write-Host ('E. enumerated=' + $k)
  for ($r = 1; $r -le $t.Rows.Count; $r++) { Write-Host ('   row ' + $r + ' cells=' + $t.Rows.Item($r).Cells.Count) }
} catch { Write-Host ('E failed: ' + $_.Exception.Message) }

# F: whole table Range.Text with control chars revealed
try {
  $tt = $t.Range.Text
  $sb = New-Object System.Text.StringBuilder
  $limit = [Math]::Min($tt.Length, 400)
  for ($i = 0; $i -lt $limit; $i++) {
    $ch = $tt[$i]
    $code = [int]$ch
    if ($code -eq 7) { [void]$sb.Append('<7>') }
    elseif ($code -eq 13) { [void]$sb.Append('<13>') }
    elseif ($code -eq 10) { [void]$sb.Append('<10>') }
    elseif ($code -lt 32) { [void]$sb.Append('<' + $code + '>') }
    else { [void]$sb.Append($ch) }
  }
  Write-Host ('F. Table1.Range.Text length=' + $tt.Length)
  Write-Host ('F. first 400 chars: ' + $sb.ToString())
  $count7 = ([regex]::Matches($tt, [string][char]7)).Count
  $count13 = ([regex]::Matches($tt, [string][char]13)).Count
  Write-Host ('F. Chr7 count=' + $count7 + ' Chr13 count=' + $count13)
} catch { Write-Host ('F failed: ' + $_.Exception.Message) }

# G: row/cell structure via Rows.Item(r).Cells.Item(c)
try {
  $c = $t.Rows.Item(1).Cells.Item(1)
  Write-Host ('G. Rows.Item(1).Cells.Item(1).Range.Text=' + (($c.Range.Text -replace ([string][char]13), '\r' -replace ([string][char]7), '\a')))
} catch { Write-Host ('G failed: ' + $_.Exception.Message) }

$doc.Close(0)
$word.Quit()
Write-Host 'probe done'
