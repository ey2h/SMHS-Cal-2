<# Run all Playwright UI tests in a single orchestrated command
   - Assumes backend API is running at localhost:5000
   - Uses dotnet test to run the Playwright-based UI tests
   - Stops any leftover browser processes afterwards
>
param(
  [string]$TestProjectPath = "./../../GlassManagement.Ui.Tests/PlaywrightTests/PlaywrightTests.csproj",
  [string]$ApiBaseUrl = "http://localhost:5000",
  [string]$Browser = "all"  # all/chromium/firefox/webkit
)

Write-Host "[TestRun] Starting UI Playwright tests against API at $ApiBaseUrl"

try {
  # Ensure browsers are installed for Playwright if needed
  # The first run may download browsers; subsequent runs reuse them
  & dotnet tool restore -g
  & dotnet tool update --global Microsoft.Playwright.CLI --version 1.0.0 2>$null
  & pwsh -NoLogo -NoProfile -Command "if (-not (Test-Path ./../../GlassManagement.Ui.Tests/PlaywrightTests/.playwright)) { }" 2>$null

  # Run tests
  & dotnet test $TestProjectPath -v minimal
  $ExitCode = $LASTEXITCODE
  if ($ExitCode -eq 0) {
    Write-Host "[TestRun] UI tests passed."
  } else {
    Write-Host "[TestRun] UI tests failed with exit code $ExitCode" -ForegroundColor Red
  }
} catch {
  Write-Error "[TestRun] Exception occurred: $_"
  $ExitCode = 1
}

# Always stop browsers to avoid lingering processes
.
& "$PSScriptRoot\stop-browsers.ps1"
exit $ExitCode
