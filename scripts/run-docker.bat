@echo off
setlocal
chcp 65001 >nul 2>&1
title Video Subtitle Translator - Docker Mode

set "ROOT=%~dp0.."
cd /d "%ROOT%"

echo.
echo   [DOCKER] Starting via Docker Compose...
echo   ═════════════════════════════════════════
echo.

docker info >nul 2>&1
if %errorlevel% neq 0 (
    echo   [ERROR] Docker is not running. Start Docker Desktop.
    pause
    exit /b 1
)

set "COMPOSE=docker compose"
docker compose version >nul 2>&1
if %errorlevel% neq 0 (
    where docker-compose >nul 2>&1
    if %errorlevel%==0 (
        set "COMPOSE=docker-compose"
    ) else (
        echo   [ERROR] Docker Compose not found.
        pause
        exit /b 1
    )
)

echo   Building images...
cd /d "%ROOT%\docker"
%COMPOSE% build

echo.
echo   Starting services...
echo.
echo         Frontend  http://localhost:3000
echo         API       http://localhost:5000
echo         NATS      nats://localhost:4222
echo.
echo   Ctrl+C to stop.
echo.

%COMPOSE% up

endlocal
