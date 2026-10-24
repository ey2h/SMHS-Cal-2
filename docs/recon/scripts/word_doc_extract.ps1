# word_doc_extract.ps1
# Extract text + tables from the .doc files under 汇宝...\规范 (and any .doc in its subdirs).
# Source files are only READ: each .doc is copied to docs\recon\specs\raw and the copy is opened
# read-only through Word COM. Nothing is saved back, the source tree is never modified.
$ErrorActionPreference = 'Continue'
try { [Console]::OutputEncoding = [System.Text.Encoding]::UTF8 } catch {}

$src       = 'C:\Program Files (x86)\汇宝幕墙门窗结构计算软件V35.0\规范'
$specs     = 'C:\code\Glass\docs\recon\specs'
$raw       = Join-Path $specs 'raw'
$tablesDir = Join-Path $specs 'tables'
$reports   = 'C:\code\Glass\docs\recon\reports'
$outJson   = Join-Path $reports 'word_doc_stats.json'

foreach ($d in @($specs, $raw, $tablesDir, $reports)) {
  if (-not (Test-Path -LiteralPath $d)) { New-Item -ItemType Directory -Force -Path $d | Out-Null }
}

function Write-Utf8Bom([string]$path, [string]$text) {
  $enc = New-Object System.Text.UTF8Encoding($true)
  [System.IO.File]::WriteAllText($path, $text, $enc)
}
function Write-Utf8NoBom([string]$path, [string]$text) {
  $enc = New-Object System.Text.UTF8Encoding($false)
  [System.IO.File]::WriteAllText($path, $text, $enc)
}

# Word control characters -> readable text. Chr(7)=cell end, Chr(13)=paragraph mark,
# Chr(12)=page break, Chr(11)=column break, Chr(18/19/20/21)=field begin/separate/end.
function Clean-WordText([string]$s, [bool]$cellMarksAsColumns) {
  if ($null -eq $s) { return '' }
  $s = $s.Replace(([string][char]13 + [string][char]7), $(if ($cellMarksAsColumns) { ' | ' } else { "`n" }))
  $s = $s.Replace([string][char]7, '')
  $s = $s.Replace([string][char]13, "`n")
  $s = $s.Replace([string][char]12, ' [分页符] ')
  $s = $s.Replace([string][char]11, ' [分栏符] ')
  $s = $s.Replace([string][char]18, '')
  $s = $s.Replace([string][char]19, '')
  $s = $s.Replace([string][char]20, ' | ')
  $s = $s.Replace([string][char]21, '')
  $s = $s.Replace([string][char]9, ' ')
  $s = $s.Replace([string][char]160, ' ')
  $s = ($s -split "`n" | ForEach-Object { $_.TrimEnd() }) -join "`n"
  return $s
}

function Csv-Escape([string]$s) {
  if ($null -eq $s) { return '' }
  $s = $s.Replace("`r`n", ' / ').Replace("`r", ' / ').Replace("`n", ' / ')
  if ($s -match '[",]') { return '"' + $s.Replace('"', '""') + '"' }
  return $s
}

$docFiles = @()
$docFiles += Get-ChildItem -LiteralPath $src -Filter '*.doc' -File | ForEach-Object {
  New-Object psobject -Property ([ordered]@{ Name = $_.Name; Path = $_.FullName; Group = '规范'; Bytes = $_.Length })
}
foreach ($sub in Get-ChildItem -LiteralPath $src -Directory) {
  foreach ($x in (Get-ChildItem -LiteralPath $sub.FullName -Filter '*.doc' -File -ErrorAction SilentlyContinue)) {
    $docFiles += New-Object psobject -Property ([ordered]@{ Name = $x.Name; Path = $x.FullName; Group = $sub.Name; Bytes = $x.Length })
  }
}
Write-Host ('.doc files found: ' + $docFiles.Count)
$docFiles | ForEach-Object { Write-Host ('  [' + $_.Group + '] ' + $_.Name + ' ' + $_.Bytes + ' bytes') }

$word = New-Object -ComObject Word.Application
$word.Visible = $false
$word.DisplayAlerts = 0
try { $word.ScreenUpdating = $false } catch {}

$results = @()

