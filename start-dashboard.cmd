@echo off
rem ============================================================
rem  START THE BRAIN (dashboard + senses + voice, live Meta API)
rem  - runs the built exe directly: no launch-settings surprises,
rem    correct ports guaranteed
rem  - a minimized "brain" console window is its life support:
rem    open it to see the log, close it to stop the brain
rem ============================================================
cd /d "%~dp0"

set EXE=src\BioDashboard\bin\Release\net9.0\BioComp.Dashboard.exe

if not exist "%EXE%" (
  echo first run: building...
  dotnet build BioComputational.sln -c Release --nologo -v q
  if errorlevel 1 ( echo BUILD FAILED - run: dotnet build BioComputational.sln & pause & exit /b 1 )
)
if not exist src\BioDashboard\data mkdir src\BioDashboard\data

taskkill /f /im BioComp.Dashboard.exe >nul 2>&1

start "brain" /min /d "%~dp0src\BioDashboard" "%~dp0%EXE%" "--urls=http://0.0.0.0:8080;https://0.0.0.0:8443" --live-meta --voice

echo.
echo   brain starting...
echo   this PC :  http://localhost:8080
echo   phone   :  https://YOUR-PC-IP:8443  (accept the certificate warning)
echo   to stop :  close the minimized "brain" window
echo.
timeout /t 4 /nobreak >nul 2>&1 || ping -n 5 127.0.0.1 >nul 2>&1
start http://localhost:8080
