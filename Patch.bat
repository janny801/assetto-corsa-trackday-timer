@echo off
setlocal
cd /d "%~dp0"

echo ========================================================
echo   Assetto Corsa Track Day Timer ^& Safe Lua Installer
echo ========================================================
echo.

powershell -ExecutionPolicy Bypass -Command "$target='C:\Program Files (x86)\Steam\steamapps\common\assettocorsa'; Copy-Item -LiteralPath '%~dp0apps' -Destination $target -Recurse -Force; Write-Host 'Safe timer app installed. Original Content Manager and acs.exe were not modified.' -ForegroundColor Green"

echo.
pause
