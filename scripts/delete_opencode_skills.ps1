$path = Join-Path $env:USERPROFILE '.config\opencode\skills'
if (Test-Path $path) {
    Remove-Item -Path $path -Recurse -Force
    Write-Output "Deleted: $path"
} else {
    Write-Output "NotFound: $path"
}
