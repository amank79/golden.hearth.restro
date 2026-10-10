@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0reset-pin.ps1" %*
