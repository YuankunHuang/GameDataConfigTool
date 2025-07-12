# GameDataTool (Intelligent Edition)

## 🎯 Tool Purpose
This is an intelligent game data configuration tool, designed for game development teams:
- **Designers:** Just add Excel files and click to generate data
- **Programmers:** Auto-generate strong-typed code, ready for Unity integration
- **Team Collaboration:** Standardized workflow, reduced communication cost

## 🚀 Quick Start (Designer)

### 3 Steps to Configure Data

1. **Add Excel Files**
   ```
   excels/
   ├── Character.xlsx          # Character data
   ├── Item.xlsx              # Item data
   └── EnumTypes/
       ├── CharacterType.xlsx  # Character type enum
       └── ItemType.xlsx       # Item type enum
   ```

2. **Fill in Data Format**
   ```
   | ID | Name | Type | Level | Cost |
   |----|------|------|-------|------|
   | int | string | enum | int | float |
   | Character ID | Character Name | Character Type | Level | Cost |
   | 1 | Warrior | 1 | 10 | 100.5 |
   | 2 | Mage | 2 | 15 | 200.0 |
   ```

3. **One-Click Generation**
   ```
   Double-click generate.bat
   ```

## 🎮 Unity Integration (Programmer)

### Auto Deployment
```bash
# Run the deployment script
deploy_to_unity.bat
# Enter your Unity project path
```

### Use in Game
```csharp
// Initialize
gameDataManager.Initialize();

// Get data
Character[] characters = GameDataManager.GetCharacters();
Character warrior = GameDataManager.GetCharacter(1);
```

## 📁 Directory Structure

```
GameDataTool/
├── config/
│   └── settings.json          # Tool config
├── excels/                    # Excel data files
│   ├── Character.xlsx
│   └── EnumTypes/
│       └── CharacterType.xlsx
├── src/                       # Tool source code
├── Unity/                     # Unity-specific code
│   └── GameDataManager.cs
├── output/                    # Generated files
│   ├── json/                  # JSON data files
│   ├── binary/                # Binary data files
│   └── code/                  # C# code files
├── generate.bat               # One-click generation
├── deploy_to_unity.bat        # Auto deployment
├── DESIGNER_GUIDE.md          # Designer guide
└── PROGRAMMER_GUIDE.md        # Programmer guide
```

## 🎯 Intelligent Environment Detection

The tool automatically detects the running environment:

### Standalone Environment
- Detected: `🛠️  Standalone tool environment, output to local output/ directory`
- Use: Testing, development, standalone use

### Unity Project Environment
- Detected: `🎮 Unity project environment detected, output to Assets/Scripts/ConfigData/`
- Use: Direct Unity project integration

## 📋 Team Workflow

### Designer Workflow
1. **Add new data:** Add Excel files to `excels/` directory
2. **Fill in data:** Follow the required format
3. **Generate data:** Double-click `generate.bat` to generate files
4. **Notify programmers:** Let them know new data is available

### Programmer Workflow
1. **Receive data:** Get generated files from designers
2. **Deploy to Unity:** Run `deploy_to_unity.bat` for auto deployment
3. **Integrate code:** Use `GameDataManager` in your game
4. **Test and verify:** Ensure data loads correctly

## 📋 Features

- ✅ **Designer-friendly** - Simple Excel format, one-click generation
- ✅ **Programmer-friendly** - Auto-generated strong-typed code, Unity integration
- ✅ **Intelligent environment detection** - Standalone or Unity project
- ✅ **Excel data parsing** - Supports data tables and enum tables
- ✅ **Data validation** - Auto checks for completeness and type
- ✅ **Multi-format output** - JSON, binary, C# code
- ✅ **Unity integration** - Auto-generated Unity-ready code
- ✅ **Cross-platform** - Windows, Mac, Linux
- ✅ **Auto deployment** - One-click deployment to Unity project
- ✅ **Team collaboration** - Standardized workflow, reduced communication cost

## 🔧 Configuration Options

Edit `config/settings.json` to adjust:
- Output path (overridden by environment detection)
- Output formats
- Code namespace
- Validation rules
- Log level

## 📖 Documentation

- [Designer Guide](DESIGNER_GUIDE.md) - Excel format and usage
- [Programmer Guide](PROGRAMMER_GUIDE.md) - Unity integration and API usage

## 🎯 Team Collaboration Benefits

1. **Designers:**
   - Use familiar Excel tools
   - Simple data format
   - One-click generation, no coding required

2. **Programmers:**
   - Auto-generated strong-typed code
   - Direct Unity integration
   - Type safety, fewer runtime errors

3. **Team:**
   - Standardized data format
   - Automated workflow
   - Reduced communication cost
   - Improved development efficiency

The whole process is smart, efficient, and supports seamless team collaboration! 