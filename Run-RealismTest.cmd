@echo off
setlocal
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0tools\run_demo.ps1" -RealismTest %*
if errorlevel 1 pause
