$word = New-Object -ComObject Word.Application
$word.Visible = $false
$doc = $word.Documents.Open("D:\Code\Glass\计算书样本\全隐框玻璃幕墙计算书.DOC")
$text = $doc.Content.Text
$lines = $text -split "`n"
$i = 0
foreach ($line in $lines) {
    if ($i -lt 80 -and $line.Trim().Length -gt 2) {
        $trunc = if ($line.Trim().Length -gt 80) { $line.Trim().Substring(0, 80) } else { $line.Trim() }
        Write-Host ($i.ToString() + ": " + $trunc)
    }
    $i++
}
$doc.Close($false)
$word.Quit()
