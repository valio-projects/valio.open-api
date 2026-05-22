@echo off
setlocal
cd /d "%~dp0"
dotnet test ValIO.sln -c Release --nologo %*
if errorlevel 1 (
    echo.
    echo Command failed with exit code %errorlevel%.
    pause
    exit /b %errorlevel%
)
endlocal
