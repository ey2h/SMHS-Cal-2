$ErrorActionPreference = 'Continue'
try {
    Add-Type -AssemblyName Microsoft.Office.Interop.Word
    Write-Output "ADDTYPE=OK"
    foreach ($en in 'WdPaperSize','WdParagraphAlignment','WdLineSpacing','WdOutlineLevel','WdOrientation','WdStoryType') {
        $t = [Type]::GetType("Microsoft.Office.Interop.Word.$en, Microsoft.Office.Interop.Word")
        if ($t) {
            $pairs = [System.Enum]::GetValues($t) | ForEach-Object { "{0}={1}" -f $_, [int]$_ }
            Write-Output ("{0}: {1}" -f $en, ($pairs -join ', '))
        } else {
            Write-Output "$en : TYPE_NOT_FOUND"
        }
    }
} catch {
    Write-Output ("ADDTYPE=FAIL " + $_.Exception.Message)
}

