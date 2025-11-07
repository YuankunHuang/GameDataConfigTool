#!/bin/bash
set -e

clear
echo
echo "========================================="
echo "   Game Data Tool - One-Click Build    "
echo "========================================="
echo

# Check for Excel files
if ! ls excels/*.xlsx 1> /dev/null 2>&1; then
    echo "❌ ERROR: No Excel files found in the excels/ directory"
    echo
    echo "📋 Please follow these steps:"
    echo "1. Add Excel files to the excels/ directory"
    echo "2. Make sure the file format is correct (see DESIGNER_GUIDE.md)"
    echo "3. Run this script again"
    echo
    read -n 1 -s -r -p "Press any key to exit..."
    exit 1
fi

echo "✅ Excel files detected, starting build..."
echo

echo "🔨 Building tool..."
dotnet build GameDataConfigTool.sln --verbosity quiet

echo "📊 Generating data files..."
dotnet run --project GameDataConfigTool.csproj --verbosity quiet

echo
echo "🎉 Build complete!"
echo

echo "📁 Generated files:"
echo "   📄 JSON data: output/json/"
echo "   🔢 Binary data: output/binary/"
echo "   💻 C# code: output/code/"
echo

read -p "Do you want to deploy to Unity project? (y/n): " DEPLOY_CHOICE
if [[ "$DEPLOY_CHOICE" =~ ^[Yy]$ ]]; then
    echo
    read -p "Enter Unity project path (e.g. /Users/yourname/MyGame): " UNITY_PROJECT_PATH
    if [ ! -d "$UNITY_PROJECT_PATH" ]; then
        echo "❌ ERROR: Unity project path does not exist"
        read -n 1 -s -r -p "Press any key to exit..."
        exit 1
    fi
    if [ ! -d "$UNITY_PROJECT_PATH/Assets" ]; then
        echo "❌ ERROR: This is not a valid Unity project (Assets directory missing)"
        read -n 1 -s -r -p "Press any key to exit..."
        exit 1
    fi
    echo "✅ Unity project detected, deploying..."
    echo
    mkdir -p "$UNITY_PROJECT_PATH/Assets/Scripts/GameData"
    mkdir -p "$UNITY_PROJECT_PATH/Assets/StreamingAssets/GameData"
    echo "📄 Copying C# code files..."
    cp output/code/*.cs "$UNITY_PROJECT_PATH/Assets/Scripts/GameData/"
    if [ -f Unity/GameDataManager.cs ]; then
        cp Unity/GameDataManager.cs "$UNITY_PROJECT_PATH/Assets/Scripts/GameData/"
        echo "📄 Copied GameDataManager.cs"
    fi
    echo "📄 Copying JSON data files..."
    cp output/json/*.json "$UNITY_PROJECT_PATH/Assets/StreamingAssets/GameData/"
    echo
    echo "🎉 Deployment complete!"
    echo
    echo "📁 Deployed to:"
    echo "   💻 C# code: $UNITY_PROJECT_PATH/Assets/Scripts/GameData/"
    echo "   📄 JSON data: $UNITY_PROJECT_PATH/Assets/StreamingAssets/GameData/"
    echo
    echo "💡 In Unity:"
    echo "   1. Call GameDataManager.Initialize() in your GameManager's Start() method"
    echo "   2. Use GameDataManager.GetCharacters() and other methods to access data"
    echo
else
    echo
    echo "💡 To deploy later, run: ./build.sh"
    echo
fi

read -n 1 -s -r -p "Press any key to exit..." 