@echo off
echo Creating git version file...
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0updateversion.ps1"
exit 0
