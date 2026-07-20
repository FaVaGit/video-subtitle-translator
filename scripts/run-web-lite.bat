@echo off
setlocal enabledelayedexpansion
chcp 65001 >nul 2>&1
title Video Subtitle Translator - Web Fallback

set "ROOT=%~dp0.."
cd /d "%ROOT%"
set "STARTED_API=0"
set "API_PID="
set "FRONTEND_WAS_RUNNING=0"
set "PID5000="
set "API_DLL=%ROOT%\src\Backend\VideoSubtitleTranslator.Api\bin\Debug\net8.0\VideoSubtitleTranslator.Api.dll"

echo.
echo   [WEB] Starting frontend + degraded API...
echo   ═════════════════════════════════════════
echo   Processing queue is unavailable: upload requests will return a clear message until NATS is started.
echo.

echo   [1/3] Building backend...
if exist "%API_DLL%" (
    echo         Reusing existing backend build output.
) else (
    cd /d "%ROOT%\src\Backend"
    dotnet restore --nologo --verbosity quiet >nul 2>&1
    dotnet build --nologo --verbosity quiet >nul 2>&1
    if errorlevel 1 (
        echo         [ERROR] Backend build failed:
        dotnet build --nologo
        pause
        exit /b 1
    )
)
echo         [OK]
echo.

echo   [2/3] Starting API on :5000...
set "API_READY=0"
for /f "tokens=5" %%P in ('netstat -ano ^| findstr ":5000 " ^| findstr "LISTENING"') do (
    if not defined PID5000 set "PID5000=%%P"
)

if defined PID5000 (
    powershell -NoProfile -Command "try { $null = Invoke-WebRequest -Uri 'http://localhost:5000/swagger' -UseBasicParsing -TimeoutSec 3; exit 0 } catch { exit 1 }" >nul 2>&1
    if !errorlevel!==0 set "API_READY=1"
    if "!API_READY!"=="1" (
        echo         Reusing existing API instance on http://localhost:5000.
    ) else (
        echo         [WARN] Port 5000 is in use by another process ^(PID !PID5000!^).
        echo         Frontend will still start, but API access may not be available.
    )
) else (
    for /f %%P in ('powershell -NoProfile -Command "$process = Start-Process -FilePath 'cmd.exe' -ArgumentList '/c title VST API ^&^& dotnet run --project VideoSubtitleTranslator.Api --no-build --urls \"http://localhost:5000\"' -WorkingDirectory '%ROOT%\src\Backend' -WindowStyle Minimized -PassThru; $process.Id"') do (
        set "API_PID=%%P"
    )
    set "STARTED_API=1"
    echo         [OK]
)
echo.

echo   [3/3] Starting frontend on :5173...
set "PID5173="
for /f "tokens=5" %%P in ('netstat -ano ^| findstr ":5173 " ^| findstr "LISTENING"') do (
    if not defined PID5173 set "PID5173=%%P"
)

if defined PID5173 (
    tasklist /FI "PID eq !PID5173!" | findstr /I "node.exe" >nul 2>&1
    if !errorlevel!==0 set "FRONTEND_WAS_RUNNING=1"
)

call "%~dp0run-frontend-only.bat"

if "%STARTED_API%"=="1" if "%FRONTEND_WAS_RUNNING%"=="0" if defined API_PID (
    echo.
    echo   Shutting down degraded API...
    taskkill /PID %API_PID% /T /F >nul 2>&1
)

endlocal