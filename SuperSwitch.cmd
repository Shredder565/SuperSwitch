@echo off
rem SuperSwitch one-click launcher: rebuilds if the source changed, then starts SuperSwitch.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0tools\SuperSwitch.ps1" %*
