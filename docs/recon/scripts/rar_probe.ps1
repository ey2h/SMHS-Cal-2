# rar_probe.ps1
# Report only: .rar files (name/size/hash/header signature) + availability of extraction tools.
# It does NOT extract anything and does not install anything.
$ErrorActionPreference = 'Continue'
$src = 'C:\Program Files (x86)\汇宝幕墙门窗结构计算软件V35.0\附件'
$out = 'C:\code\Glass\docs\recon\reports\rar_report.json'

function Get-SigInfo([string]$path) {
  $bytes = [System.IO.File]::ReadAllBytes($path)
  $take = [Math]::Min(8, $bytes.Length)
  $slice = $bytes[0..($take - 1)]
  $hex = ($slice | ForEach-Object { $_.ToString('x2') }) -join ' '
  $sig = 'not a RAR signature'
  if ($hex -like '52 61 72 21 1a 07 01 00*') { $sig = 'RAR 5.x archive header' }
  elseif ($hex -like '52 61 72 21 1a 07 00*') { $sig = 'RAR 4.x archive header' }
  elseif ($hex -like '52 61 72 21 1a 07*') { $sig = 'RAR (unknown sub-version)' }
  $o = New-Object psobject -Property ([ordered]@{ first8Hex = $hex; signature = $sig })
  return $o
}

$files = @()
foreach ($f in Get-ChildItem -LiteralPath $src -Filter '*.rar' -File) {
  $sha = (Get-FileHash -LiteralPath $f.FullName -Algorithm SHA256).Hash
  $sig = Get-SigInfo $f.FullName
  $files += (New-Object psobject -Property ([ordered]@{
    name = $f.Name
    path = $f.FullName
    sizeBytes = $f.Length
    sha256 = $sha
    first8Hex = $sig.first8Hex
    signature = $sig.signature
    extracted = $false
    note = 'no extraction performed (task forbids installing third-party tools)'
  }))
}

function Probe-Command([string]$name) {
  $c = Get-Command $name -ErrorAction SilentlyContinue
  if ($c) {
    return (New-Object psobject -Property ([ordered]@{ name = $name; available = $true; source = 'PATH'; path = $c.Source }))
  }
  return (New-Object psobject -Property ([ordered]@{ name = $name; available = $false; source = 'none'; path = $null }))
}

$candidates = @()
foreach ($name in @('unrar', 'rar', '7z', '7za', 'WinRAR', 'bandizip', 'bsdtar', 'tar')) {
  $candidates += Probe-Command $name
}
$fixedPaths = @(
  'C:\Program Files\7-Zip\7z.exe',
  'C:\Program Files (x86)\7-Zip\7z.exe',
  'C:\Program Files\WinRAR\WinRAR.exe',
  'C:\Program Files\WinRAR\UnRAR.exe',
  'C:\Program Files (x86)\WinRAR\WinRAR.exe',
  'C:\Program Files (x86)\WinRAR\UnRAR.exe',
  'C:\Program Files\Bandizip\bz.exe',
  'C:\Program Files (x86)\Bandizip\bz.exe'
)
foreach ($p in $fixedPaths) {
  $candidates += (New-Object psobject -Property ([ordered]@{
    name = [System.IO.Path]::GetFileName($p)
    available = (Test-Path -LiteralPath $p)
    source = 'fixed path'
    path = $p
  }))
}

# Shell.Application route (uses an already-registered archive handler; installs nothing)
$shellAvailable = $false
$rarAssoc = $null
try {
  $sh = New-Object -ComObject Shell.Application
  $shellAvailable = ($null -ne $sh)
} catch { $shellAvailable = $false }
try {
  $p = Get-ItemProperty -Path 'Registry::HKEY_CLASSES_ROOT\.rar' -ErrorAction SilentlyContinue
  if ($p) { $rarAssoc = $p.'(default)' }
} catch { $rarAssoc = $null }

$shellRoute = New-Object psobject -Property ([ordered]@{
  shellApplicationAvailable = $shellAvailable
  classRootDotRarHandler = $rarAssoc
})

$payload = New-Object psobject -Property ([ordered]@{
  generatedAt = (Get-Date).ToString('o')
  policy = 'report only; nothing extracted; nothing installed'
  files = $files
  toolProbe = $candidates
  shellRoute = $shellRoute
})
$enc = New-Object System.Text.UTF8Encoding($true)
[System.IO.File]::WriteAllText($out, ($payload | ConvertTo-Json -Depth 6), $enc)
Write-Host "wrote $out"
$files | ForEach-Object { Write-Host ($_.name + ' ' + $_.sizeBytes + ' ' + $_.signature) }
$avail = @($candidates | Where-Object { $_.available })
if ($avail.Count -eq 0) { Write-Host 'NO extraction tool found on this machine' }
else { $avail | ForEach-Object { Write-Host ('TOOL AVAILABLE: ' + $_.name + ' -> ' + $_.path) } }
Write-Host ('Shell.Application available: ' + $shellAvailable + ' ; .rar handler: ' + $rarAssoc)
