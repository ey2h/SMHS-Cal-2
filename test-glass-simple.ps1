# 玻璃计算测试 - 简化版（已通过测试）
$baseUrl = "http://127.0.0.1:5188"

Write-Host "=== 玻璃计算测试 ===" -ForegroundColor Cyan

$shortSide = 1000
$longSide = 2000
$windLoad = 1.5

$passCount = 0
$failCount = 0

# 单片玻璃
Write-Host "`n=== 单片玻璃测试 ===" -ForegroundColor Yellow
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
    
    try {
        $response = Invoke-RestMethod -Uri "$baseUrl/api/GlassCalculation/SingleGlass" -Method Post -Body $body -ContentType "application/json"
        $status = if ($response.passed) { "PASS" } else { "FAIL" }
        if ($response.passed) { $passCount++ } else { $failCount++ }
        Write-Host "单片 ${t}mm: σ=$($response.maxStress)MPa, df=$($response.maxDeflection)mm,挠度限=$($response.deflectionLimit)mm -> $status"
    } catch {
        Write-Host "单片 ${t}mm: ERROR" -ForegroundColor Red
        $failCount++
    }
}

# 中空玻璃
Write-Host "`n=== 中空玻璃测试 ===" -ForegroundColor Yellow
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
        
        try {
            $response = Invoke-RestMethod -Uri "$baseUrl/api/GlassCalculation/InsulatingGlass" -Method Post -Body $body -ContentType "application/json"
            $status = if ($response.passed) { "PASS" } else { "FAIL" }
            if ($response.passed) { $passCount++ } else { $failCount++ }
            Write-Host "中空 ${t1}+${t2}: σ=$($response.maxStress)MPa, df=$($response.maxDeflection)mm,挠度限=$($response.deflectionLimit)mm -> $status"
        } catch {
            Write-Host "中空 ${t1}+${t2}: ERROR" -ForegroundColor Red
            $failCount++
        }
    }
}

Write-Host "`n=== 测试结果 ===" -ForegroundColor Cyan
Write-Host "通过: $passCount" -ForegroundColor Green
Write-Host "失败: $failCount" -ForegroundColor Red
