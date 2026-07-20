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
		call :ask_install_or_fallback
		if /I "%INSTALL_DECISION%"=="I" (
			call :install_missing_packages
			echo.
			echo Installation commands completed or skipped.
			echo Relaunch start-app.bat to continue with updated environment.
			pause
			exit /b 0
		)
		echo Switching to web/dev alternative...
		if "%HAS_NODE%"=="0" (
			echo.
			echo Node.js is required for web fallback but is not installed.
			echo Install with:
			echo   winget install OpenJS.NodeJS.LTS
			pause
			exit /b 1
		)
		if "%HAS_NATS%"=="1" (
			if "%HAS_DOTNET%"=="1" (
				echo Launching full web/dev mode...
				echo   Frontend: http://localhost:5173
				echo   API:      http://localhost:5000
				echo   Note: if 5173 is busy, Vite will auto-switch port.
				call "%~dp0scripts\run-dev.bat"
			) else (
				echo Launching frontend-only mode.
				echo .NET SDK not found: API/Worker cannot be started.
				echo.
				echo Install options to enable full processing:
				echo   winget install Microsoft.DotNet.SDK.9
				echo   winget install Docker.DockerDesktop
				echo.
				echo Processing/upload requires backend queue ^(NATS or Docker^).
				echo Frontend URL: http://localhost:5173 ^(or next free port shown in terminal^)
				call "%~dp0scripts\run-frontend-only.bat"
			)
		) else (
			if "%HAS_DOTNET%"=="1" (
				echo Launching web fallback mode.
				echo API will start, but processing/upload remains unavailable until NATS or Docker is available.
				echo.
				echo Install options to enable full processing:
				echo   winget install Docker.DockerDesktop
				echo.
				call :ask_install_or_fallback
				if /I "%INSTALL_DECISION%"=="I" (
					call :install_missing_packages
					echo.
					echo Installation commands completed or skipped.
					echo Relaunch start-app.bat to continue with updated environment.
					pause
					exit /b 0
				)
				call "%~dp0scripts\run-web-lite.bat"
			) else (
				echo Launching frontend-only mode.
				echo .NET SDK not found: API cannot be started.
				echo NATS/Docker not available: queue runtime cannot be started.
				echo.
				echo Install options to enable full processing:
				echo   winget install Microsoft.DotNet.SDK.9
				echo   winget install Docker.DockerDesktop
				echo.
				call :ask_install_or_fallback
				if /I "%INSTALL_DECISION%"=="I" (
					call :install_missing_packages
					echo.
					echo Installation commands completed or skipped.
					echo Relaunch start-app.bat to continue with updated environment.
					pause
					exit /b 0
				)
				echo Frontend URL: http://localhost:5173 ^(or next free port shown in terminal^)
				call "%~dp0scripts\run-frontend-only.bat"
			)
		)
	)
)

endlocal & exit /b 0

:ask_install_or_fallback
set "INSTALL_DECISION=F"
echo Choose next action:
echo   I = install missing dependencies now
echo   F = continue with fallback mode
choice /C IF /N /M "Select [I/F]: "
if errorlevel 2 set "INSTALL_DECISION=F"
if errorlevel 1 set "INSTALL_DECISION=I"
exit /b 0

:install_missing_packages
if "%HAS_DOTNET%"=="0" call :install_package "Microsoft.DotNet.SDK.9"
if "%HAS_NODE%"=="0" call :install_package "OpenJS.NodeJS.LTS"
if "%HAS_CARGO%"=="0" call :install_package "Rustlang.Rustup"
if "%HAS_NATS%"=="0" call :install_package "Docker.DockerDesktop"
exit /b 0

:install_package
set "PKG=%~1"
echo.
echo Missing package: %PKG%
choice /C YN /N /M "Run winget install %PKG% now? [Y/N]: "
if errorlevel 2 (
    echo Skipped %PKG%
    exit /b 0
)
echo Running winget install %PKG% ...
winget install %PKG%
exit /b 0
