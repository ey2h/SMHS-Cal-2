param(
    [Parameter(Mandatory = $true)][string]$Path,
    [Parameter(Mandatory = $true)][string]$JsonOut,
    [string]$DerivedDocxOut = ''
)
$ErrorActionPreference = 'Continue'

# ============ helpers ============
$enumMaps = @{}
try {
    Add-Type -AssemblyName Microsoft.Office.Interop.Word
    $asm = [AppDomain]::CurrentDomain.GetAssemblies() | Where-Object { $_.GetName().Name -like '*Interop.Word*' } | Select-Object -First 1
    $enums = 'WdPaperSize','WdParagraphAlignment','WdLineSpacing','WdOutlineLevel','WdOrientation','WdStoryType','WdFieldType','WdSectionStart','WdStyleType','WdBorderType','WdLineStyle','WdTableAlignment','WdPreferredWidthType','WdStatistic','WdPageNumberStyle','WdBorders','WdEnableBorders'
    foreach ($en in $enums) {
        if (-not $asm) { break }
        $t = $asm.GetType("Microsoft.Office.Interop.Word.$en")
        if ($t) {
            $m = @{}
            foreach ($v in [Enum]::GetValues($t)) { $m[[int]$v] = [string]$v }
            $enumMaps[$en] = $m
        }
    }
} catch { Write-Host ('enum map load failed: ' + $_.Exception.Message) }

function EN([string]$en, $val) {
    if ($null -eq $val) { return $null }
    $i = [int]$val
    if ($enumMaps.ContainsKey($en)) { if ($enumMaps[$en].ContainsKey($i)) { return $enumMaps[$en][$i] } }
    return ('raw_' + $i)
}

function Num($v) {
    if ($null -eq $v) { return $null }
    $ok = ($v -is [int]) -or ($v -is [long]) -or ($v -is [double]) -or ($v -is [single]) -or ($v -is [decimal]) -or ($v -is [float])
    if (-not $ok) { return $null }
    $d = [double]$v
    if ([double]::IsNaN($d) -or [double]::IsInfinity($d)) { return $null }
    if ([math]::Abs($d) -ge 900000) { return $null }   # wdUndefined -> value not specified
    return [math]::Round($d, 3)
}
function Pt2Mm($v) {
    $n = Num $v
    if ($null -eq $n) { return $null }
    return [math]::Round($n * 25.4 / 72.0, 2)
}
function BoolOf($v) {
    if ($null -eq $v) { return $null }
    if ($v -is [bool]) { return $v }
    try { $i = [int]$v } catch { return $null }
    return ($i -ne 0)
}
function Txt($v) {
    if ($null -eq $v) { return $null }
    $s = [string]$v
    $s = $s.Replace([string][char]7, '<CELL>')
    $s = $s.Replace([string][char]11, '<LINEBREAK>')
    $s = $s.Replace([string][char]12, '<PAGEBREAK>')
    $s = $s.Replace([string][char]13, '<PARA>')
    $s = $s.Replace([string][char]10, '<PARA>')
    $s = $s.Replace([string][char]1, '<OBJ>')
    $s = $s.Replace([string][char]29, '<HARDBREAK>')
    $s = $s.Replace([string][char]160, ' ')
    return $s.Trim()
}
function Clip($s, [int]$n) {
    if ($null -eq $s) { return $null }
    if ($s.Length -le $n) { return $s }
    return ($s.Substring(0, $n) + '...')
}
function Prop($obj, [string]$name) {
    if ($null -eq $obj) { return $null }
    try {
        $v = $obj.$name
        if ($v -is [System.__ComObject]) { return $null }
        return $v
    } catch { return $null }
}
function PropOf($obj, [string]$outer, [string]$inner) {
    if ($null -eq $obj) { return $null }
    try { return Prop ($obj.$outer) $inner } catch { return $null }
}
function Info($rng, [int]$which) {
    if ($null -eq $rng) { return $null }
    try { return $rng.Information($which) } catch {}
    try { return $rng.GetType().InvokeMember('Information', 'GetProperty', $null, $rng, @($which)) } catch { return $null }
}
function EnumBorders($coll) {
    $out = @()
    if ($null -eq $coll) { return ,$out }
    foreach ($bt in -1, -2, -3, -4, -5, -6) {
        $b = $null
        try { $b = $coll.Item($bt) } catch {}
        if ($null -eq $b) {
            $out += [ordered]@{ type = $bt; type_name = EN 'WdBorderType' $bt; unavailable = $true }
            continue
        }
        $ls = Prop $b 'LineStyle'
        $vis = $false
        if ($null -ne $ls) { try { $vis = ([int]$ls -ne 0) } catch {} }
        $out += [ordered]@{
            type = $bt
            type_name = EN 'WdBorderType' $bt
            line_style = $ls
            line_style_name = EN 'WdLineStyle' $ls
            has_visible_border = $vis
            line_width = Prop $b 'LineWidth'
            space_pt = Num (Prop $b 'Space')
            color_index = Prop $b 'ColorIndex'
        }
    }
    return ,$out
}

$errors = New-Object System.Collections.Generic.List[string]
function Note($msg) { $errors.Add([string]$msg) | Out-Null }

