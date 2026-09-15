@echo off
setlocal
cd /d "%~dp0"

where dotnet >nul 2>nul
if errorlevel 1 (
  echo The .NET 8 SDK is required to build Nova Battery.
  echo Download it from: https://dotnet.microsoft.com/download/dotnet/8.0
  pause
  exit /b 1
)

dotnet publish src\NovaBattery\NovaBattery.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:DebugType=None -p:DebugSymbols=false -o dist
if errorlevel 1 (
  echo.
  echo Build failed.
  pause
  exit /b 1
)

echo.
echo Built successfully: %CD%\dist\NovaBattery.exe
pause

