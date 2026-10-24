$ErrorActionPreference = 'Stop'
$OutputEncoding = [System.Text.Encoding]::UTF8

$paths = @(
    'C:\Program Files (x86)\汇宝幕墙门窗结构计算软件V35.0\jss\模板.doc',
    'C:\Program Files (x86)\汇宝幕墙门窗结构计算软件V35.0\jss\mmbk.CAM'
)

$results = @()
foreach ($p in $paths) {
    if (Test-Path -LiteralPath $p) {
        $i = Get-Item -LiteralPath $p
        $h = (Get-FileHash -LiteralPath $p -Algorithm SHA256).Hash
        # first 16 bytes for OLE magic check
        $fs = [System.IO.File]::OpenRead($p)
        try {
            $buf = New-Object byte[] 16
            [void]$fs.Read($buf, 0, 16)
        } finally { $fs.Dispose() }
        $magic = ($buf | ForEach-Object { $_.ToString('x2') }) -join ' '
        $results += [pscustomobject]@{
            Path      = $p
            Size      = $i.Length
            LastWrite = $i.LastWriteTime.ToString('yyyy-MM-dd HH:mm:ss')
            SHA256    = $h
            Magic16   = $magic
        }
    }
    else {
        $results += [pscustomobject]@{ Path = $p; Size = -1; LastWrite = 'MISSING'; SHA256 = 'MISSING'; Magic16 = '' }
    }
}

foreach ($r in $results) {
    Write-Output ("PATH : " + $r.Path)
    Write-Output ("SIZE : " + $r.Size)
    Write-Output ("MTIME: " + $r.LastWrite)
    Write-Output ("SHA256: " + $r.SHA256)
    Write-Output ("MAGIC : " + $r.Magic16)
    Write-Output "---"
}

if ($results.Count -eq 2 -and $results[0].SHA256 -eq $results[1].SHA256) {
    Write-Output "IDENTICAL_CONTENT = TRUE"
} else {
    Write-Output "IDENTICAL_CONTENT = FALSE"
}

# Also list other candidate template-ish files in jss dir
$jss = 'C:\Program Files (x86)\汇宝幕墙门窗结构计算软件V35.0\jss'
if (Test-Path -LiteralPath $jss) {
    Write-Output "=== jss dir listing (doc/docx/CAM/template-ish) ==="
    Get-ChildItem -LiteralPath $jss -File | Where-Object { $_.Extension -match '^(?i)\.(doc|docx|cam|rtf|dot)$' -or $_.Name -match '模板' } |
        ForEach-Object {
            $h = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash
            Write-Output ("{0,-40} {1,10} B  {2}  {3}" -f $_.Name, $_.Length, $h.Substring(0,16), $_.LastWriteTime.ToString('yyyy-MM-dd HH:mm'))
        }
}

