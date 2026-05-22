@echo off
setlocal
cd /d "%~dp0"
dotnet pack ValIO.sln -c Release -o .\nupkgs %*
if errorlevel 1 (
    echo.
    echo Command failed with exit code %errorlevel%.
    pause
    exit /b %errorlevel%
)
endlocal
