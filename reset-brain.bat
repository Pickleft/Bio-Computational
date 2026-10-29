@echo off
rem ============================================================
rem  RESET THE BRAIN - wipes all memory, identity and artifacts.
rem  The next start is a true newborn (seed stays the same).
rem ============================================================
taskkill /f /im BioComp.Dashboard.exe >nul 2>&1
cd /d "%~dp0src\BioDashboard"
if exist data\brain-identity.json del /q data\brain-identity.json
if exist data\brain.ckpt del /q data\brain.ckpt
if exist diary rmdir /s /q diary
if exist dreams rmdir /s /q dreams
mkdir diary >nul 2>&1
mkdir dreams >nul 2>&1
echo.
echo  BRAIN RESET - it remembers nothing now.
echo  Start it with start-dashboard.cmd
echo.
pause
