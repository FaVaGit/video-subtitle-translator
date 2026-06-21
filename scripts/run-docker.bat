@echo off
setlocal enabledelayedexpansion
chcp 65001 >nul 2>&1
title Video Subtitle Translator - Docker Mode

set "ROOT=%~dp0.."
cd /d "%ROOT%"

set "GREEN=[92m"
set "YELLOW=[93m"
set "RED=[91m"
set "CYAN=[96m"
set "RESET=[0m"

echo %CYAN%[DOCKER] Starting via Docker Compose...%RESET%
echo.

:: Verify Docker
docker info >nul 2>&1
if %errorlevel% neq 0 (
    echo %RED%Docker is not running. Please start Docker Desktop.%RESET%
    pause
    exit /b 1
)

:: Detect compose command (v2 vs v1)
set "COMPOSE=docker compose"
docker compose version >nul 2>&1
if %errorlevel% neq 0 (
    where docker-compose >nul 2>&1
    if %errorlevel%==0 (
        set "COMPOSE=docker-compose"
    ) else (
        echo %RED%Docker Compose not found.%RESET%
        pause
        exit /b 1
    )
)

echo   Using: %COMPOSE%
echo.

:: Build and start
echo %CYAN%Building images...%RESET%
cd /d "%ROOT%\docker"
%COMPOSE% build

echo.
echo %CYAN%Starting services...%RESET%
echo.
echo   %GREEN%Frontend%RESET%  → http://localhost:3000
echo   %GREEN%API%RESET%       → http://localhost:5000
echo   %GREEN%NATS%RESET%      → nats://localhost:4222
echo   %GREEN%NATS Mon%RESET%  → http://localhost:8222
echo.
echo   %YELLOW%Press Ctrl+C to stop%RESET%
echo.

%COMPOSE% up

endlocal
