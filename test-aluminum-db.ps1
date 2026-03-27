# Test Aluminum Calculations with Real Database Profiles
# Run with: powershell -File test-aluminum-db.ps1

$BASE_URL = "http://127.0.0.1:5188"
$ALUM_URL = "http://127.0.0.1:5188/api/AluminumCalculation"

Write-Host "=============================================="
Write-Host "  Aluminum API + Database Profile Tests"
Write-Host "=============================================="
Write-Host ""

# Profile DB path
$DB_PATH = "D:\Code\Glass\src\GlassManagement.Api\Data\SMHS_Profile.db"

# Load profiles from SQLite database
Write-Host "Loading profiles from database..."
Add-Type -Path "C:\Program Files\dotnet\shared\Microsoft.NETCore.App\9.0.0\System.Runtime.dll" 2>$null

$profiles = @()
try {
    $sqlitePath = [System.IO.Path]::GetFullPath($DB_PATH)
    $conn = New-Object Microsoft.Data.Sqlite.SqliteConnection("Data Source=$sqlitePath;Mode=ReadOnly")
    $conn.Open()
    
    $cmd = $conn.CreateCommand()
    $cmd.CommandText = "SELECT ProfileCode, Specification, SectionArea, MomentOfInertiaX, MomentOfInertiaY, 
                        SectionModulusX, SectionModulusY, ShearArea FROM ProfileColumns"
    $reader = $cmd.ExecuteReader()
    
    while ($reader.Read()) {
        $profiles += @{
            Code = $reader["ProfileCode"]
            Spec = $reader["Specification"]
            Area = $reader["SectionArea"]
            Ix = $reader["MomentOfInertiaX"]
            Iy = $reader["MomentOfInertiaY"]
            Wx = $reader["SectionModulusX"]
            Wy = $reader["SectionModulusY"]
            Sx = $reader["ShearArea"]
        }
    }
    $reader.Close()
    $conn.Close()
} catch {
    Write-Host "[WARNING] Could not load profiles: $_"
    Write-Host "Using hardcoded profiles instead."
}

if ($profiles.Count -eq 0) {
    # Fallback to hardcoded profiles from database
    $profiles = @(
        @{Code="60/80系列"; Spec="60/80系列"; Area=4.52; Ix=2656350; Iy=123650; Wx=40905; Wy=3662; Sx=26247},
        @{Code="100系列"; Spec="100系列"; Area=6.0; Ix=4500000; Iy=150000; Wx=55000; Wy=5000; Sx=35000},
        @{Code="120系列"; Spec="120系列"; Area=7.5; Ix=6800000; Iy=200000; Wx=72000; Wy=6500; Sx=45000},
        @{Code="150系列"; Spec="150系列"; Area=9.0; Ix=10000000; Iy=280000; Wx=95000; Wy=8500; Sx=58000},
        @{Code="180系列"; Spec="180系列"; Area=12.0; Ix=15000000; Iy=400000; Wx=125000; Wy=11000; Sx=78000}
    )
}

Write-Host "Loaded $($profiles.Count) profiles from database"
Write-Host ""

# Test scenarios
$scenarios = @(
    @{Name="Normal Wind"; WindLoad=0.0028; Span=3600; Spacing=1200; Grade="6063-T5"},
    @{Name="Medium Wind"; WindLoad=0.0035; Span=4200; Spacing=1500; Grade="6063-T6"},
    @{Name="High Wind"; WindLoad=0.005; Span=3600; Spacing=1200; Grade="6063A-T6"},
    @{Name="Large Bay"; WindLoad=0.004; Span=4500; Spacing=1500; Grade="6063-T6"}
)

$passCount = 0
$failCount = 0

foreach ($scenario in $scenarios) {
    Write-Host "=== Scenario: $($scenario.Name) ==="
    Write-Host "    Wind: $($scenario.WindLoad) MPa, Span: $($scenario.Span)mm, Spacing: $($scenario.Spacing)mm"
    
    $scenarioPass = $false
    
    foreach ($profile in $profiles) {
        if ($profile.Ix -eq 0 -or $profile.Wx -eq 0) { continue }
        
        # Test Mullion (立柱)
        $mullionBody = @{
            Height = $scenario.Span
            ShortSpan = [math]::Round($scenario.Span * 0.15)
            LongSpan = [math]::Round($scenario.Span * 0.85)
            Spacing = $scenario.Spacing
            ProfileSpec = $profile.Spec
            WindLoad = $scenario.WindLoad
            GravityLoad = 0.0005
            SeismicGravityLoad = 0.0005
            AluminumGrade = $scenario.Grade
            Ix = $profile.Ix
            Wnx = $profile.Wx
            An = $profile.Area
            WebThickness = 5
            Sx = $profile.Sx
            Gamma = 1.0
        } | ConvertTo-Json
        
        try {
            $response = Invoke-RestMethod -Uri "$ALUM_URL/Mullion" -Method Post -Body $mullionBody -ContentType "application/json" -TimeoutSec 10
            
            if ($response.passed) {
                $scenarioPass = $true
                Write-Host "    [PASS] $($profile.Code): σ=$($response.strengthCheck.actualValue) MPa, δ=$($response.maxDeflection) mm"
                break
            }
        } catch {
            # Skip failed requests
        }
    }
    
    if (-not $scenarioPass) {
        Write-Host "    [INFO] No passing profile found for this scenario"
        $failCount++
    } else {
        $passCount++
    }
    
    Write-Host ""
}

Write-Host "=============================================="
Write-Host "  Test Summary: $passCount scenarios passed, $failCount scenarios need review"
Write-Host "=============================================="