foreach ($f in $docFiles) {
  Write-Host ('--- processing ' + $f.Name)
  $rec = [ordered]@{
    group        = $f.Group
    sourceFile   = $f.Name
    sourcePath   = $f.Path
    sourceBytes  = $f.Bytes
    copyPath     = $null
    opened       = $false
    error        = $null
    paragraphs   = $null
    words        = $null
    characters   = $null
    lines        = $null
    pages        = $null
    tablesFound  = 0
    tableDetails = @()
    bodyTextLines = 0
    bodyTextChars  = 0
    storyRanges    = @()
    textFrames     = @()
    inlineShapes   = 0
    shapes         = 0
    mdOut          = $null
  }
  $copyName = (($f.Group + '__' + $f.Name) -replace '[\\/]', '_')
  $copy = Join-Path $raw $copyName
  try {
    Copy-Item -LiteralPath $f.Path -Destination $copy -Force
    $rec.copyPath = $copy
    $doc = $word.Documents.Open($copy, $false, $true)
    $rec.opened = $true
    Write-Host ('  opened read-only: ' + $copy)

    $rec.tablesFound = $doc.Tables.Count
    $rec.paragraphs = $doc.Paragraphs.Count
    try { $rec.words = $doc.ComputeStatistics(0) } catch {}
    try { $rec.lines = $doc.ComputeStatistics(1) } catch {}
    try { $rec.pages = $doc.ComputeStatistics(2) } catch {}
    try { $rec.characters = $doc.ComputeStatistics(3) } catch {}
    try { $rec.inlineShapes = $doc.InlineShapes.Count } catch {}
    try { $rec.shapes = $doc.Shapes.Count } catch {}
    Write-Host ('  paragraphs=' + $rec.paragraphs + ' tables=' + $doc.Tables.Count + ' words=' + $rec.words + ' pages=' + $rec.pages)

    $md = New-Object System.Text.StringBuilder
    [void]$md.AppendLine('# ' + [System.IO.Path]::GetFileNameWithoutExtension($f.Name) + '.doc（源：' + $f.Path + '）')
    [void]$md.AppendLine('')
    [void]$md.AppendLine('- 源字节数：' + $f.Bytes)
    [void]$md.AppendLine('- 抽取方式：源文件复制为只读副本（specs\raw\' + $copyName + '），Word COM `Documents.Open(path, ConfirmConversions=false, ReadOnly=true)`，抽取后 `Close(0)`（不保存）。源文件未被修改。')
    [void]$md.AppendLine('- 段落数：' + $rec.paragraphs + '；字数：' + $rec.words + '；字符数：' + $rec.characters + '；行数：' + $rec.lines + '；页数：' + $rec.pages)
    [void]$md.AppendLine('- Word 表格数：' + $rec.tablesFound + '（详见下方“表格清单”与 specs\tables\*.csv）')
    [void]$md.AppendLine('- 内嵌图形(InlineShapes)：' + $rec.inlineShapes + '；浮动图形(Shapes)：' + $rec.shapes)
    [void]$md.AppendLine('- 正文为 Word 主文档流文本，已剔除表格内部文本（表格另以 CSV 结构化导出）；Word 段落标记译为换行，Chr(12)/Chr(11) 译为 [分页符]/[分栏符]。原文未翻译。')
    [void]$md.AppendLine('')

    # ---- collect table positions first ----
    $tblInfo = @()
    $tcount = $doc.Tables.Count
    $rec.tablesFound = $tcount
    for ($i = 1; $i -le $tcount; $i++) {
      try {
        $t = $doc.Tables.Item($i)
        $tblInfo += New-Object psobject -Property ([ordered]@{ item = $i; start = $t.Range.Start; end = $t.Range.End; table = $t })
      } catch {
        Write-Host ('  Tables.Item(' + $i + ') failed: ' + $_.Exception.Message)
        $rec.tableDetails += [ordered]@{ item = $i; error = 'Tables.Item failed: ' + $_.Exception.Message }
      }
    }
    $tblInfo = @($tblInfo | Sort-Object { $_.start })

    # ---- body text: main story minus table ranges ----
    $chunks = @()
    $cursor = 0
    foreach ($ti in $tblInfo) {
      if ($ti.start -gt $cursor) {
        try { $chunkTxt = $doc.Range($cursor, $ti.start).Text; $chunks += (Clean-WordText $chunkTxt $true) } catch {}
      }
      if ($ti.end -gt $cursor) { $cursor = $ti.end }
    }
    try { $tailTxt = $doc.Range($cursor, $doc.Content.End).Text; $chunks += (Clean-WordText $tailTxt $true) } catch {}
    $bodyText = ($chunks -join '').Trim()
    $rec.bodyTextChars = $bodyText.Length
    $rec.bodyTextLines = ($bodyText -split "`n").Count
    [void]$md.AppendLine('## 正文（主文档流，不含表格内部文本）')
    [void]$md.AppendLine('')
    [void]$md.AppendLine($bodyText)

    # ---- other story ranges (headers/footers/footnotes/textboxes) ----
    try {
      $rng = $doc.StoryRanges
      while ($null -ne $rng) {
        $stType = $rng.StoryType
        $rawTxt = $rng.Text
        $txt = Clean-WordText $rawTxt $true
        $flat = ($txt -replace '\s+', ' ').Trim()
        $exc = $flat; if ($exc.Length -gt 400) { $exc = $exc.Substring(0, 400) }
        $rec.storyRanges += [ordered]@{ storyType = $stType; chars = $txt.Length; excerpt = $exc }
        if ($stType -ne 1 -and $txt.Trim().Length -gt 0) {
          [void]$md.AppendLine('')
          [void]$md.AppendLine('## StoryType=' + $stType + '（页眉/页脚/脚注/尾注/文本框等，非主文档流）')
          [void]$md.AppendLine('')
          [void]$md.AppendLine($txt)
        }
        $rng = $rng.NextStoryRange
      }
    } catch { Write-Host ('  story iteration failed: ' + $_.Exception.Message) }

    try {
      foreach ($sh in $doc.Shapes) {
        try {
          if ($sh.TextFrame.HasText) {
            $shTxt = $sh.TextFrame.TextRange.Text
            $t = Clean-WordText $shTxt $true
            $rec.textFrames += [ordered]@{ name = [string]$sh.Name; chars = $t.Length; text = $t }
            [void]$md.AppendLine('')
            [void]$md.AppendLine('## 文本框 ' + $sh.Name)
            [void]$md.AppendLine('')
            [void]$md.AppendLine($t)
          }
        } catch {}
      }
    } catch { Write-Host ('  shapes iteration failed: ' + $_.Exception.Message) }

    # ---- tables -> CSV (document order) ----
    $docIndex = 0
    foreach ($ti in $tblInfo) {
      $docIndex++
      $t = $ti.table
      $rowsDeclared = $null; $colsDeclared = $null
      try { $rowsDeclared = $t.Rows.Count } catch {}
      try { $colsDeclared = $t.Columns.Count } catch {}

      $cap = ''
      try {
        $ps = [Math]::Max(0, $ti.start - 400)
        $preRaw = $doc.Range($ps, $ti.start).Text
        $pre = Clean-WordText $preRaw $true
        $flat = ($pre -replace "`n", ' | ' -replace '\s+', ' ').Trim()
        if ($flat.Length -gt 160) { $flat = $flat.Substring($flat.Length - 160) }
        $cap = $flat
      } catch {}

      $grid = @{}
      $maxR = 0; $maxC = 0; $cellsFilled = 0; $rowIdxErr = 0; $colIdxErr = 0; $cellTextErr = 0
      foreach ($cell in $t.Range.Cells) {
        $ri = $null; $ci = $null
        try { $ri = $cell.Row.Index } catch { $rowIdxErr++ }
        try { $ci = $cell.Column.Index } catch { $colIdxErr++ }
        if ($null -eq $ri -or $null -eq $ci) { continue }
        $val = ''
        try { $val = Clean-WordText $cell.Range.Text $false } catch { $cellTextErr++ }
        $grid[[string]$ri + ',' + [string]$ci] = $val
        if ($ri -gt $maxR) { $maxR = $ri }
        if ($ci -gt $maxC) { $maxC = $ci }
        $cellsFilled++
      }

      $lines = New-Object System.Collections.Generic.List[string]
      $nonEmptyRows = 0
      for ($r = 1; $r -le $maxR; $r++) {
        $rowArr = @()
        $hasContent = $false
        for ($c = 1; $c -le $maxC; $c++) {
          $v = ''
          if ($grid.ContainsKey([string]$r + ',' + [string]$c)) { $v = $grid[[string]$r + ',' + [string]$c] }
          if ($v.Trim().Length -gt 0) { $hasContent = $true }
          $rowArr += (Csv-Escape $v)
        }
        $lines.Add(($rowArr -join ','))
        if ($hasContent) { $nonEmptyRows++ }
      }
      $csvName = ('{0}__t{1:D2}_r{2}x{3}.csv' -f ($f.Group + '_' + [System.IO.Path]::GetFileNameWithoutExtension($f.Name)), $docIndex, $maxR, $maxC)
      $csvPath = Join-Path $tablesDir $csvName
      Write-Utf8Bom $csvPath (([string]::Join("`r`n", $lines.ToArray())) + "`r`n")

      $firstRow = @()
      for ($c = 1; $c -le [Math]::Min($maxC, 16); $c++) {
        $v = ''; if ($grid.ContainsKey('1,' + $c)) { $v = $grid['1,' + $c] }
        $firstRow += $v
      }
      # find the first non-empty row too (some Word tables have a blank spacer row 1)
      $firstNonEmptyRow = @(); $firstNonEmptyRowIndex = 0
      for ($r = 1; $r -le $maxR -and $firstNonEmptyRowIndex -eq 0; $r++) {
        $rowVals = @()
        $any = $false
        for ($c = 1; $c -le $maxC; $c++) {
          $v = ''; if ($grid.ContainsKey([string]$r + ',' + [string]$c)) { $v = $grid[[string]$r + ',' + [string]$c] }
          $rowVals += $v
          if ($v.Trim().Length -gt 0) { $any = $true }
        }
        if ($any) { $firstNonEmptyRow = $rowVals; $firstNonEmptyRowIndex = $r }
      }

      $rec.tableDetails += [ordered]@{
        docOrder          = $docIndex
        wordTableItem     = $ti.item
        csv               = $csvPath
        csvName           = $csvName
        rowsDeclared      = $rowsDeclared
        colsDeclared      = $colsDeclared
        gridRows          = $maxR
        gridCols          = $maxC
        cellsFilled       = $cellsFilled
        nonEmptyRows      = $nonEmptyRows
        rowIdxErrors      = $rowIdxErr
        colIdxErrors      = $colIdxErr
        cellTextErrors    = $cellTextErr
        rangeStart        = $ti.start
        captionGuess      = $cap
        firstRowCells     = $firstRow
        firstNonEmptyRow  = $firstNonEmptyRow
        firstNonEmptyRowNumber = $firstNonEmptyRowIndex
      }
      Write-Host ('  table doc#' + $docIndex + ' item#' + $ti.item + ' grid=' + $maxR + 'x' + $maxC + ' filled=' + $cellsFilled + ' nonEmpty=' + $nonEmptyRows + ' declared=' + $rowsDeclared + 'x' + $colsDeclared + ' -> ' + $csvName)
    }

    [void]$md.AppendLine('')
    [void]$md.AppendLine('## 表格清单（按文档出现顺序）')
    [void]$md.AppendLine('')
    foreach ($td in $rec.tableDetails) {
      if ($td.Contains('csv')) {
        [void]$md.AppendLine('- 表 #' + $td.docOrder + '（Word Tables.Item ' + $td.wordTableItem + '）: ' + $td.gridRows + ' 行 × ' + $td.gridCols + ' 列，非空行 ' + $td.nonEmptyRows + '，已填单元格 ' + $td.cellsFilled + '，CSV: specs\tables\' + $td.csvName)
        [void]$md.AppendLine('  - 首个非空行(第 ' + $td.firstNonEmptyRowNumber + ' 行): ' + (($td.firstNonEmptyRow | ForEach-Object { $_ }) -join ' | '))
        if ($td.captionGuess) { [void]$md.AppendLine('  - 表格前文提示: …' + $td.captionGuess) }
      } else {
        [void]$md.AppendLine('- 表 #' + $td.docOrder + ': 抽取失败 ' + $td.error)
      }
    }

    $mdOut = Join-Path $specs ([System.IO.Path]::GetFileNameWithoutExtension($f.Name) + '.doc.md')
    Write-Utf8NoBom $mdOut $md.ToString()
    $rec.mdOut = $mdOut
    $doc.Close(0)
    Write-Host ('  wrote ' + $mdOut)
  } catch {
    $rec.error = $_.Exception.Message
    Write-Host ('  ERROR: ' + $_.Exception.Message)
  }
  $results += New-Object psobject -Property $rec
}

try { $word.Quit() } catch {}
try { [System.Runtime.InteropServices.Marshal]::ReleaseComObject($word) | Out-Null } catch {}

$payload = New-Object psobject -Property ([ordered]@{
  generatedAt = (Get-Date).ToString('o')
  method = 'Word COM: copy source .doc -> specs\raw, Documents.Open(path, ConfirmConversions=$false, ReadOnly=$true), Close(0)=wdDoNotSaveChanges. Source tree never modified.'
  docCount = $results.Count
  docs = $results
})
Write-Utf8Bom $outJson ($payload | ConvertTo-Json -Depth 8)
Write-Host ('wrote ' + $outJson)
