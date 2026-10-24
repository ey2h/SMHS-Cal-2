# rar_listing_extract.ps1
# Uses the ALREADY INSTALLED WinRAR UnRAR.exe (nothing is installed by this script).
# Step 1: list every .rar entry (UnRAR vt) -> reports/rar_listing.txt + reports/rar_listing.json
# Step 2: extract ONLY the archive that is spec material (节能计算基本原则与汇总表.rar)
#         into docs\recon\attachments\extracted\ ; the other two are environment/utility archives
#         and are listed but not extracted (their payloads are executables/batch scripts).
$ErrorActionPreference = 'Continue'
try { [Console]::OutputEncoding = [System.Text.Encoding]::UTF8 } catch {}

$unrar = 'C:\Program Files\WinRAR\UnRAR.exe'
$src   = 'C:\Program Files (x86)\汇宝幕墙门窗结构计算软件V35.0\附件'
$reports = 'C:\code\Glass\docs\recon\reports'
$extractRoot = 'C:\code\Glass\docs\recon\attachments\extracted'
$listingTxt = Join-Path $reports 'rar_listing.txt'
$listingJson = Join-Path $reports 'rar_listing.json'

if (-not (Test-Path -LiteralPath $unrar)) { Write-Host 'UnRAR.exe not present - aborting'; exit 2 }
foreach ($d in @($reports, $extractRoot)) { if (-not (Test-Path -LiteralPath $d)) { New-Item -ItemType Directory -Force -Path $d | Out-Null } }

$extractThese = @('节能计算基本原则与汇总表.rar')

$enc = New-Object System.Text.UTF8Encoding($true)
$allTxt = New-Object System.Text.StringBuilder
$records = @()

foreach ($f in Get-ChildItem -LiteralPath $src -Filter '*.rar' -File) {
  Write-Host ('=== ' + $f.Name)
  [void]$allTxt.AppendLine('======================================================')
  [void]$allTxt.AppendLine('ARCHIVE: ' + $f.FullName + '  size=' + $f.Length)
  $out = & $unrar vt -y $f.FullName 2>&1 | Out-String
  [void]$allTxt.AppendLine($out)

  # parse the "Path = ..." / "Type = ..." / "Size = ..." blocks produced by `unrar vt`
  $entries = @()
  $cur = $null
  foreach ($line in ($out -split "`r?`n")) {
    $l = $line.Trim()
    if ($l -match '^Path\s*=\s*(.+)$') {
      if ($cur) { $entries += $cur }
      $cur = [ordered]@{ path = $matches[1] }
    } elseif ($null -ne $cur -and $l -match '^Size\s*=\s*([\d,]+)') {
      $cur['sizeText'] = $matches[1]
      $cur['unpackedBytes'] = [int64](($matches[1] -replace ',', ''))
    } elseif ($null -ne $cur -and $l -match '^Type\s*=\s*(.+)$') {
      $cur['type'] = $matches[1]
    } elseif ($null -ne $cur -and $l -match '^Packed size\s*=\s*([\d,]+)') {
      $cur['packedText'] = $matches[1]
    } elseif ($null -ne $cur -and $l -match '^Modified\s*=\s*(.+)$') {
      $cur['modified'] = $matches[1]
    } elseif ($null -ne $cur -and $l -match '^Version needed to extract\s*=\s*(.+)$') {
      $cur['versionNeeded'] = $matches[1]
    } elseif ($null -ne $cur -and $l -match '^Compression method\s*=\s*(.+)$') {
      $cur['compression'] = $matches[1]
    } elseif ($null -ne $cur -and $l -match '^Encrypted\s*=\s*(.+)$') {
      $cur['encrypted'] = $matches[1]
    }
  }
  if ($cur) { $entries += $cur }

  $rec = [ordered]@{
    archive = $f.Name
    archivePath = $f.FullName
    archiveBytes = $f.Length
    listedEntries = $entries.Count
    entries = $entries
    extracted = $false
    extractDir = $null
    extractedFiles = @()
  }

  if ($extractThese -contains $f.Name) {
    $destDir = Join-Path $extractRoot ([System.IO.Path]::GetFileNameWithoutExtension($f.Name))
    if (Test-Path -LiteralPath $destDir) { Remove-Item -LiteralPath $destDir -Recurse -Force }
    New-Item -ItemType Directory -Force -Path $destDir | Out-Null
    Write-Host ('  extracting to ' + $destDir)
    $xout = & $unrar x -y -inul $f.FullName $destDir 2>&1 | Out-String
    [void]$allTxt.AppendLine('--- extraction output ---')
    [void]$allTxt.AppendLine($xout)
    $rec.extracted = $true
    $rec.extractDir = $destDir
    foreach ($x in (Get-ChildItem -LiteralPath $destDir -Recurse -File -Force)) {
      $rec.extractedFiles += [ordered]@{
        path = $x.FullName
        relPath = $x.FullName.Substring($destDir.Length + 1)
        bytes = $x.Length
        ext = $x.Extension.ToLower()
        sha256 = (Get-FileHash -LiteralPath $x.FullName -Algorithm SHA256).Hash
      }
      Write-Host ('  extracted: ' + $x.FullName + ' ' + $x.Length + ' bytes')
    }
  } else {
    Write-Host '  listed only (not extracted: payload is utility/executable material, not spec text)'
  }
  $records += New-Object psobject -Property $rec
}

[System.IO.File]::WriteAllText($listingTxt, $allTxt.ToString(), $enc)
$payload = New-Object psobject -Property ([ordered]@{
  generatedAt = (Get-Date).ToString('o')
  tool = $unrar
  toolNote = 'WinRAR UnRAR.exe was already installed on this machine; nothing was installed.'
  policy = 'all archives listed with "unrar vt"; only the spec-related archive was extracted'
  archives = $records
})
[System.IO.File]::WriteAllText($listingJson, ($payload | ConvertTo-Json -Depth 8), $enc)
Write-Host ('wrote ' + $listingTxt)
Write-Host ('wrote ' + $listingJson)
