@echo off
cd /d "%~dp0"
REM powershell -ExecutionPolicy Bypass -NoLogo -NoProfile -File ".\http-scenario.ps1"

powershell -ExecutionPolicy Bypass -File .\interactive-scenario.ps1
pause