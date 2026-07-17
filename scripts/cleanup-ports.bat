@echo off
setlocal enabledelayedexpansion
chcp 65001 >nul 2>&1
title Video Subtitle Translator - Cleanup Ports

echo.
echo   [CLEANUP] Releasing stale frontend ports for this application...
echo   ═══════════════════════════════════════════════════════════════
echo.

:: Require admin for reliable task termination
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo   Administrator privileges are required.
    echo   Requesting elevation...
    powershell -NoProfile -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
    exit /b
)

set "KILLED=0"
set "SKIPPED=0"

for %%L in (5173 5174 5175 5176 5177 5178 5179) do (
    set "PID="
    for /f "tokens=5" %%P in ('netstat -ano ^| findstr ":%%L " ^| findstr "LISTENING"') do (
        if not defined PID set "PID=%%P"
    )

    if defined PID (
        set "PNAME="
        for /f "tokens=1" %%N in ('tasklist /FI "PID eq !PID!" /FO CSV /NH') do (
            set "PNAME=%%~N"
        )

        if /I "!PNAME!"=="node.exe" (
            echo   Port %%L -> node.exe PID !PID! : terminating...
            taskkill /PID !PID! /F >nul 2>&1
            if !errorlevel! EQU 0 (
                set /a KILLED+=1
                echo      [OK]
            ) else (
                set /a SKIPPED+=1
                echo      [WARN] Unable to terminate PID !PID!
            )
        ) else (
            set /a SKIPPED+=1
            echo   Port %%L -> PID !PID! ^(!PNAME!^) : skipped ^(not node.exe^)
        )
    )
)

echo.
echo   Cleanup summary:
echo     Killed node processes: !KILLED!
echo     Skipped entries:       !SKIPPED!
echo.
echo   Done.

endlocal