if (-not (Test-Path -LiteralPath $Path)) { throw ('input not found: ' + $Path) }
$Path = (Resolve-Path -LiteralPath $Path).Path
$sha = (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash
$item = Get-Item -LiteralPath $Path

$word = $null
$doc = $null
$out = [ordered]@{}

try {
    $word = New-Object -ComObject Word.Application
    try { $word.Visible = $false } catch {}
    try { $word.DisplayAlerts = 0 } catch {}

    $doc = $word.Documents.Open($Path, $false, $true)
    if (-not $doc) { throw 'Documents.Open returned null' }
    Write-Host ('opened read-only: ' + $Path)

    $out['meta'] = [ordered]@{
        source_path       = $Path
        sha256            = $sha
        bytes             = $item.Length
        opened_readonly   = $true
        word_version      = [string](Prop $word 'Version')
        doc_full_name     = Prop $doc 'FullName'
        doc_attached_template = Prop $doc 'AttachedTemplate'
        compatibility_mode = Prop $doc 'CompatibilityMode'
        has_password      = BoolOf (Prop $doc 'HasPassword')
        has_vb_project    = BoolOf (Prop $doc 'HasVBProject')
        read_only_recommended = BoolOf (Prop $doc 'ReadOnlyRecommended')
        capture_time      = (Get-Date).ToString('yyyy-MM-dd HH:mm:ss')
        enum_maps_loaded  = @($enumMaps.Keys | Sort-Object)
    }

    # ---------- sections / page setup ----------
    $secCount = 0
    try { $secCount = [int]$doc.Sections.Count } catch { Note 'Sections.Count failed' }
    $psList = @()
    for ($s = 1; $s -le $secCount; $s++) {
        $sec = $null
        try { $sec = $doc.Sections.Item($s) } catch { Note ("Section $s Item failed"); continue }
        $ps = $null
        try { $ps = $sec.PageSetup } catch { Note ("Section $s PageSetup failed") }
        $startRaw = Prop $sec 'Start'
        $pw = Num (Prop $ps 'PageWidth'); $ph = Num (Prop $ps 'PageHeight')
        $rec = [ordered]@{
            section_index = $s
            section_start = $startRaw
            section_start_name = EN 'WdSectionStart' $startRaw
            page_width_pt = $pw; page_width_mm = Pt2Mm $pw
            page_height_pt = $ph; page_height_mm = Pt2Mm $ph
            paper_size = Prop $ps 'PaperSize'; paper_size_name = EN 'WdPaperSize' (Prop $ps 'PaperSize')
            orientation = Prop $ps 'Orientation'; orientation_name = EN 'WdOrientation' (Prop $ps 'Orientation')
            top_margin_pt = Num (Prop $ps 'TopMargin'); top_margin_mm = Pt2Mm (Prop $ps 'TopMargin')
            bottom_margin_pt = Num (Prop $ps 'BottomMargin'); bottom_margin_mm = Pt2Mm (Prop $ps 'BottomMargin')
            left_margin_pt = Num (Prop $ps 'LeftMargin'); left_margin_mm = Pt2Mm (Prop $ps 'LeftMargin')
            right_margin_pt = Num (Prop $ps 'RightMargin'); right_margin_mm = Pt2Mm (Prop $ps 'RightMargin')
            header_distance_pt = Num (Prop $ps 'HeaderDistance'); header_distance_mm = Pt2Mm (Prop $ps 'HeaderDistance')
            footer_distance_pt = Num (Prop $ps 'FooterDistance'); footer_distance_mm = Pt2Mm (Prop $ps 'FooterDistance')
            gutter_pt = Num (Prop $ps 'Gutter'); gutter_mm = Pt2Mm (Prop $ps 'Gutter')
            gutter_position = Prop $ps 'GutterPosition'
            mirror_margins = BoolOf (Prop $ps 'MirrorMargins')
            different_first_page_header_footer = BoolOf (Prop $ps 'DifferentFirstPageHeaderFooter')
            odd_and_even_pages_header_footer = BoolOf (Prop $ps 'OddAndEvenPagesHeaderFooter')
            default_tab_stop_pt = Num (Prop $ps 'DefaultTabStop'); default_tab_stop_mm = Pt2Mm (Prop $ps 'DefaultTabStop')
            text_columns_count = PropOf $ps 'TextColumns' 'Count'
            text_columns_spacing_pt = Num (PropOf $ps 'TextColumns' 'Spacing')
            text_columns_evenly_spaced = BoolOf (PropOf $ps 'TextColumns' 'EvenlySpaced')
            vertical_alignment = Prop $ps 'VerticalAlignment'
            restart_numbering_at_section = BoolOf (Prop $ps 'RestartNumberingAtSection')
            starting_page_index = Prop $ps 'StartingPageIndex'
            page_number_style = Prop $ps 'PageNumberStyle'
            page_number_style_name = EN 'WdPageNumberStyle' (Prop $ps 'PageNumberStyle')
            page_borders = EnumBorders (Prop $ps 'Borders')
        }
        $wmm = $rec['page_width_mm']; $hmm = $rec['page_height_mm']
        $rec['paper_dims_match'] = ('{0} x {1} mm' -f $wmm, $hmm)
        $psList += $rec
    }
    $out['page_setup'] = $psList

    # ---------- all styles ----------
    $styleCount = 0
    try { $styleCount = [int]$doc.Styles.Count } catch { Note 'Styles.Count failed' }
    $allStyles = @()
    $styleByName = @{}
    for ($i = 1; $i -le $styleCount; $i++) {
        $st = $null
        try { $st = $doc.Styles.Item($i) } catch { continue }
        $nl = Prop $st 'NameLocal'
        $nm = Prop $st 'Name'
        $bi = BoolOf (Prop $st 'BuiltIn')
        $allStyles += [ordered]@{ name_local = $nl; name = $nm; built_in = $bi; style_type = EN 'WdStyleType' (Prop $st 'Type') }
        if ($nl -and -not $styleByName.ContainsKey([string]$nl)) { $styleByName[[string]$nl] = $st }
    }

    # ---------- paragraphs ----------
    $paraCount = 0
    try { $paraCount = [int]$doc.Paragraphs.Count } catch { Note 'Paragraphs.Count failed' }
    $paras = New-Object System.Collections.Generic.List[object]
    $usedOrder = New-Object System.Collections.Generic.List[string]
    $usedHits = @{}
    for ($i = 1; $i -le $paraCount; $i++) {
        $p = $null
        try { $p = $doc.Paragraphs.Item($i) } catch { Note ("Paragraph $i Item failed"); continue }
        $rng = $null; try { $rng = $p.Range } catch {}
        $stObj = $null; try { $stObj = $p.Style } catch {}
        $pf = $null; try { $pf = $p.Format } catch {}
        $fnt = $null; try { $fnt = $p.Font } catch {}
        $styleLocal = Prop $stObj 'NameLocal'
        $rec = [ordered]@{
            index = $i
            text = Txt (Prop $rng 'Text')
            style = $styleLocal
            style_internal = Prop $stObj 'Name'
            style_type = EN 'WdStyleType' (Prop $stObj 'Type')
            outline_level = Prop $pf 'OutlineLevel'
            outline_level_name = EN 'WdOutlineLevel' (Prop $pf 'OutlineLevel')
            alignment = Prop $pf 'Alignment'
            alignment_name = EN 'WdParagraphAlignment' (Prop $pf 'Alignment')
            font_name = Prop $fnt 'Name'
            font_name_fareast = Prop $fnt 'NameFarEast'
            font_name_ascii = Prop $fnt 'NameAscii'
            font_size_pt = Num (Prop $fnt 'Size')
            bold = BoolOf (Prop $fnt 'Bold')
            italic = BoolOf (Prop $fnt 'Italic')
            space_before_pt = Num (Prop $pf 'SpaceBefore')
            space_after_pt = Num (Prop $pf 'SpaceAfter')
            space_before_auto = BoolOf (Prop $pf 'SpaceBeforeAuto')
            space_after_auto = BoolOf (Prop $pf 'SpaceAfterAuto')
            line_spacing_rule = Prop $pf 'LineSpacingRule'
            line_spacing_rule_name = EN 'WdLineSpacing' (Prop $pf 'LineSpacingRule')
            line_spacing_pt = Num (Prop $pf 'LineSpacing')
            left_indent_pt = Num (Prop $pf 'LeftIndent')
            first_line_indent_pt = Num (Prop $pf 'FirstLineIndent')
            right_indent_pt = Num (Prop $pf 'RightIndent')
            keep_with_next = BoolOf (Prop $pf 'KeepWithNext')
            keep_together = BoolOf (Prop $pf 'KeepTogether')
            widow_control = BoolOf (Prop $pf 'WidowControl')
            page_break_before = BoolOf (Prop $pf 'PageBreakBefore')
            in_table = BoolOf (Info $rng 12)
            range_start = Prop $rng 'Start'
            range_end = Prop $rng 'End'
            page_physical = Info $rng 3
            page_adjusted = Info $rng 1
        }
        $paras.Add($rec)
        if ($styleLocal) {
            $k = [string]$styleLocal
            if (-not $usedHits.ContainsKey($k)) {
                $h = [ordered]@{ used_count = 0; first_index = $i }
                $usedHits[$k] = $h
                $usedOrder.Add($k)
            }
            $usedHits[$k]['used_count'] = ([int]$usedHits[$k]['used_count'] + 1)
        }
    }
    $out['paragraphs'] = $paras

    function StyleDetail($st, [string]$fallbackName) {
        if ($null -eq $st) { return [ordered]@{ name_local = $fallbackName; note = 'style object not resolvable' } }
        $f = $null; try { $f = $st.Font } catch {}
        $pf = $null; try { $pf = $st.ParagraphFormat } catch {}
        $bs = $null; try { $bs = $st.BaseStyle } catch {}
        $nps = $null; try { $nps = $st.NextParagraphStyle } catch {}
        $d = [ordered]@{
            name_local = Prop $st 'NameLocal'
            name_internal = Prop $st 'Name'
            style_type = EN 'WdStyleType' (Prop $st 'Type')
            built_in = BoolOf (Prop $st 'BuiltIn')
            base_style = Prop $bs 'NameLocal'
            base_style_internal = Prop $bs 'Name'
            next_paragraph_style = Prop $nps 'NameLocal'
            author = Prop $st 'Author'
            font_name = Prop $f 'Name'
            font_name_fareast = Prop $f 'NameFarEast'
            font_name_ascii = Prop $f 'NameAscii'
            font_name_other = Prop $f 'NameOther'
            font_size_pt = Num (Prop $f 'Size')
            bold = BoolOf (Prop $f 'Bold')
            italic = BoolOf (Prop $f 'Italic')
            underline = Prop $f 'Underline'
            color_index = Prop $f 'ColorIndex'
            color_rgb = PropOf $f 'Color' 'RGB'
            all_caps = BoolOf (Prop $f 'AllCaps')
            small_caps = BoolOf (Prop $f 'SmallCaps')
            character_spacing_pt = Num (Prop $f 'Spacing')
            scale_percent = Num (Prop $f 'Scale')
            emboss = BoolOf (Prop $f 'Emboss')
            outline = BoolOf (Prop $f 'Outline')
            shadow = BoolOf (Prop $f 'Shadow')
            alignment = Prop $pf 'Alignment'
            alignment_name = EN 'WdParagraphAlignment' (Prop $pf 'Alignment')
            space_before_pt = Num (Prop $pf 'SpaceBefore')
            space_after_pt = Num (Prop $pf 'SpaceAfter')
            space_before_auto = BoolOf (Prop $pf 'SpaceBeforeAuto')
            space_after_auto = BoolOf (Prop $pf 'SpaceAfterAuto')
            line_spacing_rule = Prop $pf 'LineSpacingRule'
            line_spacing_rule_name = EN 'WdLineSpacing' (Prop $pf 'LineSpacingRule')
            line_spacing_pt = Num (Prop $pf 'LineSpacing')
            outline_level = Prop $pf 'OutlineLevel'
            outline_level_name = EN 'WdOutlineLevel' (Prop $pf 'OutlineLevel')
            left_indent_pt = Num (Prop $pf 'LeftIndent')
            first_line_indent_pt = Num (Prop $pf 'FirstLineIndent')
            right_indent_pt = Num (Prop $pf 'RightIndent')
            keep_with_next = BoolOf (Prop $pf 'KeepWithNext')
            keep_together = BoolOf (Prop $pf 'KeepTogether')
            widow_control = BoolOf (Prop $pf 'WidowControl')
            page_break_before = BoolOf (Prop $pf 'PageBreakBefore')
            paragraph_borders = EnumBorders (Prop $pf 'Borders')
        }
        try {
            $lf = $st.ListFormat
            if ($null -eq $lf) {
                $d['numbering'] = 'style has no ListFormat member (COM)'
            } else {
                $num = [ordered]@{}
                $num['list_type'] = Prop $lf 'ListType'
                $num['list_template_name'] = PropOf $lf 'ListTemplate' 'Name'
                $num['list_level_number'] = PropOf $lf 'ListLevel' 'Number'
                $num['list_level_text'] = Txt (PropOf $lf 'ListLevel' 'Text')
                $num['list_level_start_at'] = PropOf $lf 'ListLevel' 'StartAt'
                $num['list_level_restart_mode'] = PropOf $lf 'ListLevel' 'RestartMode'
                $num['list_level_trailing_space'] = PropOf $lf 'ListLevel' 'TrailingSpace'
                $d['numbering'] = $num
            }
        } catch { $d['numbering'] = 'ListFormat read failed: ' + $_.Exception.Message }
        return $d
    }

    $usedDetails = @()
    foreach ($k in $usedOrder) {
        $st = $null
        if ($styleByName.ContainsKey($k)) { $st = $styleByName[$k] }
        if ($null -eq $st) { try { $st = $doc.Styles.Item($k) } catch {} }
        $d = StyleDetail $st $k
        $d['used_by_paragraphs'] = [int]$usedHits[$k]['used_count']
        $d['first_paragraph_index'] = [int]$usedHits[$k]['first_index']
        $usedDetails += $d
    }
    $out['styles_used'] = $usedDetails
    $out['styles_used_count'] = $usedOrder.Count
    $out['styles_total_count'] = $styleCount
    $unused = @()
    foreach ($a in $allStyles) {
        $nl = $a['name_local']
        if ($nl -and -not $usedHits.ContainsKey([string]$nl)) { $unused += $nl }
    }
    $out['styles_unused_names'] = $unused
    $out['styles_unused_count'] = $unused.Count
    $out['styles_all_names'] = $allStyles

    # ---------- tables ----------
    $tblCount = 0
    try { $tblCount = [int]$doc.Tables.Count } catch { Note 'Tables.Count failed' }
    $tables = New-Object System.Collections.Generic.List[object]
    for ($t = 1; $t -le $tblCount; $t++) {
        $tb = $null
        try { $tb = $doc.Tables.Item($t) } catch { Note ("Table $t Item failed"); continue }
        $rows = $null; $cols = $null; $cells = $null
        try { $rows = [int]$tb.Rows.Count } catch { Note ("Table $t Rows.Count failed") }
        try { $cols = [int]$tb.Columns.Count } catch { Note ("Table $t Columns.Count failed (merged cells?)") }
        try { $cells = [int]$tb.Range.Cells.Count } catch {}

        $firstRow = @()
        $rowsColl = $null
        try { $rowsColl = $tb.Rows } catch {}
        $row1 = $null
        try { $row1 = $rowsColl.Item(1) } catch {}
        if ($row1) { try { foreach ($c in $row1.Cells) { $firstRow += Txt (Prop (Prop $c 'Range') 'Text') } } catch { Note ("Table $t row1 cell read failed") } }
        if ($firstRow.Count -eq 0) {
            try { foreach ($c in $tb.Range.Cells) { if ((Prop $c 'RowIndex') -eq 1) { $firstRow += Txt (Prop (Prop $c 'Range') 'Text') } } } catch {}
        }

        $preview = @()
        if ($rows -and $row1) {
            $maxRows = [int]$rows
            if ($maxRows -gt 6) { $maxRows = 6 }
            for ($r = 1; $r -le $maxRows; $r++) {
                $rr = $null
                try { $rr = $rowsColl.Item($r) } catch {}
                if (-not $rr) { continue }
                $rowTexts = @()
                try { foreach ($c in $rr.Cells) { $rowTexts += Txt (Prop (Prop $c 'Range') 'Text') } } catch {}
                $preview += [ordered]@{ row = $r; cell_count = $rowTexts.Count; texts = $rowTexts }
            }
        }

        $colW = @()
        $columnsColl = $null
        try { $columnsColl = $tb.Columns } catch {}
        if ($columnsColl -and $cols) {
            for ($j = 1; $j -le [int]$cols; $j++) {
                $col = $null
                try { $col = $columnsColl.Item($j) } catch {}
                $w = Num (Prop $col 'Width')
                $colW += [ordered]@{ index = $j; pt = $w; mm = Pt2Mm $w }
            }
        }
        if ($colW.Count -eq 0) {
            try { foreach ($c in $tb.Range.Cells) { if ((Prop $c 'RowIndex') -eq 1) { $w = Num (Prop $c 'Width'); $colW += [ordered]@{ index = Prop $c 'ColumnIndex'; pt = $w; mm = Pt2Mm $w } } } } catch {}
        }
        $sumPt = 0.0
        foreach ($cwid in $colW) { if ($cwid['pt']) { $sumPt += [double]$cwid['pt'] } }

        $tblStyle = $null
        try { $tblStyle = $tb.Style } catch {}
        $row1Repeat = $null
        if ($row1) { try { $row1Repeat = BoolOf (Prop $row1 'HeadingFormat') } catch {} }

        $rec = [ordered]@{
            index = $t
            rows = $rows
            columns = $cols
            cells = $cells
            nest_level = Prop $tb 'NestLevel'
            first_row_texts = $firstRow
            rows_preview = $preview
            column_widths = $colW
            column_width_sum_pt = [math]::Round($sumPt, 2)
            column_width_sum_mm = [math]::Round($sumPt * 25.4 / 72.0, 2)
            indent_from_left_pt = Num (Prop $tb 'IndentFromLeft')
            preferred_width_type = Prop $tb 'PreferredWidthType'
            preferred_width_type_name = EN 'WdPreferredWidthType' (Prop $tb 'PreferredWidthType')
            preferred_value = Num (Prop $tb 'PreferredWidth')
            rows_alignment = PropOf $tb 'Rows' 'Alignment'
            rows_alignment_name = EN 'WdTableAlignment' (PropOf $tb 'Rows' 'Alignment')
            row1_repeat_header_row = $row1Repeat
            table_style_local = Prop $tblStyle 'NameLocal'
            table_style_internal = Prop $tblStyle 'Name'
            cell_margins_pt = [ordered]@{ left = Num (Prop $tb 'LeftPadding'); right = Num (Prop $tb 'RightPadding'); top = Num (Prop $tb 'TopPadding'); bottom = Num (Prop $tb 'BottomPadding') }
            apply_default_cell_margins = BoolOf (Prop $tb 'ApplyDefaultCellMargins')
            auto_fit_to_contents = BoolOf (Prop $tb 'AutoFitToContents')
            auto_fit_to_window = BoolOf (Prop $tb 'AutoFitToWindow')
            borders_enable = BoolOf (PropOf $tb 'Borders' 'Enable')
            borders = EnumBorders (Prop $tb 'Borders')
            range_start = PropOf $tb 'Range' 'Start'
            range_end = PropOf $tb 'Range' 'End'
            page_physical = Info (Prop $tb 'Range') 3
        }
        $tables.Add($rec)
    }
    $out['tables'] = $tables

    # ---------- fields ----------
    function FieldList($container, [string]$scope) {
        $list = @()
        if ($null -eq $container) { return ,$list }
        $cnt = 0
        try { $cnt = [int]$container.Fields.Count } catch { return ,$list }
        for ($i = 1; $i -le $cnt; $i++) {
            $f = $null
            try { $f = $container.Fields.Item($i) } catch { continue }
            $code = $null; $result = $null; $resultPresent = $false
            try { $code = Txt (Prop (Prop $f 'Code') 'Text') } catch {}
            try {
                $r = $f.Result
                if ($r) { $resultPresent = $true; $result = Txt (Prop $r 'Text') }
            } catch { $resultPresent = $false }
            $parentRng = $null; try { $parentRng = $f.Parent } catch {}
            $codeRng = $null; try { $codeRng = $f.Code } catch {}
            $list += [ordered]@{
                scope = $scope
                index = $i
                type = Prop $f 'Type'
                type_name = EN 'WdFieldType' (Prop $f 'Type')
                code = $code
                result = $result
                result_available = $resultPresent
                locked = BoolOf (Prop $f 'IsLocked')
                code_start = Prop $codeRng 'Start'
                code_end = Prop $codeRng 'End'
                story_type = Prop $parentRng 'StoryType'
                story_name = EN 'WdStoryType' (Prop $parentRng 'StoryType')
                in_table = BoolOf (Info $codeRng 12)
                parent_text_head = Clip (Txt (Prop $parentRng 'Text')) 140
            }
        }
        return ,$list
    }
    $out['fields'] = FieldList $doc 'main'

    # ---------- headers / footers ----------
    $hfList = @()
    for ($s = 1; $s -le $secCount; $s++) {
        $sec = $null
        try { $sec = $doc.Sections.Item($s) } catch { continue }
        foreach ($kind in 'Headers', 'Footers') {
            $coll = $null
            try { $coll = $sec.$kind } catch { continue }
            foreach ($te in @( @(1, 'primary'), @(2, 'first'), @(3, 'even') )) {
                $tt = $te[0]; $tname = $te[1]
                $hf = $null
                try { $hf = $coll.Item($tt) } catch {}
                if ($null -eq $hf) { continue }
                $rng = $null; try { $rng = $hf.Range } catch {}
                $shapes = @()
                try {
                    $sr = $rng.ShapeRange
                    $sc = [int]$sr.Count
                    for ($k = 1; $k -le $sc; $k++) {
                        $sh = $sr.Item($k)
                        $teText = $null
                        try { $teText = Txt (Prop (Prop $sh 'TextEffect') 'Text') } catch {}
                        $shapes += [ordered]@{
                            name = Prop $sh 'Name'
                            type_raw = Prop $sh 'Type'
                            text_effect_text = $teText
                            alternative_text = Txt (Prop $sh 'AlternativeText')
                            width_pt = Num (Prop $sh 'Width'); height_pt = Num (Prop $sh 'Height')
                            left_pt = Num (Prop $sh 'Left'); top_pt = Num (Prop $sh 'Top')
                            relative_horizontal_position = Prop $sh 'RelativeHorizontalPosition'
                            relative_vertical_position = Prop $sh 'RelativeVerticalPosition'
                            horizontal_alignment = Prop $sh 'HorizontalAlignment'
                            vertical_alignment = Prop $sh 'VerticalAlignment'
                            wrap_type = Prop $sh 'WrapType'
                            z_order = Prop $sh 'ZOrder'
                            locked = BoolOf (Prop $sh 'Locked')
                            anchor_start = PropOf $sh 'Anchor' 'Start'
                            fill_fore_picture = PropOf $sh 'Fill' 'ForeColorPicture'
                        }
                    }
                } catch { Note ("section $s $kind $tname ShapeRange unavailable") }
                $inl = @()
                try {
                    $ic = [int]$rng.InlineShapes.Count
                    for ($k = 1; $k -le $ic; $k++) {
                        $ish = $rng.InlineShapes.Item($k)
                        $inl += [ordered]@{ name = Prop $ish 'Name'; type = Prop $ish 'Type'; width_pt = Num (Prop $ish 'Width'); height_pt = Num (Prop $ish 'Height'); link_source = PropOf $ish 'LinkFormat' 'SourceFullName' }
                    }
                } catch {}
                $hfParas = @()
                try {
                    $pc = [int]$rng.Paragraphs.Count
                    for ($k = 1; $k -le $pc; $k++) {
                        $p = $rng.Paragraphs.Item($k)
                        $pf = $p.Format; $fnt = $p.Font
                        $hfParas += [ordered]@{
                            index = $k
                            text = Txt (Prop (Prop $p 'Range') 'Text')
                            style = Prop (Prop $p 'Style') 'NameLocal'
                            alignment = EN 'WdParagraphAlignment' (Prop $pf 'Alignment')
                            font_name = Prop $fnt 'Name'
                            font_name_fareast = Prop $fnt 'NameFarEast'
                            font_size_pt = Num (Prop $fnt 'Size')
                            bold = BoolOf (Prop $fnt 'Bold')
                            space_after_pt = Num (Prop $pf 'SpaceAfter')
                            tab_stop_count = (try { [int]$pf.TabStops.Count } catch { $null })
                            borders = EnumBorders (Prop $pf 'Borders')
                        }
                    }
                } catch { Note ("section $s $kind $tname paragraph enumeration failed") }
                $hfList += [ordered]@{
                    section = $s
                    kind = $kind
                    type = $tname
                    type_raw = $tt
                    exists = BoolOf (Prop $hf 'Exists')
                    link_to_previous = BoolOf (Prop $hf 'LinkToPrevious')
                    text = Txt (Prop $rng 'Text')
                    text_plain_controls = (try { $rng.Text } catch { $null })
                    paragraph_count = (try { [int]$rng.Paragraphs.Count } catch { $null })
                    paragraphs = $hfParas
                    fields = FieldList $hf ("$kind/$tname/sec$s")
                    shapes = $shapes
                    inline_shapes = $inl
                    style_names = (@($hfParas | ForEach-Object { $_['style'] }) | Where-Object { $_ })
                }
            }
        }
    }
    $out['headers_footers'] = $hfList

    # ---------- watermark hunt ----------
    $wm = [ordered]@{}
    foreach ($cand in 'WatermarkText', 'WaterMark', 'Watermark') {
        $wm['doc_' + $cand] = Prop $doc $cand
    }
    $hfw = @()
    foreach ($h in $hfList) {
        foreach ($sh in $h['shapes']) {
            $blob = '' + $sh['name'] + '|' + $sh['text_effect_text'] + '|' + $sh['alternative_text']
            if ($blob -match '(?i)water|水印|texteffect|wordart') {
                $hfw += [ordered]@{ where = ('sec' + $h['section'] + ' ' + $h['kind'] + '/' + $h['type']); name = $sh['name']; text_effect_text = $sh['text_effect_text']; type_raw = $sh['type_raw'] }
            }
        }
        if ($h['text'] -and ($h['text'] -match '(?i)water|水印')) {
            $hfw += [ordered]@{ where = ('sec' + $h['section'] + ' ' + $h['kind'] + '/' + $h['type'] + ' (header/footer text)'); name = $null; text_effect_text = $h['text']; type_raw = $null }
        }
    }
    $mainShapes = @()
    try {
        $sr = $doc.Content.ShapeRange
        $sc = [int]$sr.Count
        for ($k = 1; $k -le $sc; $k++) {
            $sh = $sr.Item($k)
            $teText = $null
            try { $teText = Txt (Prop (Prop $sh 'TextEffect') 'Text') } catch {}
            $mainShapes += [ordered]@{ name = Prop $sh 'Name'; type_raw = Prop $sh 'Type'; text_effect_text = $teText; alternative_text = Txt (Prop $sh 'AlternativeText'); width_pt = Num (Prop $sh 'Width'); height_pt = Num (Prop $sh 'Height') }
        }
    } catch { Note 'main Content.ShapeRange unavailable' }
    $out['shapes_main'] = $mainShapes
    $inlMain = @()
    try {
        $ic = [int]$doc.Content.InlineShapes.Count
        for ($k = 1; $k -le $ic; $k++) {
            $ish = $doc.Content.InlineShapes.Item($k)
            $inlMain += [ordered]@{ name = Prop $ish 'Name'; type = Prop $ish 'Type'; width_pt = Num (Prop $ish 'Width'); height_pt = Num (Prop $ish 'Height'); link_source = PropOf $ish 'LinkFormat' 'SourceFullName' }
        }
    } catch { Note 'main Content.InlineShapes unavailable' }
    $out['inline_shapes_main'] = $inlMain
    $wm['header_footer_shape_candidates'] = $hfw
    $wm['main_shape_candidates'] = @($mainShapes | Where-Object { ($_.name -match '(?i)water|texteffect|wordart') -or $_.text_effect_text })
    $out['watermark'] = $wm

    # ---------- TOC / bookmarks / form fields / content controls / doc properties ----------
    $toc = 'unavailable'
    try {
        $l = @()
        for ($i = 1; $i -le [int]$doc.TablesOfContents.Count; $i++) {
            $t = $doc.TablesOfContents.Item($i)
            $l += [ordered]@{
                index = $i
                range_text_head = Clip (Txt (Prop (Prop $t 'Range') 'Text')) 1500
                use_outline_levels = BoolOf (Prop $t 'UseOutlineLevels')
                levels = Prop $t 'Levels'
                right_align = BoolOf (Prop $t 'RightAlign')
                tab_leader = Prop $t 'TabLeader'
                hash_url = BoolOf (Prop $t 'HashUrl')
                page_physical = Info (Prop $t 'Range') 3
                range_start = PropOf $t 'Range' 'Start'
                range_end = PropOf $t 'Range' 'End'
            }
        }
        $toc = $l
    } catch { Note 'TablesOfContents unavailable' }
    $out['toc'] = $toc

    $bm = 'unavailable'
    try {
        $l = @()
        for ($i = 1; $i -le [int]$doc.Bookmarks.Count; $i++) {
            $b = $doc.Bookmarks.Item($i)
            $l += [ordered]@{ index = $i; name = Prop $b 'Name'; start = PropOf $b 'Range' 'Start'; end = PropOf $b 'Range' 'End'; text_head = Clip (Txt (Prop (Prop $b 'Range') 'Text')) 120; page_physical = Info (Prop $b 'Range') 3 }
        }
        $bm = $l
    } catch { Note 'Bookmarks unavailable' }
    $out['bookmarks'] = $bm

    $ff = 'unavailable'
    try {
        $l = @()
        for ($i = 1; $i -le [int]$doc.FormFields.Count; $i++) {
            $f = $doc.FormFields.Item($i)
            $l += [ordered]@{ index = $i; name = Prop $f 'Name'; type = Prop $f 'Type'; result = Txt (Prop $f 'Result'); enabled = BoolOf (Prop $f 'Enabled'); entry_type = Prop $f 'EntryType'; page_physical = Info (Prop $f 'Range') 3 }
        }
        $ff = $l
    } catch { Note 'FormFields unavailable' }
    $out['form_fields'] = $ff

    $cc = 'unavailable'
    try {
        $l = @()
        for ($i = 1; $i -le [int]$doc.ContentControls.Count; $i++) {
            $c = $doc.ContentControls.Item($i)
            $l += [ordered]@{ index = $i; type = Prop $c 'Type'; tag = Prop $c 'Tag'; placeholder = Txt (Prop $c 'PlaceholderText'); text_head = Clip (Txt (Prop (Prop $c 'Range') 'Text')) 120 }
        }
        $cc = $l
    } catch { Note 'ContentControls unavailable' }
    $out['content_controls'] = $cc

    $dp = 'unavailable'
    try {
        $l = @()
        $props = $doc.BuiltInDocumentProperties
        $pc = [int]$props.Count
        for ($i = 1; $i -le $pc; $i++) {
            $p = $props.Item($i)
            $v = $null
            try {
                $v = $p.Value
                if ($v -is [System.__ComObject]) { $v = '<com>' }
            } catch { $v = '<unreadable>' }
            $s = $null
            try { $s = [string]$v } catch { $s = '<conversion-failed>' }
            $l += [ordered]@{ index = $i; name = Prop $p 'Name'; value = Clip $s 300 }
        }
        $dp = $l
    } catch { Note 'BuiltInDocumentProperties unavailable' }
    $out['document_properties'] = $dp

    # ---------- statistics ----------
    $out['statistics'] = [ordered]@{
        section_count = $secCount
        paragraph_count = $paraCount
        table_count = $tblCount
        field_count_main = (try { [int]$doc.Fields.Count } catch { $null })
        style_count_total = $styleCount
        style_count_used_by_paragraphs = $usedOrder.Count
        toc_count = (try { [int]$doc.TablesOfContents.Count } catch { $null })
        bookmark_count = (try { [int]$doc.Bookmarks.Count } catch { $null })
        form_field_count = (try { [int]$doc.FormFields.Count } catch { $null })
        content_control_count = (try { [int]$doc.ContentControls.Count } catch { $null })
        main_shape_count = $mainShapes.Count
        main_inline_shape_count = $inlMain.Count
        pages_computed = (try { [int]$doc.ComputeStatistics(2) } catch { $null })
        words_computed = (try { [int]$doc.ComputeStatistics(0) } catch { $null })
        characters_computed = (try { [int]$doc.ComputeStatistics(3) } catch { $null })
        paragraphs_computed = (try { [int]$doc.ComputeStatistics(4) } catch { $null })
    }

    # ---------- marker scan ----------
    $patterns = [ordered]@{
        signature_labels = '(设计|校对|校核|审核|审定|批准|复核|编制|校对)'
        signature_with_colon = '(设计|校对|校核|审核|审定|批准|编制)[：:]'
        company_like = '(有限公司|公司|设计院|集团|研究院)'
        date_like = '(\d{4}\s*[年\-/\.]\s*\d{1,2}\s*[月\-/\.]\s*\d{1,2}|\d{4}\s*[年\-/\.]\s*\d{1,2})'
        toc_like = '^\s*目\s*录\s*$'
        index_like = '(?i)index'
        calcbook_like = '(计算书|计算依据|计算结果|工程名称|项目名?称?)'
        load_calc_like = '(荷载|应力|挠度|工况|安全|校核|公式|内力)'
        drawing_no_like = '(图号|编号|NO\.|No\.)'
    }
    $markers = [ordered]@{}
    foreach ($key in $patterns.Keys) {
        $re = [string]$patterns[$key]
        $hits = @()
        foreach ($p in $paras) {
            $tx = $p['text']
            if ($tx -and ($tx -match $re)) {
                $hits += [ordered]@{ index = $p['index']; style = $p['style']; text = Clip $tx 200; page_physical = $p['page_physical']; outline_level_name = $p['outline_level_name']; alignment_name = $p['alignment_name'] }
            }
        }
        $markers[$key] = $hits
    }
    $pgFields = @()
    foreach ($f in $out['fields']) {
        if ($f['type_name'] -eq 'wdFieldPage' -or ($f['code'] -and ($f['code'] -match '(?i)PAGE'))) {
            $pgFields += [ordered]@{ scope = $f['scope']; index = $f['index']; code = $f['code']; result = $f['result']; type_name = $f['type_name'] }
        }
    }
    $markers['page_fields'] = $pgFields
    $markers['field_scopes'] = @($out['fields'] | Group-Object { $_['scope'] } | ForEach-Object { [ordered]@{ scope = $_.Name; count = $_.Count } })
    $markers['paragraphs_per_page'] = @($paras | Group-Object { $_['page_physical'] } | ForEach-Object { [ordered]@{ page = $_.Name; paragraph_count = $_.Count } })
    $markers['styles_used_list'] = @($usedOrder)
    $markers['outline_levels_used'] = @($paras | Group-Object { $_['outline_level_name'] } | ForEach-Object { [ordered]@{ outline_level_name = $_.Name; count = $_.Count } })
    $out['markers_scan'] = $markers

    # ---------- optional derived docx for byte-level cross check ----------
    if ($DerivedDocxOut) {
        try {
            $dir = Split-Path -Parent $DerivedDocxOut
            if ($dir) { New-Item -ItemType Directory -Force -Path $dir | Out-Null }
            if (Test-Path -LiteralPath $DerivedDocxOut) { Remove-Item -LiteralPath $DerivedDocxOut -Force }
            $doc.SaveAs2($DerivedDocxOut, 12, $false, '', $false)
            $out['meta']['derived_docx'] = $DerivedDocxOut
            $out['meta']['derived_docx_sha256'] = (Get-FileHash -LiteralPath $DerivedDocxOut -Algorithm SHA256).Hash
        } catch {
            $out['meta']['derived_docx'] = 'FAILED: ' + $_.Exception.Message
            Note ('SaveAs2 failed: ' + $_.Exception.Message)
        }
    }
} catch {
    Note ('FATAL: ' + $_.Exception.Message)
    if (-not $out.Contains('meta')) {
        $out['meta'] = [ordered]@{ source_path = $Path; sha256 = $sha }
    }
} finally {
    if ($doc) { try { $doc.Close(0) } catch {} }
    if ($word) { try { $word.Quit() } catch {} }
    try { [System.Runtime.InteropServices.Marshal]::ReleaseComObject($word) | Out-Null } catch {}
    $word = $null
    [GC]::Collect()
    [GC]::WaitForPendingFinalizers()
}

$out['errors'] = @($errors)
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $JsonOut) | Out-Null
$json = $out | ConvertTo-Json -Depth 12
Set-Content -LiteralPath $JsonOut -Value $json -Encoding UTF8
Write-Host ('JSON written: ' + $JsonOut)
Write-Host ('error count: ' + $errors.Count)
foreach ($e in $errors) { Write-Host ('  ERR: ' + $e) }

