# word_doc_extract2.ps1
# Word .doc -> text + CSV tables (v2).
# Table extraction strategy (validated against these documents):
#   1) declared Rows.Count / Columns.Count give the grid size;
#   2) every grid position is probed with Table.Cell(r,c)  (merged-covered positions raise);
#   3) uncovered positions are filled by merge-continuation heuristics and counted;
#   4) a lossless cross-check compares the grid's non-empty cell texts against the linear
#      Table.Range.Text cell sequence (cells are terminated by Chr(13)+Chr(7) in Word);
#      if content differs, an extra "*_linear.csv" from the linear sequence is written.
# Source files are never modified: only copies in specs\raw are opened, read-only, and closed with Close(0).
$ErrorActionPreference = 'Continue'
try { [Console]::OutputEncoding = [System.Text.Encoding]::UTF8 } catch {}

$srcDirs = @(
  @{ Path = 'C:\Program Files (x86)\汇宝幕墙门窗结构计算软件V35.0\规范'; Group = '规范' },
  @{ Path = 'C:\code\Glass\docs\recon\attachments\extracted\节能计算基本原则与汇总表'; Group = '附件节能' }
)
$specs     = 'C:\code\Glass\docs\recon\specs'
$raw       = Join-Path $specs 'raw'
$tablesDir = Join-Path $specs 'tables'
$reports   = 'C:\code\Glass\docs\recon\reports'
$outJson   = Join-Path $reports 'word_doc_stats.json'

foreach ($d in @($specs, $raw, $tablesDir, $reports)) {
  if (-not (Test-Path -LiteralPath $d)) { New-Item -ItemType Directory -Force -Path $d | Out-Null }
}

$encBom = New-Object System.Text.UTF8Encoding($true)
$encNo  = New-Object System.Text.UTF8Encoding($false)

function Write-Utf8Bom([string]$path, [string]$text) { [System.IO.File]::WriteAllText($path, $text, $encBom) }
function Write-Utf8NoBom([string]$path, [string]$text) { [System.IO.File]::WriteAllText($path, $text, $encNo) }

$CH7  = [string][char]7
$CH13 = [string][char]13

