@echo off
cd /d "%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0tools\clear_casual_pvp_today_cache.ps1"
echo.
pause
