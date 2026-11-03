@echo off
rem ============================================================
rem  RESET THE BRAIN - BACKS UP FIRST, then wipes.
rem  A reset must never be unrecoverable: everything about to be
rem  deleted is copied to backups\reset-<timestamp>\ first, and if
rem  the copy fails THE RESET ABORTS with nothing deleted.
rem  Restore later with:  restore-brain.bat
rem ============================================================

rem Give the brain its farewell moment (if it's running)
curl -s -m 5 http://localhost:8080/api/farewell >nul 2>&1
if not errorlevel 1 (
  echo   The brain said its last words. See the console log.
  timeout /t 2 /nobreak >nul
)

taskkill /f /im BioComp.Dashboard.exe >nul 2>&1
cd /d "%~dp0src\BioDashboard"

rem ---- 1. BACKUP EVERYTHING THE RESET IS ABOUT TO DELETE ----
for /f %%i in ('powershell -NoProfile -Command "Get-Date -Format yyyyMMdd-HHmmss"') do set STAMP=%%i
set "BAK=%~dp0backups\reset-%STAMP%"
mkdir "%BAK%" >nul 2>&1
set FAILED=0
if exist data\brain-identity.json copy /y data\brain-identity.json "%BAK%\" >nul || set FAILED=1
if exist data\brain.ckpt             copy /y data\brain.ckpt             "%BAK%\" >nul || set FAILED=1
if exist data\episodes.jsonl         copy /y data\episodes.jsonl         "%BAK%\" >nul || set FAILED=1
if exist data\thoughts.jsonl         copy /y data\thoughts.jsonl         "%BAK%\" >nul || set FAILED=1
if exist data\thoughts-*.jsonl       copy /y data\thoughts-*.jsonl       "%BAK%\" >nul || set FAILED=1
if exist data\testament.md           copy /y data\testament.md           "%BAK%\" >nul || set FAILED=1
if exist data\letter-to-next-self.md copy /y data\letter-to-next-self.md "%BAK%\" >nul || set FAILED=1
if exist diary xcopy /y /e /i diary "%BAK%\diary" >nul || set FAILED=1
if exist dreams xcopy /y /e /i dreams "%BAK%\dreams" >nul || set FAILED=1
if %FAILED%==1 (
  echo.
  echo   BACKUP FAILED - RESET ABORTED. Nothing was deleted.
  echo   Check disk space or permissions, then try again.
  pause
  exit /b 1
)
echo   Backup saved: backups\reset-%STAMP%

rem ---- 2. NOW THE RESET (the letter stays for the newborn, as designed) ----
if exist data\brain-identity.json del /q data\brain-identity.json
if exist data\brain.ckpt del /q data\brain.ckpt
if exist data\episodes.jsonl del /q data\episodes.jsonl
if exist data\thoughts.jsonl del /q data\thoughts.jsonl
if exist data\thoughts-*.jsonl del /q data\thoughts-*.jsonl
if exist data\testament.md del /q data\testament.md
if exist diary rmdir /s /q diary
if exist dreams rmdir /s /q dreams
mkdir diary >nul 2>&1
mkdir dreams >nul 2>&1
echo.
echo   BRAIN RESET - it remembers nothing now.
echo   Everything it was is safe in backups\reset-%STAMP%
echo   (restore-brain.bat brings it back).
echo   Start it with start-dashboard.cmd
echo.
pause
