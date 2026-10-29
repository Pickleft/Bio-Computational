@echo off
rem Bio-Computational dashboard launcher.
rem Detached from any terminal: logs land in data\dashboard-out.log / dashboard-err.log
rem Open http://localhost:8080 (or https://localhost:8443). Phone: https://<your-pc-ip>:8443
cd /d "%~dp0"
if not exist data mkdir data
set ASPNETCORE_URLS=http://0.0.0.0:8080;https://0.0.0.0:8443
start "brain" /min cmd /c "dotnet run --project src\BioDashboard -c Release -- --live-meta --voice > data\dashboard-out.log 2> data\dashboard-err.log"
echo brain starting - dashboard: http://localhost:8080
