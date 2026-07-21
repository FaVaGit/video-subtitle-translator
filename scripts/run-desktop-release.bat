@echo off
setlocal enabledelayedexpansion
chcp 65001 >nul 2>&1
title Video Subtitle Translator - Desktop Release

set "ROOT=%~dp0.."
cd /d "%ROOT%"

echo.
echo   [DESKTOP-RELEASE] Building and launching packaged app...
echo   ═══════════════════════════════════════════════════════════
echo.

if /I "%VST_TEST_MODE%"=="1" (
    echo   [TEST MODE] Desktop release launcher selected.
    exit /b 0
)

where node >nul 2>&1
if %errorlevel% neq 0 (
    echo   [ERROR] Node.js not found. Install Node.js 22+.
    pause
    exit /b 1
)

where dotnet >nul 2>&1
if %errorlevel% neq 0 (
    echo   [ERROR] .NET SDK not found.
    pause
    exit /b 1
)

where cargo >nul 2>&1
if %errorlevel% neq 0 (
    echo   [ERROR] Rust/Cargo not found. Install from https://rustup.rs
    pause
    exit /b 1
)

cargo tauri --version >nul 2>&1
if %errorlevel% neq 0 (
    echo   [ERROR] cargo-tauri not found. Install with: cargo install tauri-cli
    pause
    exit /b 1
)

:: ── NATS ──
echo   [1/4] NATS server...
set "NATS_STARTED=0"
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
echo         [ERROR] No NATS available.
pause
exit /b 1

:nats_ok
echo         [OK]
echo.

:: ── Backend ──
echo   [2/4] Starting backend...
cd /d "%ROOT%\src\Backend"
dotnet build --nologo -q >nul 2>&1
start "VST Worker" /min dotnet run --project VideoSubtitleTranslator.Worker --no-build
start "VST API" /min dotnet run --project VideoSubtitleTranslator.Api --no-build --urls "http://localhost:5000"
echo         [OK]
echo.

:: ── Build desktop ──
echo   [3/4] Building desktop app...
cd /d "%ROOT%\src\Frontend"
if not exist "node_modules" npm install --silent >nul 2>&1
cd /d "%ROOT%\src\Desktop\src-tauri"
cargo tauri build
if %errorlevel% neq 0 (
    echo         [ERROR] Desktop build failed.
    goto :cleanup
)
echo         [OK]
echo.

:: ── Launch packaged binary ──
echo   [4/4] Launching packaged desktop app...
set "APP_EXE=%ROOT%\src\Desktop\src-tauri\target\release\video-subtitle-translator.exe"
if not exist "%APP_EXE%" (
    echo         [ERROR] Executable not found: %APP_EXE%
    goto :cleanup
)
echo         API       http://localhost:5000
echo         Desktop   %APP_EXE%
echo.
"%APP_EXE%"

:cleanup
echo.
echo   Shutting down...
taskkill /fi "WINDOWTITLE eq VST Worker" /f >nul 2>&1
taskkill /fi "WINDOWTITLE eq VST API" /f >nul 2>&1
if "%NATS_STARTED%"=="local" taskkill /fi "WINDOWTITLE eq NATS Server" /f >nul 2>&1
if "%NATS_STARTED%"=="docker" docker stop vst-nats >nul 2>&1
echo   All services stopped.

endlocal
