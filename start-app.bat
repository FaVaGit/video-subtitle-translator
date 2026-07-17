@echo off
setlocal
cd /d "%~dp0"

set "APP_EXE=%~dp0src\Desktop\src-tauri\target\release\video-subtitle-translator.exe"
set "HAS_CARGO=0"
set "HAS_NATS=0"
set "DOCKER_RUNNING=0"
set "HAS_DOTNET=0"
set "HAS_NODE=0"

where dotnet >nul 2>&1
if %errorlevel%==0 set "HAS_DOTNET=1"

where node >nul 2>&1
if %errorlevel%==0 set "HAS_NODE=1"

netstat -an 2>nul | findstr ":4222 " | findstr "LISTENING" >nul 2>&1
if %errorlevel%==0 set "HAS_NATS=1"

where nats-server >nul 2>&1
if %errorlevel%==0 set "HAS_NATS=1"

where docker >nul 2>&1
if %errorlevel%==0 (
	docker info >nul 2>&1 && set "DOCKER_RUNNING=1"
)

if "%DOCKER_RUNNING%"=="1" set "HAS_NATS=1"

where cargo >nul 2>&1
if %errorlevel%==0 (
	cargo --version >nul 2>&1
	if %errorlevel%==0 set "HAS_CARGO=1"
)

rem One-click launcher:
rem - If packaged exe exists, run direct mode (no Rust/Node required)
rem - Otherwise try build+run mode when Rust is available
rem - If Rust is missing, fallback to web/dev mode instead of hard fail
if exist "%APP_EXE%" (
	echo Starting packaged desktop app...
	call "%~dp0scripts\run-desktop-direct.bat"
) else (
	if "%HAS_CARGO%"=="1" (
		echo Packaged desktop app not found. Running first-time build...
		call "%~dp0scripts\run-desktop-release.bat"
	) else (
		echo Rust/Cargo not found.
		echo.
		echo Install option for desktop one-click mode:
		echo   winget install Rustlang.Rustup
		echo.
		echo Optional prerequisites if missing:
		echo   winget install Microsoft.DotNet.SDK.9
		echo   winget install OpenJS.NodeJS.LTS
		echo   winget install Docker.DockerDesktop
		echo.
		echo Switching to web/dev alternative...
		if "%HAS_NODE%"=="0" (
			echo.
			echo Node.js is required for web fallback but is not installed.
			echo Install with:
			echo   winget install OpenJS.NodeJS.LTS
			pause
			exit /b 1
		)
		if "%HAS_NATS%"=="1" if "%HAS_DOTNET%"=="1" (
			echo Launching full web/dev mode...
			echo   Frontend: http://localhost:5173
			echo   API:      http://localhost:5000
			echo   Note: if 5173 is busy, Vite will auto-switch port.
			call "%~dp0scripts\run-dev.bat"
		) else (
			echo Launching frontend-only mode.
			if "%HAS_DOTNET%"=="0" echo .NET SDK not found: API/Worker cannot be started.
			if "%HAS_NATS%"=="0" echo NATS/Docker not available: queue runtime cannot be started.
			echo.
			echo Install options to enable full processing:
			echo   winget install Microsoft.DotNet.SDK.9
			echo   winget install Docker.DockerDesktop
			echo.
			echo Processing/upload requires backend queue ^(NATS or Docker^).
			echo Frontend URL: http://localhost:5173 ^(or next free port shown in terminal^)
			call "%~dp0scripts\run-frontend-only.bat"
		)
	)
)

endlocal
