@echo off
setlocal enabledelayedexpansion
chcp 65001 >nul 2>&1
title Video Subtitle Translator - Desktop Mode (Tauri)

set "ROOT=%~dp0.."
cd /d "%ROOT%"

set "GREEN=[92m"
set "YELLOW=[93m"
set "RED=[91m"
set "CYAN=[96m"
set "RESET=[0m"

echo %CYAN%[DESKTOP] Starting Tauri desktop app...%RESET%
echo.

:: Verify prerequisites
where cargo >nul 2>&1
if %errorlevel% neq 0 (
    echo %RED%Rust/Cargo not found. Install from https://rustup.rs%RESET%
    pause
    exit /b 1
)

where dotnet >nul 2>&1
if %errorlevel% neq 0 (
    echo %RED%.NET SDK not found.%RESET%
    pause
    exit /b 1
)

:: ── NATS (same logic as dev) ──
echo %CYAN%[1/3] Checking NATS...%RESET%
set "NATS_STARTED=0"
netstat -an | findstr ":4222" >nul 2>&1
if %errorlevel% neq 0 (
    where nats-server >nul 2>&1
    if !errorlevel!==0 (
        start "NATS Server" /min nats-server --jetstream --store_dir "%ROOT%\data\nats"
        set "NATS_STARTED=1"
        timeout /t 2 /nobreak >nul
    ) else (
        docker info >nul 2>&1
        if !errorlevel!==0 (
            docker run -d --name vst-nats -p 4222:4222 -p 8222:8222 nats:2.11-alpine --jetstream >nul 2>&1 || docker start vst-nats >nul 2>&1
            set "NATS_STARTED=1"
            timeout /t 2 /nobreak >nul
        ) else (
            echo %RED%  No NATS available. Install nats-server or start Docker.%RESET%
            pause
            exit /b 1
        )
    )
)
echo   %GREEN%✓ NATS ready%RESET%
echo.

:: ── Backend services ──
echo %CYAN%[2/3] Starting backend services...%RESET%
cd /d "%ROOT%\src\Backend"
dotnet build --nologo -q >nul 2>&1

start "VST Worker" /min dotnet run --project VideoSubtitleTranslator.Worker --no-build
start "VST API" /min dotnet run --project VideoSubtitleTranslator.Api --no-build --urls "http://localhost:5000"

echo   %GREEN%✓ API + Worker started%RESET%
echo.

:: ── Tauri desktop ──
echo %CYAN%[3/3] Starting Tauri dev mode...%RESET%
echo.
echo   %GREEN%API%RESET%       → http://localhost:5000
echo   %GREEN%Desktop%RESET%   → Tauri window will open
echo.

cd /d "%ROOT%\src\Desktop"
if not exist "node_modules" (
    npm install --silent >nul 2>&1
)
npx tauri dev

:: Cleanup
echo.
echo %YELLOW%Shutting down...%RESET%
taskkill /fi "WINDOWTITLE eq VST Worker" /f >nul 2>&1
taskkill /fi "WINDOWTITLE eq VST API" /f >nul 2>&1
if "%NATS_STARTED%"=="1" (
    taskkill /im nats-server.exe /f >nul 2>&1
    docker stop vst-nats >nul 2>&1
)
echo %GREEN%All services stopped.%RESET%

endlocal
