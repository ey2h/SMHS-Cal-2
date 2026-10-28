$raw = 'C:\code\Glass\docs\recon\calcbooks\raw'
Get-ChildItem -LiteralPath $raw -Filter *.doc | ForEach-Object {
    $bytes = [System.IO.File]::ReadAllBytes($_.FullName)
    $head = $bytes[0..([Math]::Min(15, $bytes.Length - 1))]
    $hex = ($head | ForEach-Object { $_.ToString('x2') }) -join ' '
    $txt = -join ($head | ForEach-Object { if ($_ -ge 32 -and $_ -lt 127) { [char]$_ } else { '.' } })
    '{0}|{1}|{2}|{3}' -f $_.Name, $bytes.Length, $hex, $txt
}