function Clean-WordText([string]$s, [bool]$cellMarksAsColumns) {
  if ($null -eq $s) { return '' }
  $s = $s.Replace($CH13 + $CH7, $(if ($cellMarksAsColumns) { ' | ' } else { "`n" }))
  $s = $s.Replace($CH7, '')
  $s = $s.Replace($CH13, "`n")
  $s = $s.Replace([string][char]12, ' [分页符] ')
  $s = $s.Replace([string][char]11, ' [分栏符] ')
  $s = $s.Replace([string][char]18, '')
  $s = $s.Replace([string][char]19, '')
  $s = $s.Replace([string][char]20, ' | ')
  $s = $s.Replace([string][char]21, '')
  $s = $s.Replace([string][char]9, ' ')
  $s = $s.Replace([string][char]160, ' ')
  $s = $s.Replace([string][char]183, ' ')
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
foreach ($sd in $srcDirs) {
  if (-not (Test-Path -LiteralPath $sd.Path)) { Write-Host ('skip missing dir: ' + $sd.Path); continue }
  foreach ($x in (Get-ChildItem -LiteralPath $sd.Path -Filter '*.doc' -File -Recurse -ErrorAction SilentlyContinue)) {
    $docFiles += New-Object psobject -Property ([ordered]@{
      Name  = $x.Name
      Path  = $x.FullName
      Group = $sd.Group
      Bytes = $x.Length
    })
  }
}
Write-Host ('.doc files to process: ' + $docFiles.Count)
$docFiles | ForEach-Object { Write-Host ('  [' + $_.Group + '] ' + $_.Name + ' ' + $_.Bytes + ' bytes') }

$word = New-Object -ComObject Word.Application
$word.Visible = $false
$word.DisplayAlerts = 0
try { $word.ScreenUpdating = $false } catch {}

$results = @()

foreach ($f in $docFiles) {
  Write-Host ('--- processing ' + $f.Name + ' (' + $f.Group + ')')
  $rec = [ordered]@{
    group = $f.Group
    sourceFile = $f.Name
    sourcePath = $f.Path
    sourceBytes = $f.Bytes
    copyPath = $null
    opened = $false
    error = $null
    paragraphs = $null
    words = $null
    characters = $null
    lines = $null
    pages = $null
    inlineShapes = 0
    shapes = 0
    tablesFound = 0
    tableDetails = @()
    bodyTextLines = 0
    bodyTextChars = 0
    storyRanges = @()
    textFrames = @()
    mdOut = $null
  }
  $copyName = (($f.Group + '__' + $f.Name) -replace '[\\/]', '_')
  $copy = Join-Path $raw $copyName
  try {
    Copy-Item -LiteralPath $f.Path -Destination $copy -Force
    $rec.copyPath = $copy
    $doc = $word.Documents.Open($copy, $false, $true)
    $rec.opened = $true
    $rec.tablesFound = $doc.Tables.Count
    $rec.paragraphs = $doc.Paragraphs.Count
    try { $rec.words = $doc.ComputeStatistics(0) } catch {}
    try { $rec.lines = $doc.ComputeStatistics(1) } catch {}
    try { $rec.pages = $doc.ComputeStatistics(2) } catch {}
    try { $rec.characters = $doc.ComputeStatistics(3) } catch {}
    try { $rec.inlineShapes = $doc.InlineShapes.Count } catch {}
    try { $rec.shapes = $doc.Shapes.Count } catch {}
    Write-Host ('  opened read-only; paragraphs=' + $rec.paragraphs + ' tables=' + $rec.tablesFound + ' words=' + $rec.words + ' pages=' + $rec.pages)

    $md = New-Object System.Text.StringBuilder
    [void]$md.AppendLine('# ' + [System.IO.Path]::GetFileNameWithoutExtension($f.Name) + '.doc（源：' + $f.Path + '）')
    [void]$md.AppendLine('')
    [void]$md.AppendLine('- 源字节数：' + $f.Bytes)
    [void]$md.AppendLine('- 抽取方式：源文件只读复制为副本（specs\raw\' + $copyName + '），Word COM `Documents.Open(path, ConfirmConversions=false, ReadOnly=true)`，结束后 `Close(0)`（不保存）。源文件未被修改。')
    [void]$md.AppendLine('- 段落数：' + $rec.paragraphs + '；字数：' + $rec.words + '；字符数：' + $rec.characters + '；行数：' + $rec.lines + '；页数：' + $rec.pages)
    [void]$md.AppendLine('- Word 表格数：' + $rec.tablesFound + '（详见“表格清单”与 specs\tables\*.csv）')
    [void]$md.AppendLine('- 内嵌图形 InlineShapes：' + $rec.inlineShapes + '；浮动图形 Shapes：' + $rec.shapes)
    [void]$md.AppendLine('- 正文为主文档流文本，已剔除表格内部文本（表格另存 CSV）；Chr(13)→换行，Chr(12)/Chr(11)→[分页符]/[分栏符]。原文未翻译。')
    [void]$md.AppendLine('')

    # ---- tables: collect positions first (document order) ----
    $tblInfo = @()
    for ($i = 1; $i -le $rec.tablesFound; $i++) {
      try {
        $t = $doc.Tables.Item($i)
        $tblInfo += New-Object psobject -Property ([ordered]@{ item = $i; start = $t.Range.Start; end = $t.Range.End; table = $t })
      } catch {
        Write-Host ('  Tables.Item(' + $i + ') failed: ' + $_.Exception.Message)
        $rec.tableDetails += [ordered]@{ item = $i; error = 'Tables.Item failed: ' + $_.Exception.Message }
      }
    }
    $tblInfo = @($tblInfo | Sort-Object { $_.start })

    # ---- body text = main story minus table ranges ----
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

    # ---- other story ranges + text frames ----
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
          [void]$md.AppendLine('## StoryType=' + $stType + '（页眉/页脚/脚注/尾注/文本框等非主文档流）')
          [void]$md.AppendLine('')
          [void]$md.AppendLine($txt)
        }
        $rng = $rng.NextStoryRange
      }
    } catch { Write-Host ('  story iteration failed: ' + $_.Exception.Message) }

    try {
      foreach ($sh in $doc.Shapes) {
        try {
          $hasT = $false
          try { $hasT = $sh.TextFrame.HasText } catch { $hasT = $false }
          if ($hasT) {
            $shTxt = $sh.TextFrame.TextRange.Text
            $t2 = Clean-WordText $shTxt $true
            $rec.textFrames += [ordered]@{ name = [string]$sh.Name; chars = $t2.Length; text = $t2 }
            [void]$md.AppendLine('')
            [void]$md.AppendLine('## 文本框 ' + [string]$sh.Name)
            [void]$md.AppendLine('')
            [void]$md.AppendLine($t2)
          }
        } catch {}
      }
    } catch { Write-Host ('  shapes iteration failed: ' + $_.Exception.Message) }

    # ---- tables -> CSV ----
    $docOrder = 0
    foreach ($ti in $tblInfo) {
      $docOrder++
      $t = $ti.table
      $R = $null; $C = $null
      try { $R = $t.Rows.Count } catch {}
      try { $C = $t.Columns.Count } catch {}
      if ($null -eq $R -or $null -eq $C -or $R -le 0 -or $C -le 0) {
        Write-Host ('  table #' + $docOrder + ': Rows/Columns 不可用，改用线性抽取')
        $R = $null; $C = $null
      }

      # linear cell sequence from Table.Range.Text (cells end with Chr13+Chr7)
      $rawTableText = $t.Range.Text
      $linear = @($rawTableText.Split(@($CH13 + $CH7), [System.StringSplitOptions]::None))
      # remove the final empty element produced by the trailing separator
      while ($linear.Count -gt 0 -and $linear[-1] -eq '') { $linear = @($linear[0..($linear.Count - 2)]) }
      $linear = @($linear | ForEach-Object { Clean-WordText $_ $false })
      $linearNonEmpty = @($linear | Where-Object { $_.Trim().Length -gt 0 })

      # grid via Cell(r,c) probing
      $grid = @{}
      $probedOk = 0; $probedFail = 0; $textErr = 0
      if ($null -ne $R -and $null -ne $C) {
        for ($r = 1; $r -le $R; $r++) {
          for ($c = 1; $c -le $C; $c++) {
            try {
              $cell = $t.Cell($r, $c)
              $cellTxt = $cell.Range.Text
              $grid[[string]$r + ',' + [string]$c] = (Clean-WordText $cellTxt $false)
              $probedOk++
            } catch {
              $probedFail++
            }
          }
        }
      }

      # merge-continuation fill for uncovered positions
      $filledHorizontal = 0; $filledVertical = 0
      if ($null -ne $R -and $null -ne $C) {
        for ($r = 1; $r -le $R; $r++) {
          for ($c = 1; $c -le $C; $c++) {
            $key = [string]$r + ',' + [string]$c
            if ($grid.ContainsKey($key)) { continue }
            $leftKey = [string]$r + ',' + [string]($c - 1)
            $upKey   = [string]($r - 1) + ',' + [string]$c
            $leftTxt = ''
            $upTxt = ''
            if ($c -gt 1 -and $grid.ContainsKey($leftKey)) { $leftTxt = $grid[$leftKey] }
            if ($r -gt 1 -and $grid.ContainsKey($upKey)) { $upTxt = $grid[$upKey] }
            if ($leftTxt.Trim().Length -gt 0) {
              $grid[$key] = $leftTxt; $filledHorizontal++
            } elseif ($upTxt.Trim().Length -gt 0) {
              $grid[$key] = $upTxt; $filledVertical++
            } else {
              $grid[$key] = ''
            }
          }
        }
      }

      # lossless cross-check: multiset of non-empty grid texts vs linear non-empty texts
      $gridNonEmpty = @()
      foreach ($v in $grid.Values) { if ($v.Trim().Length -gt 0) { $gridNonEmpty += $v.Trim() } }
      $linSorted = @($linearNonEmpty | ForEach-Object { $_.Trim() } | Sort-Object)
      $gridSorted = @($gridNonEmpty | Sort-Object)
      $sameCount = ($linSorted.Count -eq $gridSorted.Count)
      $contentMatch = $true
      if ($sameCount) {
        for ($k = 0; $k -lt $linSorted.Count; $k++) {
          if ($linSorted[$k] -ne $gridSorted[$k]) { $contentMatch = $false; break }
        }
      } else { $contentMatch = $false }

      $csvFiles = @()
      $csvNameBase = ('{0}__t{1:D2}' -f ($f.Group + '_' + [System.IO.Path]::GetFileNameWithoutExtension($f.Name)), $docOrder)

      if ($null -ne $R -and $null -ne $C) {
        $rowsOut = New-Object System.Collections.Generic.List[string]
        $nonEmptyRows = 0
        for ($r = 1; $r -le $R; $r++) {
          $arr = @()
          $any = $false
          for ($c = 1; $c -le $C; $c++) {
            $v = $grid[[string]$r + ',' + [string]$c]
            if ($v.Trim().Length -gt 0) { $any = $true }
            $arr += (Csv-Escape $v)
          }
          $rowsOut.Add(($arr -join ','))
          if ($any) { $nonEmptyRows++ }
        }
        $csvName = ($csvNameBase + '_r{0}x{1}.csv' -f $R, $C)
        $csvPath = Join-Path $tablesDir $csvName
        Write-Utf8Bom $csvPath (([string]::Join("`r`n", $rowsOut.ToArray())) + "`r`n")
        $csvFiles += $csvPath
      } else {
        $nonEmptyRows = 0
      }

      # linear fallback CSV when the grid did not reproduce the same cell content
      $linearCsv = $null
      if (-not $contentMatch) {
        $maxCols = 0
        if ($null -ne $C) { $maxCols = $C } else { $maxCols = 1 }
        $linRows = New-Object System.Collections.Generic.List[string]
        $i = 0
        while ($i -lt $linear.Count) {
          $take = [Math]::Min($maxCols, $linear.Count - $i)
          $arr = @()
          for ($k = 0; $k -lt $take; $k++) { $arr += (Csv-Escape $linear[$i + $k]) }
          for ($k = $take; $k -lt $maxCols; $k++) { $arr += '' }
          $linRows.Add(($arr -join ','))
          $i += $take
        }
        $linearCsv = Join-Path $tablesDir ($csvNameBase + '_linear.csv')
        Write-Utf8Bom $linearCsv (([string]::Join("`r`n", $linRows.ToArray())) + "`r`n")
        $csvFiles += $linearCsv
      }

      # caption guess (text preceding the table) + first non-empty row of the grid
      $cap = ''
      try {
        $ps = [Math]::Max(0, $ti.start - 400)
        $preRaw = $doc.Range($ps, $ti.start).Text
        $pre = Clean-WordText $preRaw $true
        $flat = ($pre -replace "`n", ' | ' -replace '\s+', ' ').Trim()
        if ($flat.Length -gt 160) { $flat = $flat.Substring($flat.Length - 160) }
        $cap = $flat
      } catch {}

      $firstNonEmptyRowText = ''
      $firstNonEmptyRowNumber = 0
      if ($null -ne $R -and $null -ne $C) {
        for ($r = 1; $r -le $R -and $firstNonEmptyRowNumber -eq 0; $r++) {
          $vals = @()
          $any = $false
          for ($c = 1; $c -le $C; $c++) {
            $v = $grid[[string]$r + ',' + [string]$c]
            $vals += $v
            if ($v.Trim().Length -gt 0) { $any = $true }
          }
          if ($any) { $firstNonEmptyRowNumber = $r; $firstNonEmptyRowText = ($vals -join ' | ') }
        }
      }

      $rec.tableDetails += [ordered]@{
        docOrder = $docOrder
        wordTableItem = $ti.item
        csvFiles = $csvFiles
        declaredRows = $R
        declaredCols = $C
        cellProbeOk = $probedOk
        cellProbeFail = $probedFail
        fillHorizontalMerged = $filledHorizontal
        fillVerticalMerged = $filledVertical
        linearCellCount = $linear.Count
        linearNonEmptyCount = $linearNonEmpty.Count
        gridNonEmptyCount = $gridNonEmpty.Count
        losslessCrossCheck = $contentMatch
        nonEmptyRows = $nonEmptyRows
        rangeStart = $ti.start
        captionGuess = $cap
        firstNonEmptyRowNumber = $firstNonEmptyRowNumber
        firstNonEmptyRow = $firstNonEmptyRowText
      }
      Write-Host ('  table #' + $docOrder + ' declared=' + $R + 'x' + $C + ' ok=' + $probedOk + ' mergedPositions=' + $probedFail + ' linearCells=' + $linear.Count + ' crossCheck=' + $contentMatch + ' csv=' + ($csvFiles -join ' , '))
    }

    [void]$md.AppendLine('')
    [void]$md.AppendLine('## 表格清单（按文档出现顺序）')
    [void]$md.AppendLine('')
    foreach ($td in $rec.tableDetails) {
      if ($td.Contains('csvFiles')) {
        [void]$md.AppendLine('- 表 #' + $td.docOrder + '（Word Tables.Item ' + $td.wordTableItem + '）声明 ' + $td.declaredRows + ' 行 × ' + $td.declaredCols + ' 列；非空行 ' + $td.nonEmptyRows + '；Cell(r,c) 命中 ' + $td.cellProbeOk + '，合并覆盖位 ' + $td.cellProbeFail + '（横向补 ' + $td.fillHorizontalMerged + '，纵向补 ' + $td.fillVerticalMerged + '）；线性单元格数 ' + $td.linearCellCount + '；内容一致性校验=' + $td.losslessCrossCheck)
        [void]$md.AppendLine('  - CSV: ' + (($td.csvFiles | ForEach-Object { 'specs\tables\' + [System.IO.Path]::GetFileName($_) }) -join ' , '))
        [void]$md.AppendLine('  - 首个非空行(第 ' + $td.firstNonEmptyRowNumber + ' 行): ' + $td.firstNonEmptyRow)
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
  method = 'Word COM; copies opened read-only, Close(0)=wdDoNotSaveChanges; source tree untouched; table grid via Table.Cell(r,c) probing + merge fill + linear cross-check'
  docCount = $results.Count
  docs = $results
})
Write-Utf8Bom $outJson ($payload | ConvertTo-Json -Depth 8)
Write-Host ('wrote ' + $outJson)
