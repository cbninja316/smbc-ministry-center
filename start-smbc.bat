@echo off
echo Stopping any existing SMBC backend on port 5000...
for /f "tokens=5" %%a in ('netstat -aon ^| findstr ":5000 " ^| findstr "LISTENING"') do (
    taskkill /PID %%a /F >nul 2>&1
)
timeout /t 1 /nobreak >nul

echo Starting SMBC backend...
start "SMBC Backend" cmd /k "cd /d C:\Users\offic\smbc-ministry-center\smbc-ministry-center\backend\SmbcStatusBoard.Api && dotnet run"
timeout /t 4 /nobreak >nul

echo Starting ngrok tunnel...
start "SMBC Ngrok" cmd /k "ngrok http --domain=glorious-magical-overboard.ngrok-free.dev 5000"
echo.
echo Both windows are starting. You can minimize them but do not close them.
