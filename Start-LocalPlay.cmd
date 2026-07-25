@echo off
setlocal
cd /d "%~dp0"

echo Starting LocalPlay...
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\run.ps1"

if errorlevel 1 (
    echo.
    echo LocalPlay could not be started. See the error above.
    pause
    exit /b 1
)

endlocal
