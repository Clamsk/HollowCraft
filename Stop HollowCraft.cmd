@echo off
"%USERPROFILE%\.cache\codex-runtimes\codex-primary-runtime\dependencies\native\powershell\pwsh.exe" -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\stop-hollowcraft.ps1"
if errorlevel 1 pause

