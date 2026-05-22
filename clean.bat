@echo off
setlocal
cd /d "%~dp0"
dotnet clean ValIO.sln
if errorlevel 1 (
    echo.
    echo Command failed with exit code %errorlevel%.
    pause
    exit /b %errorlevel%
)
if exist nupkgs rmdir /s /q nupkgs
if exist publish rmdir /s /q publish
endlocal
