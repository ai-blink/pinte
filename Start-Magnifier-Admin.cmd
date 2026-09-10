@echo off
"%ProgramFiles%\PowerShell\7\pwsh.exe" -NoProfile -File "%~dp0scripts\Start-Magnifier.ps1" -Mode App -Administrator
if errorlevel 1 pause
