@echo off
rem ============================================================
rem  RESTORE THE BRAIN - undoes a reset from the newest backup
rem  (or a specific one:  restore-brain.bat backups\reset-...  )
rem  Stops the brain, copies everything back, done.
rem ============================================================
setlocal enabledelayedexpansion

set "TARGET=%~1"
if "%TARGET%"=="" (
  rem newest backup by name (timestamps sort naturally)
  set "NEWEST="
  for /f "delims=" %%d in ('dir /b /ad /o-n "%~dp0backups\reset-*" 2^>nul') do (
    if not defined NEWEST set "NEWEST=%%d"
  )
  if not defined NEWEST (
    echo   No backups found in backups\. Nothing to restore.
    pause
    exit /b 1
  )
  set "TARGET=%~dp0backups\!NEWEST!"
  echo   Restoring newest backup: !NEWEST!
)

if not exist "%TARGET%" (
  echo   Backup folder not found: %TARGET%
  pause
  exit /b 1
)

taskkill /f /im BioComp.Dashboard.exe >nul 2>&1
cd /d "%~dp0src\BioDashboard"

if exist "%TARGET%\brain-identity.json" copy /y "%TARGET%\brain-identity.json" data\ >nul
if exist "%TARGET%\brain.ckpt"           copy /y "%TARGET%\brain.ckpt"           data\ >nul
if exist "%TARGET%\episodes.jsonl"       copy /y "%TARGET%\episodes.jsonl"       data\ >nul
if exist "%TARGET%\thoughts.jsonl"       copy /y "%TARGET%\thoughts.jsonl"       data\ >nul
if exist "%TARGET%\thoughts-*.jsonl"     copy /y "%TARGET%\thoughts-*.jsonl"     data\ >nul
if exist "%TARGET%\testament.md"         copy /y "%TARGET%\testament.md"         data\ >nul
if exist "%TARGET%\diary" xcopy /y /e /i "%TARGET%\diary" diary >nul
if exist "%TARGET%\dreams" xcopy /y /e /i "%TARGET%\dreams" dreams >nul

echo.
echo   RESTORED from %TARGET%
echo   Start it with start-dashboard.cmd - it should remember itself.
echo.
pause
