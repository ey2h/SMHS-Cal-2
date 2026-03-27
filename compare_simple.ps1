# Compare calculation book sizes
Write-Host "========================================"
Write-Host "  CalcBook Size Comparison"
Write-Host "========================================"
Write-Host ""

$samples = @(
    @{ Name="Glass (sample)"; Path="D:/Code/Glass/计算书样本/全隐框玻璃幕墙计算书.DOC"; Size=0 },
    @{ Name="Aluminum (sample)"; Path="D:/Code/Glass/计算书样本/铝单板幕墙计算书.DOC"; Size=0 },
    @{ Name="Unit (sample)"; Path="D:/Code/Glass/计算书样本/单元幕墙计算书.DOC"; Size=0 }
)

$generated = @(
    @{ Name="Our output S1"; Path="D:/Code/Glass/calcbook_test_output_s1.docx"; Size=0 }
)

Write-Host "### Sample CalcBooks:"
foreach ($s in $samples) {
    if (Test-Path $s.Path) {
        $s.Size = (Get-Item $s.Path).Length
        Write-Host "  $($s.Name): $([math]::Round($s.Size/1KB, 1)) KB"
    } else {
        Write-Host "  $($s.Name): [Not Found]"
    }
}

Write-Host ""
Write-Host "### Our Generated CalcBooks:"
foreach ($g in $generated) {
    if (Test-Path $g.Path) {
        $g.Size = (Get-Item $g.Path).Length
        Write-Host "  $($g.Name): $([math]::Round($g.Size/1KB, 1)) KB"
    } else {
        Write-Host "  $($g.Name): [Not Found]"
    }
}

Write-Host ""
Write-Host "### Gap Analysis:"
Write-Host "  Sample (glass): ~710 KB"
Write-Host "  Our output: ~62 KB"
Write-Host "  Gap: ~11x"
Write-Host ""
Write-Host "  Sample has 51 pages, our output ~5-6 pages"
