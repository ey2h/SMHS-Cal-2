param([Parameter(Mandatory = $true)][string]$Script)
$p = (Resolve-Path -LiteralPath $Script).Path
# normalize to UTF-8 with BOM so Windows PowerShell 5.1 reads non-ASCII literals correctly
$text = Get-Content -LiteralPath $p -Raw -Encoding UTF8
Set-Content -LiteralPath $p -Value $text -Encoding UTF8

$tokens = $null
$errs = $null
[void][System.Management.Automation.Language.Parser]::ParseFile($p, [ref]$tokens, [ref]$errs)
if (-not $errs -or $errs.Count -eq 0) {
    Write-Host 'PARSE OK'
} else {
    Write-Host ('PARSE ERRORS: ' + $errs.Count)
    foreach ($e in $errs) {
        Write-Host ('  line ' + $e.Extent.StartLineNumber + ': ' + $e.Message)
        Write-Host ('    >>> ' + $e.Extent.Text)
    }
}
