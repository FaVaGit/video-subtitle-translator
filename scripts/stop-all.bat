@echo off
setlocal
chcp 65001 >nul 2>&1

set "ROOT=%~dp0.."

echo.
echo   Stopping all services...

taskkill /fi "WINDOWTITLE eq VST Worker" /f >nul 2>&1
taskkill /fi "WINDOWTITLE eq VST API" /f >nul 2>&1
powershell -NoProfile -Command "Get-CimInstance Win32_Process | Where-Object { ($_.Name -ieq 'dotnet.exe' -and $_.CommandLine -like '*VideoSubtitleTranslator.Worker*') -or $_.Name -ieq 'VideoSubtitleTranslator.Worker.exe' } | ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }" >nul 2>&1
powershell -NoProfile -Command "Get-CimInstance Win32_Process | Where-Object { ($_.Name -ieq 'dotnet.exe' -and $_.CommandLine -like '*VideoSubtitleTranslator.Api*') -or $_.Name -ieq 'VideoSubtitleTranslator.Api.exe' } | ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }" >nul 2>&1
taskkill /fi "WINDOWTITLE eq NATS Server" /f >nul 2>&1
docker stop vst-nats >nul 2>&1
docker rm vst-nats >nul 2>&1
cd /d "%ROOT%\docker"
docker compose down >nul 2>&1

echo   All services stopped.

endlocal
