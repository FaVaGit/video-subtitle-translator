@echo off
setlocal
chcp 65001 >nul 2>&1
title Video Subtitle Translator - Frontend Only

set "ROOT=%~dp0.."
cd /d "%ROOT%\src\Frontend"

echo.
echo   [FRONTEND] Starting React dev server only...
echo   ═════════════════════════════════════════
echo.

if not exist "node_modules" (
    echo   Installing npm packages...
    npm install --silent >nul 2>&1
)

echo         Frontend  http://localhost:5173
echo         API proxy http://localhost:5000
echo.

npm run dev

endlocal
