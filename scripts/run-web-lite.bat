@echo off
setlocal enabledelayedexpansion
chcp 65001 >nul 2>&1
title Video Subtitle Translator - Web Fallback

set "ROOT=%~dp0.."
cd /d "%ROOT%"

echo.
echo   [WEB] Starting frontend + degraded API...
echo   ═════════════════════════════════════════
echo   Processing queue is unavailable: upload requests will return a clear message until NATS is started.
echo.

echo   [1/3] Building backend...
cd /d "%ROOT%\src\Backend"
dotnet restore --nologo --verbosity quiet >nul 2>&1
dotnet build --nologo --verbosity quiet >nul 2>&1
if errorlevel 1 (
    echo         [ERROR] Backend build failed:
    dotnet build --nologo
    pause
    exit /b 1
)
echo         [OK]
echo.

echo   [2/3] Starting API on :5000...
set "PID5000="
for /f "tokens=5" %%P in ('netstat -ano ^| findstr ":5000 " ^| findstr "LISTENING"') do (
    if not defined PID5000 set "PID5000=%%P"
)

if defined PID5000 (
    tasklist /FI "PID eq !PID5000!" | findstr /I "dotnet.exe" >nul 2>&1
    if !errorlevel!==0 (
        echo         Reusing existing API instance on http://localhost:5000.
    ) else (
        echo         [WARN] Port 5000 is in use by another process ^(PID !PID5000!^).
        echo         Frontend will still start, but API access may not be available.
    )
) else (
    start "VST API" /min dotnet run --project VideoSubtitleTranslator.Api --no-build --urls "http://localhost:5000"
    echo         [OK]
)
echo.

echo   [3/3] Starting frontend on :5173...
call "%~dp0run-frontend-only.bat"

endlocal