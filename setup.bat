@echo off
chcp 65001 >nul
cd /d "%~dp0"
dotnet run --project GameDataConfigTool.csproj --verbosity quiet -- --setup
pause
