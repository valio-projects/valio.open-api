@echo off
setlocal
cd /d "%~dp0"
call npm run build
if errorlevel 1 (
    echo.
    echo Command failed with exit code %errorlevel%.
    pause
    exit /b %errorlevel%
)
endlocal
