@echo off
setlocal enabledelayedexpansion
chcp 65001 >nul 2>&1
title Video Subtitle Translator - Launcher

:: ============================================================
:: Smart launcher - auto-detects environment and available tools
:: ============================================================

set "ROOT=%~dp0.."
cd /d "%ROOT%"

:: Colors
set "GREEN=[92m"
set "YELLOW=[93m"
set "RED=[91m"
set "CYAN=[96m"
set "RESET=[0m"

echo %CYAN%╔══════════════════════════════════════════════════╗%RESET%
echo %CYAN%║      Video Subtitle Translator - Launcher       ║%RESET%
echo %CYAN%╚══════════════════════════════════════════════════╝%RESET%
echo.

:: ── Detect available tools ──
set "HAS_DOTNET=0"
set "HAS_NODE=0"
set "HAS_DOCKER=0"
set "HAS_RUST=0"
set "HAS_NATS=0"

where dotnet >nul 2>&1 && set "HAS_DOTNET=1"
where node >nul 2>&1 && set "HAS_NODE=1"
where docker >nul 2>&1 && set "HAS_DOCKER=1"
where cargo >nul 2>&1 && set "HAS_RUST=1"
where nats-server >nul 2>&1 && set "HAS_NATS=1"

:: Check Docker daemon
set "DOCKER_RUNNING=0"
if "%HAS_DOCKER%"=="1" (
    docker info >nul 2>&1 && set "DOCKER_RUNNING=1"
)

echo %CYAN%Environment Detection:%RESET%
if "%HAS_DOTNET%"=="1" (echo   %GREEN%✓%RESET% .NET SDK found) else (echo   %RED%✗%RESET% .NET SDK not found)
if "%HAS_NODE%"=="1" (echo   %GREEN%✓%RESET% Node.js found) else (echo   %RED%✗%RESET% Node.js not found)
if "%HAS_DOCKER%"=="1" (
    if "%DOCKER_RUNNING%"=="1" (echo   %GREEN%✓%RESET% Docker running) else (echo   %YELLOW%~%RESET% Docker installed but not running)
) else (echo   %RED%✗%RESET% Docker not found)
if "%HAS_RUST%"=="1" (echo   %GREEN%✓%RESET% Rust/Cargo found) else (echo   %YELLOW%~%RESET% Rust not found ^(desktop only^))
if "%HAS_NATS%"=="1" (echo   %GREEN%✓%RESET% NATS server found) else (echo   %YELLOW%~%RESET% NATS not local ^(will use Docker^))
echo.

:: ── If user passed an argument, use that mode ──
if not "%~1"=="" (
    set "MODE=%~1"
    goto :run_mode
)

:: ── Auto-select best mode or show menu ──
echo %CYAN%Select run mode:%RESET%
echo.

set "OPT=0"

if "%HAS_DOTNET%"=="1" if "%HAS_NODE%"=="1" (
    set /a OPT+=1
    set "OPT_!OPT!=dev"
    echo   %GREEN%!OPT!%RESET%  Development    ^(API + Worker + Frontend dev server + NATS^)
)

if "%DOCKER_RUNNING%"=="1" (
    set /a OPT+=1
    set "OPT_!OPT!=docker"
    echo   %GREEN%!OPT!%RESET%  Docker         ^(All services via docker-compose^)
)

if "%HAS_DOTNET%"=="1" if "%HAS_NODE%"=="1" if "%HAS_RUST%"=="1" (
    set /a OPT+=1
    set "OPT_!OPT!=desktop"
    echo   %GREEN%!OPT!%RESET%  Desktop        ^(Tauri desktop app + backend^)
)

if "%HAS_DOTNET%"=="1" (
    set /a OPT+=1
    set "OPT_!OPT!=api-only"
    echo   %GREEN%!OPT!%RESET%  API Only       ^(Backend API only, no frontend^)
)

if "%HAS_NODE%"=="1" (
    set /a OPT+=1
    set "OPT_!OPT!=frontend-only"
    echo   %GREEN%!OPT!%RESET%  Frontend Only  ^(React dev server only^)
)

if "%OPT%"=="0" (
    echo %RED%No supported tools found. Install .NET SDK, Node.js, or Docker.%RESET%
    pause
    exit /b 1
)

echo.
set /p "CHOICE=Select [1-%OPT%]: "

set "MODE=!OPT_%CHOICE%!"
if "!MODE!"=="" (
    echo %RED%Invalid selection.%RESET%
    pause
    exit /b 1
)

:run_mode
echo.
echo %CYAN%Starting in %MODE% mode...%RESET%
echo.

if "%MODE%"=="dev" call "%~dp0run-dev.bat"
if "%MODE%"=="docker" call "%~dp0run-docker.bat"
if "%MODE%"=="desktop" call "%~dp0run-desktop.bat"
if "%MODE%"=="api-only" call "%~dp0run-api-only.bat"
if "%MODE%"=="frontend-only" call "%~dp0run-frontend-only.bat"

endlocal
