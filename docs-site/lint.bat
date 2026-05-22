@echo off
setlocal
cd /d "%~dp0"
call npm run lint:md
if errorlevel 1 (
    echo.
    echo Command failed with exit code %errorlevel%.
    pause
    exit /b %errorlevel%
)
call npm run lint:links
if errorlevel 1 (
    echo.
    echo Command failed with exit code %errorlevel%.
    pause
    exit /b %errorlevel%
)
endlocal
