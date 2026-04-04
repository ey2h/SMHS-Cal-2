# Terminates common browser processes started by Playwright tests
Get-Process chrome, msedge -ErrorAction SilentlyContinue | Stop-Process -Force
