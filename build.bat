@echo off
chcp 65001 >nul
echo.
echo ========================================
echo    Game Data Tool - One-Click Build
echo ========================================
echo.

REM Check for Excel files
if not exist "excels\*.xlsx" (
    echo ❌ ERROR: No Excel files found in the excels/ directory
    echo.
    echo 📋 Please follow these steps:
    echo 1. Add Excel files to the excels/ directory
    echo 2. Make sure the file format is correct (see DESIGNER_GUIDE.md)
    echo 3. Run this script again
    echo.
    pause
    exit /b 1
)

echo ✅ Excel files detected, starting build...
echo.

REM Build the tool
echo 🔨 Building tool...
dotnet build --verbosity quiet
if %errorlevel% neq 0 (
    echo ❌ Build failed, please check for code errors
    pause
    exit /b 1
)

REM Generate data
echo 📊 Generating data files...
dotnet run --verbosity quiet
if %errorlevel% neq 0 (
    echo ❌ Generation failed, please check your Excel file format
    pause
    exit /b 1
)

echo.
echo 🎉 Build complete!
echo.
echo 📁 Generated files:
echo    📄 JSON data: output/json/
echo    🔢 Binary data: output/binary/
echo    💻 C# code: output/code/
echo.

REM Ask if user wants to deploy to Unity
set /p DEPLOY_CHOICE="Do you want to deploy to Unity project? (y/n): "
if /i "%DEPLOY_CHOICE%"=="y" (
    echo.
    set /p UNITY_PROJECT_PATH="Enter Unity project path (e.g. C:\MyGame): "
    
    if not exist "%UNITY_PROJECT_PATH%" (
        echo ❌ ERROR: Unity project path does not exist
        pause
        exit /b 1
    )
    
    if not exist "%UNITY_PROJECT_PATH%\Assets" (
        echo ❌ ERROR: This is not a valid Unity project (Assets directory missing)
        pause
        exit /b 1
    )
    
    echo ✅ Unity project detected, deploying...
    echo.
    
    REM Create target directories
    if not exist "%UNITY_PROJECT_PATH%\Assets\Scripts\GameData" (
        mkdir "%UNITY_PROJECT_PATH%\Assets\Scripts\GameData"
        echo 📁 Created Scripts/GameData directory
    )
    
    if not exist "%UNITY_PROJECT_PATH%\Assets\StreamingAssets\GameData" (
        mkdir "%UNITY_PROJECT_PATH%\Assets\StreamingAssets\GameData"
        echo 📁 Created StreamingAssets/GameData directory
    )
    
    REM Copy code files
    echo 📄 Copying C# code files...
    xcopy "output\code\*.cs" "%UNITY_PROJECT_PATH%\Assets\Scripts\GameData\" /Y /Q
    
    REM Copy Unity-specific files
    if exist "Unity\GameDataManager.cs" (
        copy "Unity\GameDataManager.cs" "%UNITY_PROJECT_PATH%\Assets\Scripts\GameData\" /Y
        echo 📄 Copied GameDataManager.cs
    )
    
    REM Copy JSON data files
    echo 📄 Copying JSON data files...
    xcopy "output\json\*.json" "%UNITY_PROJECT_PATH%\Assets\StreamingAssets\GameData\" /Y /Q
    
    echo.
    echo 🎉 Deployment complete!
    echo.
    echo 📁 Deployed to:
    echo    💻 C# code: %UNITY_PROJECT_PATH%\Assets\Scripts\GameData\
    echo    📄 JSON data: %UNITY_PROJECT_PATH%\Assets\StreamingAssets\GameData\
    echo.
    echo 💡 In Unity:
    echo    1. Call GameDataManager.Initialize() in your GameManager's Start() method
    echo    2. Use GameDataManager.GetCharacters() and other methods to access data
    echo.
) else (
    echo.
    echo 💡 To deploy later, run: build.bat
    echo.
)

pause 