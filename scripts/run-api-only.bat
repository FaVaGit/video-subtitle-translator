@echo off
setlocal
chcp 65001 >nul 2>&1
title Video Subtitle Translator - API Only

set "ROOT=%~dp0.."
cd /d "%ROOT%\src\Backend"

set "GREEN=[92m"
set "CYAN=[96m"
set "RESET=[0m"

echo %CYAN%[API] Starting backend API only...%RESET%
echo.

dotnet build --nologo -q >nul 2>&1

echo   %GREEN%API%RESET%       → http://localhost:5000
echo   %GREEN%Swagger%RESET%   → http://localhost:5000/swagger
echo.

dotnet run --project VideoSubtitleTranslator.Api --no-build --urls "http://localhost:5000"

endlocal
