@echo off
setlocal enabledelayedexpansion
chcp 65001 >nul 2>&1
title Video Subtitle Translator - Launcher

set "ROOT=%~dp0.."
cd /d "%ROOT%"

echo.
echo   ╔══════════════════════════════════════════════════╗
echo   ║      Video Subtitle Translator - Launcher       ║
echo   ╚══════════════════════════════════════════════════╝
echo.

:: ── Detect available tools ──
set "HAS_DOTNET=0"
set "HAS_NODE=0"
set "HAS_DOCKER=0"
set "HAS_RUST=0"
set "HAS_NATS=0"
set "DOCKER_RUNNING=0"
set "CAN_NATS=0"

where dotnet >nul 2>&1 && set "HAS_DOTNET=1"
where node >nul 2>&1 && set "HAS_NODE=1"
where docker >nul 2>&1 && set "HAS_DOCKER=1"
where cargo >nul 2>&1 && set "HAS_RUST=1"
where nats-server >nul 2>&1 && set "HAS_NATS=1"

if "%HAS_DOCKER%"=="1" (
    docker info >nul 2>&1 && set "DOCKER_RUNNING=1"
)

:: Can we run NATS? (local binary OR Docker running)
if "%HAS_NATS%"=="1" set "CAN_NATS=1"
if "%DOCKER_RUNNING%"=="1" set "CAN_NATS=1"

:: Also check if NATS is already running on port 4222
netstat -an 2>nul | findstr ":4222 " | findstr "LISTENING" >nul 2>&1
if %errorlevel%==0 set "CAN_NATS=1"

echo   Detected:
if "%HAS_DOTNET%"=="1" (echo     [OK] .NET SDK) else (echo     [--] .NET SDK)
if "%HAS_NODE%"=="1" (echo     [OK] Node.js) else (echo     [--] Node.js)
if "%HAS_DOCKER%"=="1" (
    if "%DOCKER_RUNNING%"=="1" (echo     [OK] Docker ^(running^)) else (echo     [~~] Docker ^(not running^))
) else (echo     [--] Docker)
if "%HAS_NATS%"=="1" (echo     [OK] NATS server) else if "%CAN_NATS%"=="1" (echo     [OK] NATS ^(via Docker^)) else (echo     [--] NATS)
if "%HAS_RUST%"=="1" (echo     [OK] Rust/Cargo) else (echo     [~~] Rust ^(optional, for desktop^))
echo.

:: ── If user passed an argument, use that mode ──
if not "%~1"=="" (
    set "MODE=%~1"
    goto :run_mode
)

:: ── Build menu based on what's actually possible ──
echo   Available modes:
echo.

set "OPT=0"

:: Dev mode requires: dotnet + node + NATS (local or Docker or already running)
if "%HAS_DOTNET%"=="1" if "%HAS_NODE%"=="1" if "%CAN_NATS%"=="1" (
    set /a OPT+=1
    set "OPT_!OPT!=dev"
    echo     !OPT!. Development    - API + Worker + Frontend + NATS
)

:: Docker mode requires: Docker running
if "%DOCKER_RUNNING%"=="1" (
    set /a OPT+=1
    set "OPT_!OPT!=docker"
    echo     !OPT!. Docker         - All via docker-compose
)

:: Desktop mode requires: dotnet + node + rust + NATS
if "%HAS_DOTNET%"=="1" if "%HAS_NODE%"=="1" if "%HAS_RUST%"=="1" if "%CAN_NATS%"=="1" (
    set /a OPT+=1
    set "OPT_!OPT!=desktop"
    echo     !OPT!. Desktop        - Tauri app + backend

    set /a OPT+=1
    set "OPT_!OPT!=desktop-release"
    echo     !OPT!. Desktop Release - Build and run packaged desktop app
)

:: API only requires: dotnet
if "%HAS_DOTNET%"=="1" (
    set /a OPT+=1
    set "OPT_!OPT!=api-only"
    echo     !OPT!. API Only       - Backend API on :5000
)

:: Frontend only requires: node
if "%HAS_NODE%"=="1" (
    set /a OPT+=1
    set "OPT_!OPT!=frontend-only"
    echo     !OPT!. Frontend Only  - React dev server on :5173
)

if "%OPT%"=="0" (
    echo   No runnable configuration found.
    echo   Install at least one of: .NET SDK, Node.js, or Docker.
    pause
    exit /b 1
)

echo.
set /p "CHOICE=   Select [1-%OPT%]: "

set "MODE=!OPT_%CHOICE%!"
if "!MODE!"=="" (
    echo   Invalid selection.
    pause
    exit /b 1
)

:run_mode
echo.
echo   Starting: %MODE%
echo   ─────────────────────────────
echo.

if "%MODE%"=="dev" call "%~dp0run-dev.bat"
if "%MODE%"=="docker" call "%~dp0run-docker.bat"
if "%MODE%"=="desktop" call "%~dp0run-desktop.bat"
if "%MODE%"=="desktop-release" call "%~dp0run-desktop-release.bat"
if "%MODE%"=="api-only" call "%~dp0run-api-only.bat"
if "%MODE%"=="frontend-only" call "%~dp0run-frontend-only.bat"

endlocal
