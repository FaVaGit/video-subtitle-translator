@echo off
setlocal enabledelayedexpansion
chcp 65001 >nul 2>&1
title Video Subtitle Translator - Development Mode

set "ROOT=%~dp0.."
cd /d "%ROOT%"

set "GREEN=[92m"
set "YELLOW=[93m"
set "RED=[91m"
set "CYAN=[96m"
set "RESET=[0m"

echo %CYAN%[DEV] Starting development environment...%RESET%
echo.

:: ── Step 1: Ensure NATS is running ──
echo %CYAN%[1/4] Checking NATS server...%RESET%

:: Try local nats-server first
set "NATS_STARTED=0"
where nats-server >nul 2>&1
if %errorlevel%==0 (
    :: Check if already running
    netstat -an | findstr ":4222" >nul 2>&1
    if !errorlevel! neq 0 (
        echo   Starting local NATS with JetStream...
        start "NATS Server" /min nats-server --jetstream --store_dir "%ROOT%\data\nats"
        set "NATS_STARTED=1"
        timeout /t 2 /nobreak >nul
    ) else (
        echo   %GREEN%NATS already running on :4222%RESET%
    )
) else (
    :: Fall back to Docker
    where docker >nul 2>&1
    if %errorlevel%==0 (
        docker info >nul 2>&1
        if !errorlevel!==0 (
            docker ps --filter "name=vst-nats" --format "{{.Names}}" 2>nul | findstr "vst-nats" >nul
            if !errorlevel! neq 0 (
                echo   Starting NATS via Docker...
                docker run -d --name vst-nats -p 4222:4222 -p 8222:8222 nats:2.11-alpine --jetstream >nul 2>&1
                if !errorlevel! neq 0 (
                    :: Container might exist but be stopped
                    docker start vst-nats >nul 2>&1
                )
                set "NATS_STARTED=1"
                timeout /t 2 /nobreak >nul
            ) else (
                echo   %GREEN%NATS container already running%RESET%
            )
        ) else (
            echo %RED%  Docker not running. Start Docker or install nats-server.%RESET%
            pause
            exit /b 1
        )
    ) else (
        echo %RED%  No NATS server or Docker found. Install one of them.%RESET%
        pause
        exit /b 1
    )
)
echo   %GREEN%✓ NATS ready%RESET%
echo.

:: ── Step 2: Restore & build backend ──
echo %CYAN%[2/4] Building backend...%RESET%
cd /d "%ROOT%\src\Backend"
dotnet restore --nologo -q >nul 2>&1
dotnet build --nologo -q >nul 2>&1
if %errorlevel% neq 0 (
    echo %RED%  Backend build failed!%RESET%
    dotnet build --nologo
    pause
    exit /b 1
)
echo   %GREEN%✓ Backend built%RESET%
echo.

:: ── Step 3: Install frontend deps if needed ──
echo %CYAN%[3/4] Preparing frontend...%RESET%
cd /d "%ROOT%\src\Frontend"
if not exist "node_modules" (
    echo   Installing npm packages...
    npm install --silent >nul 2>&1
)
echo   %GREEN%✓ Frontend ready%RESET%
echo.

:: ── Step 4: Start all services ──
echo %CYAN%[4/4] Starting services...%RESET%
echo.
echo   %GREEN%API%RESET%       → http://localhost:5000
echo   %GREEN%Swagger%RESET%   → http://localhost:5000/swagger
echo   %GREEN%Frontend%RESET%  → http://localhost:5173
echo   %GREEN%NATS%RESET%      → nats://localhost:4222
echo   %GREEN%NATS Mon%RESET%  → http://localhost:8222
echo.
echo   %YELLOW%Press Ctrl+C in any window to stop%RESET%
echo.

:: Start Worker in background
cd /d "%ROOT%\src\Backend"
start "VST Worker" /min dotnet run --project VideoSubtitleTranslator.Worker --no-build

:: Start API in background
start "VST API" /min dotnet run --project VideoSubtitleTranslator.Api --no-build --urls "http://localhost:5000"

:: Start Frontend (foreground - Ctrl+C stops it)
cd /d "%ROOT%\src\Frontend"
echo %CYAN%Starting frontend dev server (Ctrl+C to stop all)...%RESET%
npm run dev

:: Cleanup when frontend stops
echo.
echo %YELLOW%Shutting down...%RESET%
taskkill /fi "WINDOWTITLE eq VST Worker" /f >nul 2>&1
taskkill /fi "WINDOWTITLE eq VST API" /f >nul 2>&1
if "%NATS_STARTED%"=="1" (
    where nats-server >nul 2>&1 && taskkill /im nats-server.exe /f >nul 2>&1
    docker stop vst-nats >nul 2>&1
)
echo %GREEN%All services stopped.%RESET%

endlocal
