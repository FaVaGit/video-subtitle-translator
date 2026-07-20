@echo off
setlocal enabledelayedexpansion
chcp 65001 >nul 2>&1
title Video Subtitle Translator - Development Mode

set "ROOT=%~dp0.."
cd /d "%ROOT%"
set "WORKER_PID="
set "API_PID="

echo.
echo   [DEV] Starting development environment...
echo   ═════════════════════════════════════════
echo.

:: ── Step 1: Ensure NATS is running ──
echo   [1/4] NATS server...

set "NATS_STARTED=0"

:: Check if already running on port 4222
netstat -an 2>nul | findstr ":4222 " | findstr "LISTENING" >nul 2>&1
if %errorlevel%==0 (
    echo         Already running on :4222
    goto :nats_ok
)

:: Try local nats-server binary
where nats-server >nul 2>&1
if %errorlevel%==0 (
    echo         Starting local nats-server...
    if not exist "%ROOT%\data\nats" mkdir "%ROOT%\data\nats"
    start "NATS Server" /min nats-server --jetstream --store_dir "%ROOT%\data\nats"
    set "NATS_STARTED=local"
    timeout /t 2 /nobreak >nul
    goto :nats_ok
)

:: Try Docker
where docker >nul 2>&1
if %errorlevel%==0 (
    docker info >nul 2>&1
    if !errorlevel!==0 (
        :: Check if container exists but stopped
        docker ps -a --filter "name=vst-nats" --format "{{.Names}}" 2>nul | findstr "vst-nats" >nul
        if !errorlevel!==0 (
            echo         Starting existing Docker container...
            docker start vst-nats >nul 2>&1
        ) else (
            echo         Creating NATS Docker container...
            docker run -d --name vst-nats -p 4222:4222 -p 8222:8222 nats:2.11-alpine --jetstream >nul 2>&1
        )
        set "NATS_STARTED=docker"
        timeout /t 2 /nobreak >nul
        goto :nats_ok
    )
)

echo         [ERROR] No NATS available. Install nats-server or Docker.
pause
exit /b 1

:nats_ok
echo         [OK]
echo.

:: ── Step 2: Build backend ──
echo   [2/4] Building backend...
cd /d "%ROOT%\src\Backend"
dotnet restore --nologo -q >nul 2>&1
dotnet build --nologo -q >nul 2>&1
if %errorlevel% neq 0 (
    echo         [ERROR] Build failed:
    dotnet build --nologo
    pause
    exit /b 1
)
echo         [OK]
echo.

:: ── Step 3: Frontend deps ──
echo   [3/4] Preparing frontend...
cd /d "%ROOT%\src\Frontend"
if not exist "node_modules" (
    echo         Installing npm packages...
    npm install --silent >nul 2>&1
)
echo         [OK]
echo.

:: ── Step 4: Launch all services ──
echo   [4/4] Launching services...
echo.
echo         API       http://localhost:5000
echo         Swagger   http://localhost:5000/swagger
echo         Frontend  http://localhost:5173
echo         NATS      nats://localhost:4222
echo.
echo   ─────────────────────────────────────────
echo   Ctrl+C in this window stops everything.
echo   ─────────────────────────────────────────
echo.

set "FRONTEND_DIR=%ROOT%\src\Frontend"
set "FRONTEND_REUSED=0"
set "PID5173="
for /f "tokens=5" %%P in ('netstat -ano ^| findstr ":5173 " ^| findstr "LISTENING"') do (
    if not defined PID5173 set "PID5173=%%P"
)

if defined PID5173 (
    tasklist /FI "PID eq !PID5173!" | findstr /I "node.exe" >nul 2>&1
    if !errorlevel!==0 (
        set "FRONTEND_REUSED=1"
        echo   Reusing existing frontend instance on http://localhost:5173.
    ) else (
        echo   [WARN] Port 5173 is in use by another process ^(PID !PID5173!^).
        echo   Frontend cannot start in strict mode until the port is free.
        echo   Tip: run scripts\cleanup-ports.bat as Administrator.
    )
)

:: Start Worker (background window)
cd /d "%ROOT%\src\Backend"
for /f %%P in ('powershell -NoProfile -Command "$process = Start-Process -FilePath 'dotnet' -ArgumentList 'run --project VideoSubtitleTranslator.Worker --no-build' -WorkingDirectory '%ROOT%\src\Backend' -WindowStyle Hidden -PassThru; $process.Id"') do (
    set "WORKER_PID=%%P"
)

:: Start API (background window)
for /f %%P in ('powershell -NoProfile -Command "$process = Start-Process -FilePath 'dotnet' -ArgumentList 'run --project VideoSubtitleTranslator.Api --no-build --urls http://localhost:5000' -WorkingDirectory '%ROOT%\src\Backend' -WindowStyle Hidden -PassThru; $process.Id"') do (
    set "API_PID=%%P"
)

if "!FRONTEND_REUSED!"=="1" (
    echo.
    echo   Frontend already running. Reusing existing server.
    echo   Press Ctrl+C to stop backend services started by this script.
    timeout /t -1 >nul
) else (
    :: Start Frontend (foreground)
    cd /d "%ROOT%\src\Frontend"
    npm run dev -- --strictPort
)

:: ── Cleanup ──
echo.
echo   Shutting down...
if defined WORKER_PID taskkill /PID !WORKER_PID! /T /F >nul 2>&1
if defined API_PID taskkill /PID !API_PID! /T /F >nul 2>&1
if "%NATS_STARTED%"=="local" (
    taskkill /fi "WINDOWTITLE eq NATS Server" /f >nul 2>&1
)
if "%NATS_STARTED%"=="docker" (
    docker stop vst-nats >nul 2>&1
)
echo   All services stopped.

endlocal
