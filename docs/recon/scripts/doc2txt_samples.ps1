# 用 Word COM 把 V31 自带计算书样本 DOC 转成纯文本（只读打开副本，不动原文件）
# 产出：docs/recon/calcbooks/txt2/<名称>.txt（UTF-8 无 BOM）
$ErrorActionPreference = 'Stop'
try { [Console]::OutputEncoding = [System.Text.Encoding]::UTF8 } catch {}

$src = 'C:\Program Files (x86)\汇宝幕墙门窗结构计算软件V31.0D\计算书样本文件集'
$tmp = 'C:\code\Glass\docs\recon\calcbooks\raw\_tmp'
$out = 'C:\code\Glass\docs\recon\calcbooks\txt2'
foreach ($d in @($tmp, $out)) { if (-not (Test-Path -LiteralPath $d)) { New-Item -ItemType Directory -Force -Path $d | Out-Null } }

$word = $null
try {
    $word = New-Object -ComObject Word.Application
    $word.Visible = $false
    $word.DisplayAlerts = 0

    Get-ChildItem -LiteralPath $src -Filter '*.DOC' | ForEach-Object {
        $name = [System.IO.Path]::GetFileNameWithoutExtension($_.Name)
        $copy = Join-Path $tmp ($name + '.doc')
        Copy-Item -LiteralPath $_.FullName -Destination $copy -Force
        $doc = $word.Documents.Open($copy, $false, $true)   # ConfirmConversions=false, ReadOnly=true
        try {
            $txt = $doc.Content.Text
            # Word 用 CR(13) 分段、Chr(7) 表示表格单元格结束
            $txt = $txt.Replace([string][char]13, "`n").Replace([string][char]7, "`t")
            $target = Join-Path $out ($name + '.txt')
            [System.IO.File]::WriteAllText($target, $txt, (New-Object System.Text.UTF8Encoding($false)))
            Write-Output ("OK  {0}  {1} chars" -f $name, $txt.Length)
        } finally {
            $doc.Close(0)
        }
    }
} finally {
    if ($word) { $word.Quit(); [void][System.Runtime.InteropServices.Marshal]::ReleaseComObject($word) }
}
