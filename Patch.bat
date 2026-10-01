@echo off
setlocal
cd /d "%~dp0"

echo ========================================================
echo   Assetto Corsa Track Day Timer ^& Session End Patcher
echo ========================================================
echo.

powershell -ExecutionPolicy Bypass -File "%~dp0scripts\Patch.ps1"

echo.
pause
