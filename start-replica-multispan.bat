@echo off
REM 启动 SMHS 复刻版并直接打开「多跨数据输入」面板（供手动对照测试）
setlocal
cd /d "%~dp0"
set EXE=src\GlassManagement.Desktop\bin\Debug\net10.0\GlassManagement.Desktop.exe
if not exist "%EXE%" (
  echo [构建中] ...
  .tools\dotnet\dotnet.exe build src\GlassManagement.Desktop\GlassManagement.Desktop.csproj --nologo -v q
  if errorlevel 1 (echo 构建失败 & exit /b 1)
)
start "" "%EXE%" --calc --show=多跨数据输入
endlocal
