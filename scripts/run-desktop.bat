@echo off
setlocal enabledelayedexpansion
chcp 65001 >nul 2>&1
title Video Subtitle Translator - Desktop Mode (Tauri)

set "ROOT=%~dp0.."
cd /d "%ROOT%"

echo.
echo   [DESKTOP] Starting Tauri desktop app...
echo   ═════════════════════════════════════════
echo.

where cargo >nul 2>&1
if %errorlevel% neq 0 (
    echo   [ERROR] Rust/Cargo not found. Install from https://rustup.rs
    pause
    exit /b 1
)

:: ── NATS ──
echo   [1/3] NATS server...
set "NATS_STARTED=0"
netstat -an 2>nul | findstr ":4222 " | findstr "LISTENING" >nul 2>&1
if %errorlevel%==0 (
    echo         Already running
    goto :desk_nats_ok
)
where nats-server >nul 2>&1
if %errorlevel%==0 (
    if not exist "%ROOT%\data\nats" mkdir "%ROOT%\data\nats"
    start "NATS Server" /min nats-server --jetstream --store_dir "%ROOT%\data\nats"
    set "NATS_STARTED=local"
    timeout /t 2 /nobreak >nul
    goto :desk_nats_ok
)
where docker >nul 2>&1
if %errorlevel%==0 (
    docker info >nul 2>&1
    if !errorlevel!==0 (
        docker run -d --name vst-nats -p 4222:4222 -p 8222:8222 nats:2.11-alpine --jetstream >nul 2>&1 || docker start vst-nats >nul 2>&1
        set "NATS_STARTED=docker"
        timeout /t 2 /nobreak >nul
        goto :desk_nats_ok
    )
)
echo         [ERROR] No NATS available.
pause
exit /b 1

:desk_nats_ok
echo         [OK]
echo.

:: ── Backend ──
echo   [2/3] Building and starting backend...
cd /d "%ROOT%\src\Backend"
dotnet build --nologo -q >nul 2>&1
start "VST Worker" /min dotnet run --project VideoSubtitleTranslator.Worker --no-build
start "VST API" /min dotnet run --project VideoSubtitleTranslator.Api --no-build --urls "http://localhost:5000"
echo         [OK]
echo.

:: ── Tauri ──
echo   [3/3] Starting Tauri...
echo.
echo         API       http://localhost:5000
echo         Desktop   Tauri window will open
echo.

cd /d "%ROOT%\src\Desktop"
if not exist "node_modules" npm install --silent >nul 2>&1
npx tauri dev

:: Cleanup
echo.
echo   Shutting down...
taskkill /fi "WINDOWTITLE eq VST Worker" /f >nul 2>&1
taskkill /fi "WINDOWTITLE eq VST API" /f >nul 2>&1
if "%NATS_STARTED%"=="local" taskkill /fi "WINDOWTITLE eq NATS Server" /f >nul 2>&1
if "%NATS_STARTED%"=="docker" docker stop vst-nats >nul 2>&1
echo   All services stopped.

endlocal
