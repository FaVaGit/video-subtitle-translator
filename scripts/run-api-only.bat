@echo off
setlocal
chcp 65001 >nul 2>&1
title Video Subtitle Translator - API Only

set "ROOT=%~dp0.."
cd /d "%ROOT%\src\Backend"

echo.
echo   [API] Starting backend API only...
echo   ═════════════════════════════════════════
echo.

dotnet build --nologo -q >nul 2>&1

echo         API       http://localhost:5000
echo         Swagger   http://localhost:5000/swagger
echo.

dotnet run --project VideoSubtitleTranslator.Api --no-build --urls "http://localhost:5000"

endlocal
