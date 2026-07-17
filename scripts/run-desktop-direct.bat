@echo off
setlocal enabledelayedexpansion
chcp 65001 >nul 2>&1
title Video Subtitle Translator - Desktop Direct

set "ROOT=%~dp0.."
set "APP_EXE=%ROOT%\src\Desktop\src-tauri\target\release\video-subtitle-translator.exe"
cd /d "%ROOT%"

echo.
echo   [DESKTOP-DIRECT] Starting packaged app without build...
echo   ═══════════════════════════════════════════════════════
echo.

if not exist "%APP_EXE%" (
    echo   [ERROR] Packaged executable not found:
    echo           %APP_EXE%
    echo.
    echo   Build once with: scripts\run-desktop-release.bat
    pause
    exit /b 1
)

where dotnet >nul 2>&1
if %errorlevel% neq 0 (
    echo   [ERROR] .NET SDK not found.
    pause
    exit /b 1
)

:: ── NATS ──
echo   [1/3] NATS server...
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
echo   [2/3] Starting backend...
cd /d "%ROOT%\src\Backend"
dotnet build --nologo -q >nul 2>&1
start "VST Worker" /min dotnet run --project VideoSubtitleTranslator.Worker --no-build
start "VST API" /min dotnet run --project VideoSubtitleTranslator.Api --no-build --urls "http://localhost:5000"
echo         [OK]
echo.

:: ── Launch packaged binary ──
echo   [3/3] Launching desktop app...
echo         API       http://localhost:5000
echo         Desktop   %APP_EXE%
echo.
"%APP_EXE%"

:: Cleanup
echo.
echo   Shutting down...
taskkill /fi "WINDOWTITLE eq VST Worker" /f >nul 2>&1
taskkill /fi "WINDOWTITLE eq VST API" /f >nul 2>&1
if "%NATS_STARTED%"=="local" taskkill /fi "WINDOWTITLE eq NATS Server" /f >nul 2>&1
if "%NATS_STARTED%"=="docker" docker stop vst-nats >nul 2>&1
echo   All services stopped.

endlocal
