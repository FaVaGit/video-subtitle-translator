@echo off
setlocal enabledelayedexpansion
chcp 65001 >nul 2>&1
title Video Subtitle Translator - Frontend Only

set "ROOT=%~dp0.."
set "FRONTEND_DIR=%ROOT%\src\Frontend"
set "FORCE_RESTART=0"
if /I "%~1"=="--restart" set "FORCE_RESTART=1"
cd /d "%FRONTEND_DIR%"

echo.
echo   [FRONTEND] Starting React dev server only...
echo   ═════════════════════════════════════════
echo.

if not exist "node_modules" (
    echo   Installing npm packages...
    npm install --silent >nul 2>&1
)

set "PID5173="
for /f "tokens=5" %%P in ('netstat -ano ^| findstr ":5173 " ^| findstr "LISTENING"') do (
    if not defined PID5173 set "PID5173=%%P"
)

if defined PID5173 (
    tasklist /FI "PID eq !PID5173!" | findstr /I "node.exe" >nul 2>&1
    if !errorlevel!==0 (
        if "!FORCE_RESTART!"=="1" (
            echo   Frontend already running on 5173 ^(PID !PID5173!^). Restart requested...
            taskkill /PID !PID5173! /F >nul 2>&1
            if !errorlevel! neq 0 (
                echo   [WARN] Unable to terminate PID !PID5173!.
                echo   Tip: run scripts\cleanup-ports.bat as Administrator.
                pause
                exit /b 1
            )
            timeout /t 1 /nobreak >nul
        ) else (
            echo   Frontend already running on http://localhost:5173. Reusing existing instance.
            echo   No new dev server will be created.
            echo   Opening browser...
            start "" "http://localhost:5173"
            echo.
            exit /b 0
        )
    ) else (
        echo   [WARN] Port 5173 is in use by another process ^(PID !PID5173!^).
        echo   Close that process or free the port, then retry.
        echo   Tip: run scripts\cleanup-ports.bat as Administrator.
        pause
        exit /b 1
    )
)

echo         Frontend  http://localhost:5173
echo         API proxy http://localhost:5000
echo.
start "" "http://localhost:5173"

npm run dev -- --strictPort

endlocal
