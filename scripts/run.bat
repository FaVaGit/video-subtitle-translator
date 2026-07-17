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
where nats-server >nul 2>&1 && set "HAS_NATS=1"

where cargo >nul 2>&1
if %errorlevel%==0 (
    cargo --version >nul 2>&1
    if %errorlevel%==0 set "HAS_RUST=1"
)

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

    set /a OPT+=1
    set "OPT_!OPT!=mcp"
    echo     !OPT!. MCP Server     - Video tools + GitHub-authenticated AI session
)

if "%OPT%"=="0" (
    echo   No runnable configuration found.
    echo.
    echo   Install options:
    echo     winget install Microsoft.DotNet.SDK.9
    echo     winget install OpenJS.NodeJS.LTS
    echo     winget install Docker.DockerDesktop
    echo     winget install Rustlang.Rustup
    echo.
    echo   Quick fallback: start-app.bat can auto-select web/frontend fallback.
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

set "HANDLED=0"

if "%MODE%"=="dev" (
    set "HANDLED=1"
    if "%HAS_DOTNET%"=="1" if "%HAS_NODE%"=="1" if "%CAN_NATS%"=="1" (
        call "%~dp0run-dev.bat"
    ) else (
        echo   [WARN] Dev mode requires .NET + Node + NATS/Docker.
        call :ask_install_or_fallback
        if /I "!INSTALL_DECISION!"=="I" (
            call :install_missing_packages
            echo   Relaunch scripts\run.bat to continue with updated environment.
            pause
            exit /b 0
        )
        echo   Falling back to frontend-only.
        call "%~dp0run-frontend-only.bat"
    )
)

if "%MODE%"=="docker" (
    set "HANDLED=1"
    if "%DOCKER_RUNNING%"=="1" (
        call "%~dp0run-docker.bat"
    ) else (
        echo   [WARN] Docker is not running.
        call :ask_install_or_fallback
        if /I "!INSTALL_DECISION!"=="I" (
            call :install_package "Docker.DockerDesktop"
            echo   Relaunch scripts\run.bat to continue with updated environment.
            pause
            exit /b 0
        )
        echo   Falling back to frontend-only.
        call "%~dp0run-frontend-only.bat"
    )
)

if "%MODE%"=="desktop" (
    set "HANDLED=1"
    if "%HAS_DOTNET%"=="1" if "%HAS_NODE%"=="1" if "%HAS_RUST%"=="1" if "%CAN_NATS%"=="1" (
        call "%~dp0run-desktop.bat"
    ) else (
        echo   [WARN] Desktop mode requires .NET + Node + Rust + NATS/Docker.
        call :ask_install_or_fallback
        if /I "!INSTALL_DECISION!"=="I" (
            call :install_missing_packages
            echo   Relaunch scripts\run.bat to continue with updated environment.
            pause
            exit /b 0
        )
        echo   Falling back to frontend-only.
        call "%~dp0run-frontend-only.bat"
    )
)

if "%MODE%"=="desktop-release" (
    set "HANDLED=1"
    if "%HAS_DOTNET%"=="1" if "%HAS_NODE%"=="1" if "%HAS_RUST%"=="1" (
        call "%~dp0run-desktop-release.bat"
    ) else (
        echo   [WARN] Desktop release requires .NET + Node + Rust.
        call :ask_install_or_fallback
        if /I "!INSTALL_DECISION!"=="I" (
            call :install_missing_packages
            echo   Relaunch scripts\run.bat to continue with updated environment.
            pause
            exit /b 0
        )
        echo   Falling back to frontend-only.
        call "%~dp0run-frontend-only.bat"
    )
)

if "%MODE%"=="api-only" (
    set "HANDLED=1"
    if "%HAS_DOTNET%"=="1" (
        call "%~dp0run-api-only.bat"
    ) else (
        echo   [WARN] API mode requires .NET SDK.
        call :ask_install_or_fallback
        if /I "!INSTALL_DECISION!"=="I" (
            call :install_package "Microsoft.DotNet.SDK.9"
            echo   Relaunch scripts\run.bat to continue with updated environment.
            pause
            exit /b 0
        )
        echo   Falling back to frontend-only.
        call "%~dp0run-frontend-only.bat"
    )
)

if "%MODE%"=="frontend-only" (
    set "HANDLED=1"
    if "%HAS_NODE%"=="1" (
        call "%~dp0run-frontend-only.bat"
    ) else (
        echo   [WARN] Frontend mode requires Node.js.
        call :ask_install_or_fallback
        if /I "!INSTALL_DECISION!"=="I" (
            call :install_package "OpenJS.NodeJS.LTS"
            echo   Relaunch scripts\run.bat to continue with updated environment.
            pause
            exit /b 0
        )
        echo   No fallback available without Node.js.
        pause
        exit /b 1
    )
)

if "%MODE%"=="mcp" (
    set "HANDLED=1"
    if "%HAS_NODE%"=="1" (
        call "%~dp0run-mcp.bat"
    ) else (
        echo   [WARN] MCP mode requires Node.js.
        call :ask_install_or_fallback
        if /I "!INSTALL_DECISION!"=="I" (
            call :install_package "OpenJS.NodeJS.LTS"
            echo   Relaunch scripts\run.bat to continue with updated environment.
            pause
            exit /b 0
        )
        echo   No fallback available without Node.js.
        pause
        exit /b 1
    )
)

if "%HANDLED%"=="0" (
    echo   Unknown mode: %MODE%
    echo   Valid modes: dev, docker, desktop, desktop-release, api-only, frontend-only, mcp
    pause
    exit /b 1
)

endlocal

:ask_install_or_fallback
set "INSTALL_DECISION=F"
echo   Choose next action:
echo     I = install missing dependencies now
echo     F = continue with fallback mode
choice /C IF /N /M "   Select [I/F]: "
if errorlevel 2 set "INSTALL_DECISION=F"
if errorlevel 1 set "INSTALL_DECISION=I"
exit /b 0

:install_missing_packages
if "%HAS_DOTNET%"=="0" call :install_package "Microsoft.DotNet.SDK.9"
if "%HAS_NODE%"=="0" call :install_package "OpenJS.NodeJS.LTS"
if "%HAS_RUST%"=="0" call :install_package "Rustlang.Rustup"
if "%CAN_NATS%"=="0" call :install_package "Docker.DockerDesktop"
exit /b 0

:install_package
set "PKG=%~1"
echo.
echo   Missing package: %PKG%
choice /C YN /N /M "   Run winget install %PKG% now? [Y/N]: "
if errorlevel 2 (
    echo   Skipped %PKG%
    exit /b 0
)
echo   Running winget install %PKG% ...
winget install %PKG%
exit /b 0
