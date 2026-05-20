@echo off
REM Wrapper around the ValIO CLI exe. Pass through all arguments.
"%~dp0src\ValIO.Cli\bin\Debug\net10.0\valio.exe" %*
