@echo off
chcp 65001 >nul
echo ========================================
echo    Game Data Tool - One-Click Build
echo ========================================
echo.

REM Step 1: Check for Excel files
dir /b excels\*.xlsx >nul 2>nul
if errorlevel 1 (
    echo ERROR: No Excel files found in the excels/ directory
    pause
    exit /b 1
)
echo Excel files detected, starting build...
echo.

REM Step 2: dotnet build
dotnet build --verbosity minimal
if %errorlevel% neq 0 (
    echo Build failed, please check for code errors
    pause
    exit /b 1
)
echo Build succeeded.
echo.

REM Step 3: dotnet run
dotnet run --verbosity minimal
if %errorlevel% neq 0 (
    echo Generation failed, please check your Excel file format
    pause
    exit /b 1
)
echo.
echo Data generation succeeded.
echo.

pause 