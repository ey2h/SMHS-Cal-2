# Word COM extraction of 汇宝 calc books (.doc) -> txt / tables csv / structure json / per-book stats
# One Word COM session per file, each in try/catch. Read-only open, no Save, Close($false), Quit().

$ErrorActionPreference = 'Stop'

$raw      = 'C:\code\Glass\docs\recon\calcbooks\raw'
$txtDir   = 'C:\code\Glass\docs\recon\calcbooks\txt'
$tblDir   = 'C:\code\Glass\docs\recon\calcbooks\tables'
$strDir   = 'C:\code\Glass\docs\recon\calcbooks\structure'
$workDir  = 'C:\code\Glass\docs\recon\work'
$linesDir = Join-Path $workDir 'lines'
$statsDir = Join-Path $workDir 'stats'
$shapeDir = Join-Path $workDir 'shapes'
$logPath  = Join-Path $workDir 'extract_log.txt'
$summaryPath = Join-Path $workDir 'extract_summary.json'

foreach ($d in @($txtDir,$tblDir,$strDir,$linesDir,$statsDir,$shapeDir)) { New-Item -ItemType Directory -Force -Path $d | Out-Null }

$enc = New-Object System.Text.UTF8Encoding($false)

function Log([string]$msg) {
    $line = ('[{0}] {1}' -f (Get-Date -Format 'HH:mm:ss'), $msg)
    [System.IO.File]::AppendAllText($logPath, $line + "`r`n", $enc)
    Write-Output $line
}

function CleanText([string]$s) {
    if ($null -eq $s) { return '' }
    # Word control chars: 13 = para mark, 7 = cell end, 12 = page break, 1 = object, 2 = cell anchor,
    # 19/20/21 = field start/sep/end, 9 = tab, 11 = line break, 31/30 = read/write boundary
    $s = [regex]::Replace($s, '[\u0000-\u000C\u000E-\u001F\u007F\u200B\u2028\u2029\ufeff]', ' ')
    $s = $s -replace '\u00A0', ' '
    $s = $s.Trim()
    $s = [regex]::Replace($s, '\s{2,}', ' ')
    return $s
}

function UnescapeJson([string]$json) {
    return [regex]::Replace($json, '\\u([0-9a-fA-F]{4})', {
        param($m) [string][char][Convert]::ToInt32($m.Groups[1].Value, 16)
    })
}

function CsvField([string]$v) {
    if ($null -eq $v) { $v = '' }
    $v = $v.Replace('"', '""')
    return '"' + $v + '"'
}

$isUnitLine = '(N/mm|MPa|kN|N/m|……|=|×|mm2|mm²)'

$books = Get-ChildItem -LiteralPath $raw -Filter *.doc | Sort-Object Name
Log ('FOUND {0} .doc files' -f $books.Count)

$summary = New-Object System.Collections.Generic.List[object]

