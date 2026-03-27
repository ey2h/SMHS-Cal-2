# Test Aluminum (Mullion/Column) and Rafter/Beam calculations
# Run with: powershell -File test-aluminum.ps1

$BASE_URL = "http://127.0.0.1:5188"
$ALUM_URL = "http://127.0.0.1:5188/api/AluminumCalculation"

Write-Host "=============================================="
Write-Host "  Aluminum Calculation API Tests"
Write-Host "=============================================="
Write-Host ""

# Helper function to display results
function Show-Result {
    param($response, $testName)
    if ($response) {
        $passed = $response.passed
        $defl = $response.maxDeflection
        $deflLimit = $response.deflectionLimit
        $strength = $response.strengthCheck
        Write-Host "  Result: Passed=$passed, Deflection=$defl mm (limit: $deflLimit mm)"
        if ($strength) {
            Write-Host "  Strength: $($strength.passed), Value=$($strength.actualValue) MPa (limit: $($strength.allowableValue) MPa)"
        }
    } else {
        Write-Host "  [ERROR] No response received"
    }
}

# Test 1: Mullion (立柱) calculation - basic
Write-Host "=== Test 1: Mullion (立柱) - Basic ==="
$body = @{
    Height = 3600
    ShortSpan = 600
    LongSpan = 3000
    Spacing = 1200
    ProfileSpec = "150系列"
    WindLoad = 0.0028
    GravityLoad = 0.0005
    SeismicGravityLoad = 0.0005
    AluminumGrade = "6063-T5"
    Ix = 2656350
    Wnx = 40905
    An = 1261.5
    WebThickness = 6
    Sx = 26247
    Gamma = 1.0
} | ConvertTo-Json
$response = Invoke-RestMethod -Uri "$ALUM_URL/Mullion" -Method Post -Body $body -ContentType "application/json"
Show-Result $response "Mullion Basic"

# Test 2: Mullion with different profile
Write-Host ""
Write-Host "=== Test 2: Mullion (立柱) - Stronger Profile ==="
$body = @{
    Height = 3600
    ShortSpan = 600
    LongSpan = 3000
    Spacing = 1500
    ProfileSpec = "180系列"
    WindLoad = 0.0035
    GravityLoad = 0.0006
    SeismicGravityLoad = 0.0006
    AluminumGrade = "6063-T6"
    Ix = 4500000
    Wnx = 55000
    An = 1500
    WebThickness = 8
    Sx = 35000
    Gamma = 1.0
} | ConvertTo-Json
$response = Invoke-RestMethod -Uri "$ALUM_URL/Mullion" -Method Post -Body $body -ContentType "application/json"
Show-Result $response "Mullion Stronger"

# Test 3: Mullion - cantilever model (高风压区)
Write-Host ""
Write-Host "=== Test 3: Mullion (立柱) - High Wind Pressure ==="
$body = @{
    Height = 4200
    ShortSpan = 700
    LongSpan = 3500
    Spacing = 1200
    ProfileSpec = "200系列"
    WindLoad = 0.005
    GravityLoad = 0.0007
    SeismicGravityLoad = 0.0007
    AluminumGrade = "6063A-T6"
    Ix = 6800000
    Wnx = 72000
    An = 1800
    WebThickness = 10
    Sx = 45000
    Gamma = 1.0
} | ConvertTo-Json
$response = Invoke-RestMethod -Uri "$ALUM_URL/Mullion" -Method Post -Body $body -ContentType "application/json"
Show-Result $response "Mullion High Wind"

# Test 4: Rafter (横梁) calculation - basic
Write-Host ""
Write-Host "=== Test 4: Rafter (横梁) - Basic ==="
$body = @{
    Span = 1200
    Spacing = 1500
    ProfileSpec = "50系列"
    WindLoad = 0.0028
    GravityLoad = 0.0004
    AluminumGrade = "6063-T5"
    Ix = 115810
    Iy = 123650
    Wnx = 3662
    Wny = 5763
    An = 449.3
    Sx = 2994
    Sy = 3546
    Tx = 5
    Ty = 3
    Gamma = 1.0
    BetaE = 5.0
    AlphaMax = 0.16
} | ConvertTo-Json
$response = Invoke-RestMethod -Uri "$ALUM_URL/Rafter" -Method Post -Body $body -ContentType "application/json"
Show-Result $response "Rafter Basic"

# Test 5: Rafter with larger span
Write-Host ""
Write-Host "=== Test 5: Rafter (横梁) - Large Span ==="
$body = @{
    Span = 1800
    Spacing = 1500
    ProfileSpec = "60系列"
    WindLoad = 0.003
    GravityLoad = 0.0005
    GravityHeight = 750
    AluminumGrade = "6063-T6"
    Ix = 250000
    Iy = 280000
    Wnx = 5500
    Wny = 7200
    An = 650
    Sx = 4500
    Sy = 5200
    Tx = 6
    Ty = 4
    Gamma = 1.0
} | ConvertTo-Json
$response = Invoke-RestMethod -Uri "$ALUM_URL/Rafter" -Method Post -Body $body -ContentType "application/json"
Show-Result $response "Rafter Large"

# Test 6: Aluminum Plate calculation
Write-Host ""
Write-Host "=== Test 6: Aluminum Plate - Basic ==="
$body = @{
    ShortSide = 600
    LongSide = 1200
    Thickness = 3
    WindLoad = 2.8
    MaterialGrade = "3003-H24"
} | ConvertTo-Json
$response = Invoke-RestMethod -Uri "$ALUM_URL/Plate" -Method Post -Body $body -ContentType "application/json"
Show-Result $response "Plate Basic"

# Test 7: Aluminum Plate - thicker
Write-Host ""
Write-Host "=== Test 7: Aluminum Plate - Thicker ==="
$body = @{
    ShortSide = 800
    LongSide = 1500
    Thickness = 4
    WindLoad = 3.5
    MaterialGrade = "5052-H32"
} | ConvertTo-Json
$response = Invoke-RestMethod -Uri "$ALUM_URL/Plate" -Method Post -Body $body -ContentType "application/json"
Show-Result $response "Plate Thicker"

Write-Host ""
Write-Host "=============================================="
Write-Host "  Aluminum Calculation Tests Complete"
Write-Host "=============================================="
