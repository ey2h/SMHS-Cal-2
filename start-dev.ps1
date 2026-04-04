param(
    [ValidateSet('http', 'https')]
    [string]$Profile = 'http'
)

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$apiProj = Join-Path $root 'src\GlassManagement.Api\GlassManagement.Api.csproj'
$webProj = Join-Path $root 'src\GlassManagement.Web\GlassManagement.Web.csproj'

Write-Host "Starting API and Web with profile '$Profile'..." -ForegroundColor Cyan

$running = Get-Process -Name 'GlassManagement.Web','GlassManagement.Api' -ErrorAction SilentlyContinue
if ($running) {
    Write-Host "Stopping existing GlassManagement processes to avoid DLL locks..." -ForegroundColor Yellow
    $running | Stop-Process -Force
    Start-Sleep -Seconds 2
}

Write-Host "Building solution once before launching..." -ForegroundColor Cyan
$buildTargets = @(
    $apiProj,
    $webProj
)

foreach ($target in $buildTargets) {
    Write-Host "Building $target ..." -ForegroundColor Cyan
    dotnet build $target -v minimal -m:1
    if ($LASTEXITCODE -ne 0) {
        throw "Build failed for $target. Fix the build errors above before starting the apps."
    }
}

Start-Process -FilePath powershell.exe -ArgumentList @(
    '-NoExit',
    '-Command',
    "Set-Location '$root'; dotnet run --no-build --project '$apiProj' --launch-profile $Profile"
)

Start-Process -FilePath powershell.exe -ArgumentList @(
    '-NoExit',
    '-Command',
    "Set-Location '$root'; dotnet run --no-build --project '$webProj' --launch-profile $Profile"
)

Start-Sleep -Seconds 8

$url = if ($Profile -eq 'https') { 'https://localhost:7007' } else { 'http://localhost:5143' }
Start-Process $url
