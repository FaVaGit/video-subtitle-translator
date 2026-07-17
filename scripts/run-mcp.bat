@echo off
setlocal
chcp 65001 >nul 2>&1
title Video Subtitle Translator - MCP Server

set "ROOT=%~dp0.."
set "MCP_DIR=%ROOT%\mcp-server"
cd /d "%MCP_DIR%"

echo.
echo   [MCP] Starting Video Subtitle Translator MCP server...
echo.

where node >nul 2>&1
if %errorlevel% neq 0 (
    echo   [ERROR] Node.js not found. Install with:
    echo           winget install OpenJS.NodeJS.LTS
    pause
    exit /b 1
)

if not exist "node_modules" (
    echo   Installing MCP dependencies...
    npm install
    if %errorlevel% neq 0 (
        echo   [ERROR] npm install failed.
        pause
        exit /b 1
    )
)

if "%GITHUB_TOKEN%"=="" (
    for /f %%T in ('gh auth token 2^>nul') do set "GITHUB_TOKEN=%%T"
)

echo   Running MCP server on stdio...
echo   Tools: video_probe, video_extract_frame, video_extract_audio, github_models_chat
echo.

node server.js

endlocal
