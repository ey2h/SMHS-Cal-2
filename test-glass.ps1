# 玻璃计算测试脚本
# 测试各种玻璃组合

$baseUrl = "http://127.0.0.1:5188"

Write-Host "=== 玻璃计算测试 ===" -ForegroundColor Cyan

# 测试参数
$shortSide = 1000
$longSide = 2000
$windLoad = 1.5  # kN/m²

function Test-SingleGlass {
    Write-Host "`n=== 单片玻璃测试 (6mm-12mm) ===" -ForegroundColor Yellow
    $thicknesses = @(6, 8, 10, 12)
    foreach ($t in $thicknesses) {
        $body = @{
            shortSide = $shortSide
            longSide = $longSide
            thickness = $t
            glassType = "Tempered"
            windLoad = $windLoad
            supportCondition = "FourSides"
        } | ConvertTo-Json
        
        $response = Invoke-RestMethod -Uri "$baseUrl/api/GlassCalculation/SingleGlass" -Method Post -Body $body -ContentType "application/json"
        $status = if ($response.passed) { "✅ PASS" } else { "❌ FAIL" }
        Write-Host "单片 ${t}mm: σ=$($response.maxStress)MPa, df=$($response.maxDeflection)mm $status"
    }
}

function Test-InsulatingGlass {
    Write-Host "`n=== 中空玻璃测试 (6+6到12+12) ===" -ForegroundColor Yellow
    $thicknesses = @(6, 8, 10, 12)
    foreach ($t1 in $thicknesses) {
        foreach ($t2 in $thicknesses) {
            $body = @{
                shortSide = $shortSide
                longSide = $longSide
                outerThickness = $t1
                innerThickness = $t2
                outerGlassType = "Tempered"
                innerGlassType = "Tempered"
                windLoad = $windLoad
                altitudeDifference = 0
                temperatureDifference = 0
            } | ConvertTo-Json
            
            $response = Invoke-RestMethod -Uri "$baseUrl/api/GlassCalculation/InsulatingGlass" -Method Post -Body $body -ContentType "application/json"
            $status = if ($response.passed) { "✅ PASS" } else { "❌ FAIL" }
            Write-Host "中空 ${t1}+${t2}: σ=$($response.maxStress)MPa, df=$($response.maxDeflection)mm $status"
        }
    }
}

function Test-LaminatedGlass {
    Write-Host "`n=== 夹胶玻璃测试 ===" -ForegroundColor Yellow
    $thicknesses = @(6, 8, 10, 12)
    foreach ($t in $thicknesses) {
        $body = @{
            shortSide = $shortSide
            longSide = $longSide
            thicknesses = @($t, $t)
            glassType = "Tempered"
            interlayerType = "PVB"
            interlayerThickness = 1.52
            windLoad = $windLoad
        } | ConvertTo-Json
        
        $response = Invoke-RestMethod -Uri "$baseUrl/api/GlassCalculation/LaminatedGlass" -Method Post -Body $body -ContentType "application/json"
        $status = if ($response.passed) { "✅ PASS" } else { "❌ FAIL" }
        Write-Host "夹胶 ${t}+${t}: σ=$($response.maxStress)MPa, df=$($response.maxDeflection)mm $status"
    }
}

