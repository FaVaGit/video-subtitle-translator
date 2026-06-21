@echo off
setlocal
chcp 65001 >nul 2>&1
title Video Subtitle Translator - Frontend Only

set "ROOT=%~dp0.."
cd /d "%ROOT%\src\Frontend"

set "GREEN=[92m"
set "CYAN=[96m"
set "RESET=[0m"

echo %CYAN%[FRONTEND] Starting React dev server only...%RESET%
echo.

if not exist "node_modules" (
    echo   Installing npm packages...
    npm install --silent >nul 2>&1
)

echo   %GREEN%Frontend%RESET%  → http://localhost:5173
echo   %GREEN%API proxy%RESET% → http://localhost:5000 (configure in vite.config.ts)
echo.

npm run dev

endlocal