foreach ($f in $books) {
    $name = [System.IO.Path]::GetFileNameWithoutExtension($f.Name)
    if ((Test-Path -LiteralPath (Join-Path $txtDir ($name + '.txt'))) -and (Test-Path -LiteralPath (Join-Path $strDir ($name + '.json')))) {
        Log ('SKIP {0} (txt+structure already present)' -f $name)
        continue
    }
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $rec = [ordered]@{
        book = $name; source = $f.FullName; ok = $false; error = $null
        paragraphs = 0; tables = 0; tableFailures = @(); tableCells = 0
        lines = 0; linesWithEquals = 0; linesWithStressUnit = 0; linesWithClause = 0
        headings = 0; emptyLines = 0; embeddedObjectLines = 0
        fields = 0; omaths = 0; shapes = 0; inlineShapes = 0; footnotes = 0; endnotes = 0
        outlineOnlyHeadings = 0; styleHeadings = 0; patternOnlyHeadings = 0
        seconds = 0; closeError = $null; quitError = $null
    }
    Log ('OPEN {0}' -f $f.Name)
    $word = $null; $doc = $null
    try {
        try { Unblock-File -LiteralPath $f.FullName -ErrorAction SilentlyContinue } catch {}

        $word = New-Object -ComObject Word.Application
        $word.Visible = $false
        try { $word.DisplayAlerts = 0 } catch {}
        try { $word.AutomationSecurity = 3 } catch {}   # msoAutomationSecurityForceDisable (no macros)

        $doc = $word.Documents.Open($f.FullName, $false, $true)

        try { $rec.fields        = $doc.Fields.Count } catch {}
        try { $rec.omaths        = $doc.OMaths.Count } catch {}
        try { $rec.shapes        = $doc.Shapes.Count } catch {}
        try { $rec.inlineShapes  = $doc.InlineShapes.Count } catch {}
        try { $rec.footnotes     = $doc.Footnotes.Count } catch {}
        try { $rec.endnotes      = $doc.Endnotes.Count } catch {}

        # ---- table geometry (used to tag paragraphs that live inside a table) ----
        $tableRanges = @()
        $tcount = 0
        try { $tcount = $doc.Tables.Count } catch { $rec.tableFailures += ('Tables.Count: ' + $_.Exception.Message) }
        $rec.tables = $tcount
        for ($i = 1; $i -le $tcount; $i++) {
            try {
                $t = $doc.Tables.Item($i)
                $tableRanges += [pscustomobject]@{ index = $i; start = $t.Range.Start; end = $t.Range.End }
            } catch {
                $rec.tableFailures += ('Tables.Item(' + $i + '): ' + $_.Exception.Message)
            }
        }

        # ---- paragraph walk over main story, in document order ----
        $story = $doc.Content
        $storyStart = [int]$story.Start
        $storyEnd   = [int]$story.End
        $rng = $doc.Range($storyStart, $storyStart)

        $lines      = New-Object System.Collections.Generic.List[object]   # exported txt lines
        $records    = New-Object System.Collections.Generic.List[object]   # jsonl records
        $headings   = New-Object System.Collections.Generic.List[object]
        $levelPath  = @{}
        $guard = 0
        $maxParas = 120000

        while ($true) {
            $guard++
            if ($guard -gt $maxParas) { $rec.error = 'paragraph walk guard tripped at ' + $maxParas; break }

            $p = $null
            try { $p = $rng.Paragraphs.Item(1) } catch { break }
            if ($null -eq $p) { break }

            $pStart = [int]$p.Range.Start
            $pEnd   = [int]$p.Range.End
            if ($pEnd -le $pStart) { break }          # zero-length paragraph -> avoid infinite loop
            if ($pStart -ge $storyEnd) { break }

            $rawText = ''
            try { $rawText = $p.Range.Text } catch { $rawText = '' }
            $text = CleanText $rawText

            $outline = 10
            try { $outline = [int]$p.OutlineLevel } catch {}

            $styleName = ''
            $looksNumbered = ($text -match '^\d+(\.\d+){0,4}[\s\u3000]?[^\s=]') -and ($text.Length -le 80) -and ($text -notmatch $isUnitLine) -and ($text -match '[^\d\.\s]')
            if ($outline -lt 10 -or $looksNumbered) {
                try {
                    $st = $p.Style
                    if ($st -is [string]) { $styleName = $st } else { try { $styleName = [string]$st.Name } catch { $styleName = [string]$st } }
                } catch { $styleName = '' }
            }
            $styleIsHeading = ($styleName -match '(?i)^(标题|heading|title|chapter|第)')

            $listNumber = ''
            if ($outline -lt 10 -or $looksNumbered -or $styleIsHeading) {
                try { $listNumber = CleanText ([string]$p.Range.ListFormat.ListString) } catch { $listNumber = '' }
            }

            $isHeading = ($outline -lt 10) -or $styleIsHeading -or $looksNumbered
            if ($isHeading) {
                if ($outline -lt 10) { $rec.outlineOnlyHeadings++ }
                if ($styleIsHeading) { $rec.styleHeadings++ }
                if (-not ($outline -lt 10) -and -not $styleIsHeading -and $looksNumbered) { $rec.patternOnlyHeadings++ }
            }

            # which table (if any) contains this paragraph
            $tableIndex = 0
            foreach ($tr in $tableRanges) { if ($pStart -ge $tr.start -and $pStart -lt $tr.end) { $tableIndex = $tr.index; break } }

            $flags = @()
            if ($text -eq '') { $flags += 'empty'; $rec.emptyLines++ }
            if ($rawText -match '[\u0001\u0002]') { $flags += 'embedded_object'; $rec.embeddedObjectLines++ }

            # maintain heading path (for section column)
            $section = ''
            if ($isHeading -and $text -ne '') {
                $lvl = if ($outline -lt 10) { $outline } else { 1 }
                $levelPath[$lvl] = $text
                $keys = ($levelPath.Keys | Sort-Object)
                $parts = @(); foreach ($k in $keys) { if ($levelPath[$k]) { $parts += $levelPath[$k] } }
                $section = ($parts -join ' / ')
                $headings.Add([ordered]@{ order = ($headings.Count + 1); outlineLevel = $lvl; text = $text; listNumber = $listNumber; styleName = $styleName; paraIndex = ($records.Count + 1) })
            } else {
                $keys = ($levelPath.Keys | Sort-Object)
                $parts = @(); foreach ($k in $keys) { if ($levelPath[$k]) { $parts += $levelPath[$k] } }
                $section = ($parts -join ' / ')
            }

            $lines.Add($text)
            $records.Add([ordered]@{
                i          = ($records.Count + 1)
                paraIndex  = ($records.Count + 1)
                outline    = $outline
                styleName  = $styleName
                listNumber = $listNumber
                heading    = [bool]$isHeading
                table      = $tableIndex
                section    = $section
                flags      = $flags
                text       = $text
            })

            if ($pEnd -ge $storyEnd) { break }
            $rng.SetRange($pEnd, $storyEnd)
        }

        $rec.paragraphs = $records.Count

        # ---- write txt (UTF-8, one paragraph per line) ----
        $txtPath = Join-Path $txtDir ($name + '.txt')
        [System.IO.File]::WriteAllLines($txtPath, [string[]]$lines.ToArray(), $enc)

        # ---- write jsonl of all lines with section info ----
        $jsonlPath = Join-Path $linesDir ($name + '.lines.jsonl')
        $sb = New-Object System.Text.StringBuilder
        foreach ($r in $records) {
            $j = UnescapeJson (ConvertTo-Json $r -Compress -Depth 6)
            [void]$sb.AppendLine($j)
        }
        [System.IO.File]::WriteAllText($jsonlPath, $sb.ToString(), $enc)

        # ---- write structure json ----
        $structPath = Join-Path $strDir ($name + '.json')
        $structArr = @()
        foreach ($h in $headings) {
            $item = [ordered]@{ order = $h.order; outlineLevel = $h.outlineLevel; text = $h.text }
            if ($h.listNumber) { $item['listNumber'] = $h.listNumber }
            if ($h.styleName)  { $item['styleName']  = $h.styleName }
            $item['paraIndex'] = $h.paraIndex
            $structArr += $item
        }
        $rec.headings = $structArr.Count
        $structJson = if ($structArr.Count -eq 0) { '[]' } else { UnescapeJson (ConvertTo-Json $structArr -Depth 6) }
        [System.IO.File]::WriteAllText($structPath, $structJson, $enc)

        # ---- per-table CSV (grid, every field quoted) ----
        foreach ($tr in $tableRanges) {
            $t = $null
            try { $t = $doc.Tables.Item($tr.index) } catch { $rec.tableFailures += ('re-Item table ' + $tr.index + ': ' + $_.Exception.Message); continue }
            $grid = @{}
            $maxRow = 0; $maxCol = 0; $cellCount = 0; $fail = $false
            $cells = $null
            try { $cells = $t.Range.Cells } catch { $rec.tableFailures += ('table ' + $tr.index + ' Range.Cells: ' + $_.Exception.Message); $fail = $true }
            if (-not $fail) {
                try {
                    foreach ($c in $cells) {
                        $ri = [int]$c.RowIndex; $ci = [int]$c.ColumnIndex
                        $val = CleanText ([string]$c.Range.Text)
                        if ($ri -gt $maxRow) { $maxRow = $ri }
                        if ($ci -gt $maxCol) { $maxCol = $ci }
                        $key = $ri.ToString() + ',' + $ci.ToString()
                        $grid[$key] = $val
                        $cellCount++
                    }
                } catch { $rec.tableFailures += ('table ' + $tr.index + ' cell iteration: ' + $_.Exception.Message); $fail = $true }
            }
            if ($fail -or $maxRow -eq 0) { continue }
            $rec.tableCells += $cellCount
            $csvSb = New-Object System.Text.StringBuilder
            for ($r = 1; $r -le $maxRow; $r++) {
                $row = @()
                for ($c = 1; $c -le $maxCol; $c++) {
                    $key = $r.ToString() + ',' + $c.ToString()
                    $val = ''
                    if ($grid.ContainsKey($key)) { $val = $grid[$key] }
                    $row += CsvField $val
                }
                [void]$csvSb.AppendLine(($row -join ','))
            }
            $csvPath = Join-Path $tblDir ($name + '__t' + $tr.index.ToString('000') + '.csv')
            [System.IO.File]::WriteAllText($csvPath, $csvSb.ToString(), $enc)
        }

        # ---- text boxes / floating shapes (not part of main story) ----
        try {
            $shapeSb = New-Object System.Text.StringBuilder
            $sc = [int]$doc.Shapes.Count
            for ($i = 1; $i -le $sc; $i++) {
                try {
                    $sh = $doc.Shapes.Item($i)
                    $txt = ''
                    try { if ($sh.HasTextFrame) { if ($sh.TextFrame.HasText) { $txt = CleanText ([string]$sh.TextFrame.TextRange.Text) } } } catch {}
                    [void]$shapeSb.AppendLine(('#shape{0} name={1} type={2}' -f $i, $sh.Name, $sh.Type))
                    if ($txt) { [void]$shapeSb.AppendLine($txt) }
                } catch { $rec.tableFailures += ('shape ' + $i + ': ' + $_.Exception.Message) }
            }
            if ($sc -gt 0) { [System.IO.File]::WriteAllText((Join-Path $shapeDir ($name + '.shapes.txt')), $shapeSb.ToString(), $enc) }
        } catch {}

        # ---- stats over exported txt lines ----
        $rec.lines = $lines.Count
        foreach ($l in $lines) {
            if ($l -match '=') { $rec.linesWithEquals++ }
            if ($l -match 'MPa' -or $l -match 'N/mm') { $rec.linesWithStressUnit++ }
            if ($l -match '……' -or $l -match '\[[^\[\]]{1,60}\]') { $rec.linesWithClause++ }
        }

        $rec.ok = $true
    } catch {
        $rec.error = $_.Exception.Message
        Log ('ERROR {0} :: {1}' -f $f.Name, $_.Exception.Message)
    } finally {
        if ($doc) { try { $doc.Close($false) } catch { $rec.closeError = $_.Exception.Message } }
        if ($word) { try { $word.Quit() } catch { $rec.quitError = $_.Exception.Message } }
        try { if ($doc) { [void][Runtime.InteropServices.Marshal]::ReleaseComObject($doc) } } catch {}
        try { if ($word) { [void][Runtime.InteropServices.Marshal]::ReleaseComObject($word) } } catch {}
        $doc = $null; $word = $null
        [GC]::Collect(); [GC]::WaitForPendingFinalizers()
    }
    $sw.Stop()
    $rec.seconds = [Math]::Round($sw.Elapsed.TotalSeconds, 1)
    Log ('DONE {0} paras={1} tables={2} heads={3} secs={4} ok={5}' -f $name, $rec.paragraphs, $rec.tables, $rec.headings, $rec.seconds, $rec.ok)
    $summary.Add([pscustomobject]$rec)
    $recJson = UnescapeJson (ConvertTo-Json ([pscustomobject]$rec) -Depth 6)
    [System.IO.File]::WriteAllText((Join-Path $statsDir ($name + '.stats.json')), $recJson, $enc)
}

$sumJson = UnescapeJson (ConvertTo-Json $summary.ToArray() -Depth 6)
[System.IO.File]::WriteAllText($summaryPath, $sumJson, $enc)
Log 'ALL DONE'
