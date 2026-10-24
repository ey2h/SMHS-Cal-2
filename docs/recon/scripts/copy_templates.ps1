$ErrorActionPreference = 'Stop'
$raw = 'C:\code\Glass\docs\recon\template\raw'
New-Item -ItemType Directory -Force -Path $raw | Out-Null
New-Item -ItemType Directory -Force -Path 'C:\code\Glass\docs\recon\reports' | Out-Null

$pairs = @(
    [pscustomobject]@{ Src='C:\Program Files (x86)\汇宝幕墙门窗结构计算软件V35.0\jss\模板.doc';    Dst='C:\code\Glass\docs\recon\template\raw\template_模板.doc' },
    [pscustomobject]@{ Src='C:\Program Files (x86)\汇宝幕墙门窗结构计算软件V35.0\jss\mmbk.CAM'; Dst='C:\code\Glass\docs\recon\template\raw\mmbk_CAM_as_doc.doc' }
)
foreach ($p in $pairs) {
    Copy-Item -LiteralPath $p.Src -Destination $p.Dst -Force
    $h = (Get-FileHash -LiteralPath $p.Dst -Algorithm SHA256).Hash
    $hs = (Get-FileHash -LiteralPath $p.Src -Algorithm SHA256).Hash
    Write-Output ("COPIED {0}  size={1}  SHA256={2}  MATCHES_SOURCE={3}" -f $p.Dst, (Get-Item -LiteralPath $p.Dst).Length, $h, ($h -eq $hs))
}

