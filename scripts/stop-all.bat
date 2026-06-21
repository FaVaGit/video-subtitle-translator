@echo off
setlocal
chcp 65001 >nul 2>&1

set "ROOT=%~dp0.."
set "GREEN=[92m"
set "YELLOW=[93m"
set "CYAN=[96m"
set "RESET=[0m"

echo %CYAN%Stopping all Video Subtitle Translator services...%RESET%
echo.

:: Kill .NET processes
taskkill /fi "WINDOWTITLE eq VST Worker" /f >nul 2>&1
taskkill /fi "WINDOWTITLE eq VST API" /f >nul 2>&1
taskkill /fi "WINDOWTITLE eq NATS Server" /f >nul 2>&1

:: Stop Docker containers
docker stop vst-nats >nul 2>&1
docker rm vst-nats >nul 2>&1

:: Stop docker-compose
cd /d "%ROOT%\docker"
docker compose down >nul 2>&1

echo %GREEN%All services stopped.%RESET%

endlocal
