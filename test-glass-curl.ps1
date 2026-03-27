# 玻璃计算测试 - curl版本
$baseUrl = "http://127.0.0.1:5188"

Write-Host "=== 玻璃计算测试 ===" -ForegroundColor Cyan

$shortSide = 1000
$longSide = 2000
$windLoad = 1.5

$passCount = 0
$failCount = 0

# 单片玻璃
Write-Host "`n=== 单片玻璃测试 (6mm-12mm) ===" -ForegroundColor Yellow
$thicknesses = @(6, 8, 10, 12)
foreach ($t in $thicknesses) {
    $json = '{"shortSide":' + $shortSide + ',"longSide":' + $longSide + ',"thickness":' + $t + ',"glassType":"Tempered","windLoad":' + $windLoad + ',"supportCondition":"FourSides"}'
    
    try {
        $response = curl -s "$baseUrl/api/GlassCalculation/SingleGlass" -X POST -H "Content-Type: application/json" -d $json | ConvertFrom-Json
        $status = if ($response.passed) { "PASS" } else { "FAIL" }
        if ($response.passed) { $passCount++ } else { $failCount++ }
        Write-Host "单片 ${t}mm: σ=$($response.maxStress)MPa, df=$($response.maxDeflection)mm -> $status"
    } catch {
        Write-Host "单片 ${t}mm: ERROR" -ForegroundColor Red
        $failCount++
    }
}

# 中空玻璃
Write-Host "`n=== 中空玻璃测试 (6+6到12+12) ===" -ForegroundColor Yellow
$thicknesses = @(6, 8, 10, 12)
foreach ($t1 in $thicknesses) {
    foreach ($t2 in $thicknesses) {
        $json = '{"shortSide":' + $shortSide + ',"longSide":' + $longSide + ',"outerThickness":' + $t1 + ',"innerThickness":' + $t2 + ',"outerGlassType":"Tempered","innerGlassType":"Tempered","windLoad":' + $windLoad + ',"altitudeDifference":0,"temperatureDifference":0}'
        
        try {
            $response = curl -s "$baseUrl/api/GlassCalculation/InsulatingGlass" -X POST -H "Content-Type: application/json" -d $json | ConvertFrom-Json
            $status = if ($response.passed) { "PASS" } else { "FAIL" }
            if ($response.passed) { $passCount++ } else { $failCount++ }
            Write-Host "中空 ${t1}+${t2}: σ=$($response.maxStress)MPa, df=$($response.maxDeflection)mm -> $status"
        } catch {
            Write-Host "中空 ${t1}+${t2}: ERROR" -ForegroundColor Red
            $failCount++
        }
    }
}

Write-Host "`n=== 测试结果 ===" -ForegroundColor Cyan
Write-Host "通过: $passCount" -ForegroundColor Green
Write-Host "失败: $failCount" -ForegroundColor Red
