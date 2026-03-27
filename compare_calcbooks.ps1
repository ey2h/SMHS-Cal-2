# Compare sample calculation books with our generated ones
# Run with: powershell -File compare_calcbooks.ps1

Write-Host "========================================"
Write-Host "  计算书对比分析"
Write-Host "========================================"
Write-Host ""

# Sample calculation books
$samples = @(
    @{ Name="全隐框玻璃幕墙"; Path="D:\Code\Glass\计算书样本\全隐框玻璃幕墙计算书.DOC"; Size=0 },
    @{ Name="铝单板幕墙"; Path="D:\Code\Glass\计算书样本\铝单板幕墙计算书.DOC"; Size=0 },
    @{ Name="单元幕墙"; Path="D:\Code\Glass\计算书样本\单元幕墙计算书.DOC"; Size=0 },
    @{ Name="GRC板幕墙"; Path="D:\Code\Glass\计算书样本\GRC板幕墙计算书.DOC"; Size=0 },
    @{ Name="UHPC板幕墙"; Path="D:\Code\Glass\计算书样本\UHPC板幕墙计算书.DOC"; Size=0 }
)

# Our generated calculation books
$generated = @(
    @{ Name="测试输出S1"; Path="D:\Code\Glass\calcbook_test_output_s1.docx"; Size=0 }
)

Write-Host "### 样本计算书大小："
foreach ($s in $samples) {
    if (Test-Path $s.Path) {
        $s.Size = (Get-Item $s.Path).Length
        Write-Host "  $($s.Name): $([math]::Round($s.Size/1KB, 1)) KB"
    } else {
        Write-Host "  $($s.Name): [文件不存在]"
    }
}

Write-Host ""
Write-Host "### 我们生成的计算书大小："
foreach ($g in $generated) {
    if (Test-Path $g.Path) {
        $g.Size = (Get-Item $g.Path).Length
        Write-Host "  $($g.Name): $([math]::Round($g.Size/1KB, 1)) KB"
    } else {
        Write-Host "  $($g.Name): [文件不存在]"
    }
}

Write-Host ""
Write-Host "### 大小对比："
$sampleSize = ($samples | Where-Object { $_.Size -gt 0 } | Measure-Object -Property Size -Average).Average
$genSize = ($generated | Where-Object { $_.Size -gt 0 } | Measure-Object -Property Size -Average).Average
Write-Host "  样本平均大小: $([math]::Round($sampleSize/1KB, 1)) KB"
Write-Host "  我们的大小: $([math]::Round($genSize/1KB, 1)) KB"
Write-Host "  差距: $([math]::Round($sampleSize/$genSize, 1))x"

Write-Host ""
Write-Host "### 分析："
Write-Host "  样本计算书(全隐框玻璃幕墙)约710KB，51页"
Write-Host "  我们生成的计算书约62KB"
Write-Host "  说明缺少约10倍的内容"

Write-Host ""
Write-Host "### 样本计算书应包含的章节结构："
Write-Host "  1. 封面"
Write-Host "  2. 计算引用的规范标准"
Write-Host "  3. 基本参数"
Write-Host "  4. 风荷载计算"
Write-Host "  5. 地震作用计算"
Write-Host "  6. 幕墙立柱计算 (含受力简图/强度/挠度/抗剪)"
Write-Host "  7. 幕墙横梁计算 (含受力简图/强度/挠度/抗剪)"
Write-Host "  8. 幕墙玻璃选用与校核 (详细玻璃计算)"
Write-Host "  9. 连接件计算 (螺栓连接)"
Write-Host "  10. 预埋件计算"
Write-Host "  11. 转接件计算"
Write-Host "  12. 焊缝计算"
Write-Host "  13. 结构胶计算"
Write-Host "  14. 压板计算"
Write-Host "  15. 副框计算"
Write-Host "  16. 承托件计算"
Write-Host "  17. 附录"

Write-Host ""
Write-Host "### 当前代码中Render方法检查："
$servicePath = "D:\Code\Glass\src\GlassManagement.Api\Services\CalcBookWordService.cs"
$content = Get-Content $servicePath -Raw

$methods = @(
    "RenderStandardsSection",
    "RenderBasicParametersSection",
    "RenderLoadCalculationSection",
    "RenderColumnSection",
    "RenderBeamSection",
    "RenderGlassSection",
    "RenderConnectionSection",
    "RenderEmbedSection",
    "RenderTransferSection",
    "RenderWeldSection",
    "RenderSiliconeSection",
    "RenderPressPlateSection",
    "RenderSubFrameSection",
    "RenderBearingSection"
)

foreach ($m in $methods) {
    if ($content -match "private void $m") {
        Write-Host "  ✅ $m - 已实现"
    } else {
        Write-Host "  ❌ $m - 未找到"
    }
}

Write-Host ""
Write-Host "### 结论："
Write-Host "  需要检查以下几点："
Write-Host "  1. RenderColumnSection 和 RenderBeamSection 是否正确被调用"
Write-Host "  2. 各章节内容是否足够详细（样本有大量公式推导和表格）"
Write-Host "  3. 是否有必要的受力度数简图和力学模型图"
Write-Host "  4. 计算结论表格是否完整"