function Test-UnifiedCalculate {
    Write-Host "`n=== 统一计算V2测试 (复杂组合) ===" -ForegroundColor Yellow
    
    # 中空夹胶: 6+pvb+6+12A+6 到 12+pvb+12+12A+12
    Write-Host "`n--- 中空夹胶玻璃 ---" -ForegroundColor Green
    $outerThick = @(6, 8, 10, 12)
    $innerThick = @(6, 8, 10, 12)
    foreach ($ot in $outerThick) {
        foreach ($it in $innerThick) {
            $body = @{
                shortSide = $shortSide
                longSide = $longSide
                glass = @{
                    "#type" = "InsulatingGlass, GlassManagement.Core"
                    OuterGlass = @{
                        "#type" = "LaminatedGlass, GlassManagement.Core"
                        Sheets = @(
                            @{ Thickness = $ot; TemperedType = "FullTempered" },
                            @{ Thickness = $ot; TemperedType = "FullTempered" }
                        )
                        InterlayerType = "PVB"
                    }
                    InnerGlass = @{
                        "#type" = "SingleTemperedGlass, GlassManagement.Core"
                        Thickness = $it
                        TemperedType = "FullTempered"
                    }
                    AirSpaceThickness = 12
                }
                windLoad = $windLoad
            } | ConvertTo-Json -Depth 10
            
            try {
                $response = Invoke-RestMethod -Uri "$baseUrl/api/GlassCalculation/UnifiedCalculateV2" -Method Post -Body $body -ContentType "application/json"
                $status = if ($response.passed) { "✅ PASS" } else { "❌ FAIL" }
                Write-Host "中空夹胶 ${ot}+pvb+${ot}+12A+${it}: σ=$($response.maxStress)MPa, df=$($response.maxDeflection)mm $status"
            } catch {
                Write-Host "中空夹胶 ${ot}+pvb+${ot}+12A+${it}: ❌ ERROR - $($_.Exception.Message)" -ForegroundColor Red
            }
        }
    }
    
    # 双夹胶中空: 6+pvb+6+12A+6+pvb+6
    Write-Host "`n--- 双夹胶中空玻璃 ---" -ForegroundColor Green
    $thicknesses = @(6, 8, 10, 12)
    foreach ($t in $thicknesses) {
        $body = @{
            shortSide = $shortSide
            longSide = $longSide
            glass = @{
                "#type" = "InsulatingGlass, GlassManagement.Core"
                OuterGlass = @{
                    "#type" = "LaminatedGlass, GlassManagement.Core"
                    Sheets = @(
                        @{ Thickness = $t; TemperedType = "FullTempered" },
                        @{ Thickness = $t; TemperedType = "FullTempered" }
                    )
                    InterlayerType = "PVB"
                }
                InnerGlass = @{
                    "#type" = "LaminatedGlass, GlassManagement.Core"
                    Sheets = @(
                        @{ Thickness = $t; TemperedType = "FullTempered" },
                        @{ Thickness = $t; TemperedType = "FullTempered" }
                    )
                    InterlayerType = "PVB"
                }
                AirSpaceThickness = 12
            }
            windLoad = $windLoad
        } | ConvertTo-Json -Depth 10
        
        try {
            $response = Invoke-RestMethod -Uri "$baseUrl/api/GlassCalculation/UnifiedCalculateV2" -Method Post -Body $body -ContentType "application/json"
            $status = if ($response.passed) { "✅ PASS" } else { "❌ FAIL" }
            Write-Host "双夹胶中空 ${t}+pvb+${t}+12A+${t}+pvb+${t}: σ=$($response.maxStress)MPa, df=$($response.maxDeflection)mm $status"
        } catch {
            Write-Host "双夹胶中空 ${t}+pvb+${t}+12A+${t}+pvb+${t}: ❌ ERROR - $($_.Exception.Message)" -ForegroundColor Red
        }
    }
    
    # 三玻两腔: 6+12A+6+12A+6
    Write-Host "`n--- 三玻两腔 ---" -ForegroundColor Green
    $thicknesses = @(6, 8, 10, 12)
    foreach ($t in $thicknesses) {
        $body = @{
            shortSide = $shortSide
            longSide = $longSide
            glass = @{
                "#type" = "TripleInsulatingGlass, GlassManagement.Core"
                OuterGlass = @{
                    "#type" = "SingleTemperedGlass, GlassManagement.Core"
                    Thickness = $t
                    TemperedType = "FullTempered"
                }
                MiddleGlass = @{
                    "#type" = "SingleTemperedGlass, GlassManagement.Core"
                    Thickness = $t
                    TemperedType = "FullTempered"
                }
                InnerGlass = @{
                    "#type" = "SingleTemperedGlass, GlassManagement.Core"
                    Thickness = $t
                    TemperedType = "FullTempered"
                }
                OuterAirSpace = 12
                InnerAirSpace = 12
            }
            windLoad = $windLoad
        } | ConvertTo-Json -Depth 10
        
        try {
            $response = Invoke-RestMethod -Uri "$baseUrl/api/GlassCalculation/UnifiedCalculateV2" -Method Post -Body $body -ContentType "application/json"
            $status = if ($response.passed) { "✅ PASS" } else { "❌ FAIL" }
            Write-Host "三玻两腔 ${t}+12A+${t}+12A+${t}: σ=$($response.maxStress)MPa, df=$($response.maxDeflection)mm $status"
        } catch {
            Write-Host "三玻两腔 ${t}+12A+${t}+12A+${t}: ❌ ERROR - $($_.Exception.Message)" -ForegroundColor Red
        }
    }
    
    # 夹胶三玻两腔
    Write-Host "`n--- 夹胶三玻两腔 ---" -ForegroundColor Green
    foreach ($t in $thicknesses) {
        $body = @{
            shortSide = $shortSide
            longSide = $longSide
            glass = @{
                "#type" = "TripleInsulatingGlass, GlassManagement.Core"
                OuterGlass = @{
                    "#type" = "LaminatedGlass, GlassManagement.Core"
                    Sheets = @(
                        @{ Thickness = $t; TemperedType = "FullTempered" },
                        @{ Thickness = $t; TemperedType = "FullTempered" }
                    )
                    InterlayerType = "PVB"
                }
                MiddleGlass = @{
                    "#type" = "SingleTemperedGlass, GlassManagement.Core"
                    Thickness = $t
                    TemperedType = "FullTempered"
                }
                InnerGlass = @{
                    "#type" = "LaminatedGlass, GlassManagement.Core"
                    Sheets = @(
                        @{ Thickness = $t; TemperedType = "FullTempered" },
                        @{ Thickness = $t; TemperedType = "FullTempered" }
                    )
                    InterlayerType = "PVB"
                }
                OuterAirSpace = 12
                InnerAirSpace = 12
            }
            windLoad = $windLoad
        } | ConvertTo-Json -Depth 10
        
        try {
            $response = Invoke-RestMethod -Uri "$baseUrl/api/GlassCalculation/UnifiedCalculateV2" -Method Post -Body $body -ContentType "application/json"
            $status = if ($response.passed) { "✅ PASS" } else { "❌ FAIL" }
            Write-Host "夹胶三玻 ${t}+pvb+${t}+12A+${t}+pvb+${t}: σ=$($response.maxStress)MPa, df=$($response.maxDeflection)mm $status"
        } catch {
            Write-Host "夹胶三玻 ${t}+pvb+${t}+12A+${t}+pvb+${t}: ❌ ERROR - $($_.Exception.Message)" -ForegroundColor Red
        }
    }
}

# 运行测试
Test-SingleGlass
Test-InsulatingGlass
Test-LaminatedGlass
Test-UnifiedCalculate

Write-Host "`n=== 测试完成 ===" -ForegroundColor Cyan
