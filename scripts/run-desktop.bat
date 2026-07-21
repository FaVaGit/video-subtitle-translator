@echo off
setlocal enabledelayedexpansion
chcp 65001 >nul 2>&1
title Video Subtitle Translator - Desktop Mode (Tauri)

set "ROOT=%~dp0.."
cd /d "%ROOT%"
set "NATS_BIN="

if exist "%USERPROFILE%\.cargo\bin" (
    set "PATH=%USERPROFILE%\.cargo\bin;%PATH%"
)

echo.
echo   [DESKTOP] Starting Tauri desktop app...
echo   ═════════════════════════════════════════
echo.

if /I "%VST_TEST_MODE%"=="1" (
    echo   [TEST MODE] Desktop source launcher selected.
    exit /b 0
)

where node >nul 2>&1
if %errorlevel% neq 0 (
    echo   [ERROR] Node.js not found. Install Node.js 22+.
    pause
    exit /b 1
)

where cargo >nul 2>&1
if %errorlevel% neq 0 (
    echo   [ERROR] Rust/Cargo not found. Install from https://rustup.rs
    pause
    exit /b 1
)

cargo tauri --version >nul 2>&1
if %errorlevel% neq 0 (
    echo   [ERROR] cargo-tauri not found. Install with: cargo install tauri-cli
    pause
    exit /b 1
)

:: ── NATS ──
echo   [1/3] NATS server...
set "NATS_STARTED=0"
set "QUEUE_MODE=available"
netstat -an 2>nul | findstr ":4222 " | findstr "LISTENING" >nul 2>&1
if %errorlevel%==0 (
    echo         Already running
    goto :desk_nats_ok
)
for /f "usebackq delims=" %%P in (`powershell -NoProfile -Command "$cmd = Get-Command nats-server -ErrorAction SilentlyContinue; if ($cmd) { $cmd.Source }"`) do (
    if not defined NATS_BIN set "NATS_BIN=%%P"
)
if not defined NATS_BIN if exist "%ProgramFiles%\NATS\nats-server\nats-server.exe" set "NATS_BIN=%ProgramFiles%\NATS\nats-server\nats-server.exe"
if not defined NATS_BIN if exist "%ProgramFiles(x86)%\NATS\nats-server\nats-server.exe" set "NATS_BIN=%ProgramFiles(x86)%\NATS\nats-server\nats-server.exe"

if defined NATS_BIN (
    if not exist "%ROOT%\data\nats" mkdir "%ROOT%\data\nats"
    start "NATS Server" /min "cmd" /c "\"%NATS_BIN%\" --jetstream --store_dir \"%ROOT%\data\nats\""
    set "NATS_STARTED=local"
    timeout /t 2 /nobreak >nul
    goto :desk_nats_ok
)
where docker >nul 2>&1
if %errorlevel%==0 (
    docker info >nul 2>&1
    if !errorlevel!==0 (
        docker run -d --name vst-nats -p 4222:4222 -p 8222:8222 nats:2.11-alpine --jetstream >nul 2>&1 || docker start vst-nats >nul 2>&1
        set "NATS_STARTED=docker"
        timeout /t 2 /nobreak >nul
        goto :desk_nats_ok
    )
)
set "QUEUE_MODE=direct"
echo         [WARN] No NATS available. Desktop will start in direct processing mode.
goto :desk_nats_ok

:desk_nats_ok
echo         [OK]
echo.

:: ── Backend ──
echo   [2/3] Building and starting backend...
cd /d "%ROOT%\src\Backend"

for /f "tokens=5" %%P in ('netstat -ano ^| findstr ":5000 " ^| findstr "LISTENING"') do (
    if not defined API_PORT_PID set "API_PORT_PID=%%P"
)
if defined API_PORT_PID (
    powershell -NoProfile -Command "try { Stop-Process -Id !API_PORT_PID! -Force -ErrorAction Stop; exit 0 } catch { exit 1 }" >nul 2>&1
    timeout /t 1 /nobreak >nul
)

