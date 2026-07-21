@echo off
setlocal enabledelayedexpansion
chcp 65001 >nul 2>&1
title Video Subtitle Translator - Desktop Direct

set "ROOT=%~dp0.."
set "APP_EXE=%ROOT%\src\Desktop\src-tauri\target\release\video-subtitle-translator.exe"
cd /d "%ROOT%"

echo.
echo   [DESKTOP-DIRECT] Starting packaged app without build...
echo   ═══════════════════════════════════════════════════════
echo.

if /I "%VST_TEST_MODE%"=="1" (
    echo   [TEST MODE] Packaged desktop launcher selected.
    exit /b 0
)

if not exist "%APP_EXE%" (
    echo   [ERROR] Packaged executable not found:
    echo           %APP_EXE%
    echo.
    echo   Build once with: scripts\run-desktop-release.bat
    pause
    exit /b 1
)

where dotnet >nul 2>&1
if %errorlevel% neq 0 (
    echo   [ERROR] .NET SDK not found.
    pause
    exit /b 1
)

:: ── NATS ──
echo   [1/3] NATS server...
set "NATS_STARTED=0"
set "QUEUE_MODE=available"
netstat -an 2>nul | findstr ":4222 " | findstr "LISTENING" >nul 2>&1
if %errorlevel%==0 (
    echo         Already running
    goto :nats_ok
)
where nats-server >nul 2>&1
if %errorlevel%==0 (
    if not exist "%ROOT%\data\nats" mkdir "%ROOT%\data\nats"
    start "NATS Server" /min nats-server --jetstream --store_dir "%ROOT%\data\nats"
    set "NATS_STARTED=local"
    timeout /t 2 /nobreak >nul
    goto :nats_ok
)
where docker >nul 2>&1
if %errorlevel%==0 (
    docker info >nul 2>&1
    if !errorlevel!==0 (
        docker run -d --name vst-nats -p 4222:4222 -p 8222:8222 nats:2.11-alpine --jetstream >nul 2>&1 || docker start vst-nats >nul 2>&1
        set "NATS_STARTED=docker"
        timeout /t 2 /nobreak >nul
        goto :nats_ok
    )
)
set "QUEUE_MODE=direct"
echo         [WARN] No NATS available. Desktop will start in direct processing mode.
goto :nats_ok

:nats_ok
echo         [OK]
echo.

:: ── Backend ──
echo   [2/3] Starting backend...
cd /d "%ROOT%\src\Backend"
for /f "tokens=5" %%P in ('netstat -ano ^| findstr ":5000 " ^| findstr "LISTENING"') do (
    if not defined API_PORT_PID set "API_PORT_PID=%%P"
)
if defined API_PORT_PID (
    powershell -NoProfile -Command "try { Stop-Process -Id !API_PORT_PID! -Force -ErrorAction Stop; exit 0 } catch { exit 1 }" >nul 2>&1
    timeout /t 1 /nobreak >nul
)
taskkill /FI "WINDOWTITLE eq VST Worker" /T /F >nul 2>&1
taskkill /FI "WINDOWTITLE eq VST API" /T /F >nul 2>&1
timeout /t 1 /nobreak >nul
dotnet build --nologo -q >nul 2>&1
if /I "%QUEUE_MODE%"=="available" (
    start "VST Worker" /min dotnet run --project VideoSubtitleTranslator.Worker --no-build
)
start "VST API" /min dotnet run --project VideoSubtitleTranslator.Api --no-build --urls "http://localhost:5000"
echo         [OK]
echo.

:: ── Launch packaged binary ──
echo   [3/3] Launching desktop app...
echo         API       http://localhost:5000
if /I "%QUEUE_MODE%"=="direct" echo         Queue     unavailable ^(direct API processing fallback^)
echo         Desktop   %APP_EXE%
echo.
powershell -NoProfile -Command "Get-Process video-subtitle-translator -ErrorAction SilentlyContinue | ForEach-Object { try { Stop-Process -Id $_.Id -Force -ErrorAction Stop } catch {} }" >nul 2>&1
timeout /t 1 /nobreak >nul
"%APP_EXE%"

:: Cleanup
echo.
echo   Shutting down...
taskkill /fi "WINDOWTITLE eq VST Worker" /f >nul 2>&1
taskkill /fi "WINDOWTITLE eq VST API" /f >nul 2>&1
if "%NATS_STARTED%"=="local" taskkill /fi "WINDOWTITLE eq NATS Server" /f >nul 2>&1
if "%NATS_STARTED%"=="docker" docker stop vst-nats >nul 2>&1
echo   All services stopped.

endlocal
