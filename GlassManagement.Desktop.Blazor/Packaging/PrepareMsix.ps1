# Prepare MSIX packaging for MAUI Blazor Desktop
# This is a scaffold script to be refined with your corporate packaging workflow.
# Usage: powershell -File PrepareMsix.ps1 -SolutionPath <path> -OutputDir <path> -CertPath <pfx>

param(
  [string]$SolutionPath = "./",
  [string]$OutputDir = "./msix-output",
  [string]$CertPath = "$PSScriptRoot\certs\codesign.pfx",
  [string]$CertPassword = ""
)

Write-Host "[MSIX] Preparing packaging..."
if (-not (Test-Path $OutputDir)) { New-Item -ItemType Directory -Path $OutputDir | Out-Null }

Write-Host "[MSIX] Building project..."
try {
  dotnet publish "$SolutionPath" -c Release -o "$OutputDir/publish" -nologo
} catch {
  Write-Error "Failed to publish the project: $_"
  exit 1
}

Write-Host "[MSIX] Packaging step is project-specific. Integrate with your MSIX packaging toolchain here."
Write-Host "[MSIX] Output directory: $OutputDir"
Write-Host "[MSIX] Certificate: $CertPath"
Write-Host "[MSIX] Done."