:: Stop any previous VST API / VST Worker instances by their console window title
:: (dotnet run inherits the title given by `start "Title" ...`), which is more
:: reliable than matching on process command line.
taskkill /FI "WINDOWTITLE eq VST API" /T /F >nul 2>&1
taskkill /FI "WINDOWTITLE eq VST Worker" /T /F >nul 2>&1
timeout /t 1 /nobreak >nul

dotnet build --nologo -q >nul 2>&1
if errorlevel 1 (
    echo         [WARN] First backend build attempt failed. Retrying after cleanup...
    for /f "tokens=5" %%P in ('netstat -ano ^| findstr ":5000 " ^| findstr "LISTENING"') do (
        if not defined API_PORT_PID_RETRY set "API_PORT_PID_RETRY=%%P"
    )
    if defined API_PORT_PID_RETRY (
        powershell -NoProfile -Command "try { Stop-Process -Id !API_PORT_PID_RETRY! -Force -ErrorAction Stop; exit 0 } catch { exit 1 }" >nul 2>&1
        timeout /t 1 /nobreak >nul
    )
    dotnet build --nologo -q >nul 2>&1
    if errorlevel 1 (
        echo         [WARN] Retry build still failing. Running verbose build once...
        dotnet build --nologo
        if errorlevel 1 (
            echo         [ERROR] Backend build failed.
            pause
            exit /b 1
        )
    )
)

if /I "%QUEUE_MODE%"=="available" (
    start "VST Worker" /min dotnet run --project VideoSubtitleTranslator.Worker --no-build
)
start "VST API" /min dotnet run --project VideoSubtitleTranslator.Api --no-build --urls "http://localhost:5000"
echo         [OK]
echo.

:: ── Tauri ──
echo   [3/3] Starting Tauri...
echo.
echo         API       http://localhost:5000
if /I "%QUEUE_MODE%"=="direct" echo         Queue     unavailable ^(direct API processing fallback^)
echo         Desktop   Tauri window will open
echo.

for /f "tokens=5" %%P in ('netstat -ano ^| findstr ":5173 " ^| findstr "LISTENING"') do (
    if not defined FRONTEND_PORT_PID set "FRONTEND_PORT_PID=%%P"
)
if defined FRONTEND_PORT_PID (
    powershell -NoProfile -Command "try { Stop-Process -Id !FRONTEND_PORT_PID! -Force -ErrorAction Stop; exit 0 } catch { exit 1 }" >nul 2>&1
    timeout /t 1 /nobreak >nul
)

powershell -NoProfile -Command "Get-Process video-subtitle-translator -ErrorAction SilentlyContinue ^| ForEach-Object { try { Stop-Process -Id $_.Id -Force -ErrorAction Stop } catch {} }" >nul 2>&1

cd /d "%ROOT%\src\Frontend"
if not exist "node_modules" npm install --silent >nul 2>&1

cd /d "%ROOT%\src\Desktop\src-tauri"
cargo tauri dev

:: Cleanup
echo.
echo   Shutting down...
powershell -NoProfile -Command "Get-Process dotnet -ErrorAction SilentlyContinue ^| Where-Object { $_.MainWindowTitle -in @('VST Worker','VST API') } ^| ForEach-Object { try { Stop-Process -Id $_.Id -Force -ErrorAction Stop } catch {} }" >nul 2>&1
if "%NATS_STARTED%"=="local" powershell -NoProfile -Command "Get-Process -ErrorAction SilentlyContinue ^| Where-Object { $_.MainWindowTitle -eq 'NATS Server' } ^| ForEach-Object { try { Stop-Process -Id $_.Id -Force -ErrorAction Stop } catch {} }" >nul 2>&1
if "%NATS_STARTED%"=="docker" docker stop vst-nats >nul 2>&1
echo   All services stopped.

endlocal
