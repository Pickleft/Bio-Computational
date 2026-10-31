@echo off
rem ============================================================
rem  RESET THE BRAIN - gives it its last moment, then wipes
rem  all memory, identity and artifacts. The next start is
rem  a true newborn (seed stays the same).
rem ============================================================

rem Give the brain its farewell moment (if it's running)
curl -s -m 5 http://localhost:8080/api/farewell >nul 2>&1
if not errorlevel 1 (
  echo  The brain said its last words. See the console log.
  timeout /t 2 /nobreak >nul
)

taskkill /f /im BioComp.Dashboard.exe >nul 2>&1
cd /d "%~dp0src\BioDashboard"
if exist data\brain-identity.json del /q data\brain-identity.json
if exist data\brain.ckpt del /q data\brain.ckpt
if exist data\episodes.jsonl del /q data\episodes.jsonl
if exist data\testament.md del /q data\testament.md
if exist diary rmdir /s /q diary
if exist dreams rmdir /s /q dreams
mkdir diary >nul 2>&1
mkdir dreams >nul 2>&1
echo.
echo  BRAIN RESET - it remembers nothing now.
echo  Start it with start-dashboard.cmd
echo.
pause
