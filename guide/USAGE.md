# Game Data Tool Usage Guide

## 🚀 Quick Start

### 1. Prepare Excel Files

Organize your Excel files in the following format:

```
excels/
├── EnumTypes/           # Enum type definitions
│   ├── CharacterType.xlsx
│   └── ItemType.xlsx
├── Character.xlsx       # Character data table
├── Item.xlsx           # Item data table
└── Skill.xlsx          # Skill data table
```

### 2. Excel File Format

#### Data Table Format
The first three rows of each data table are definition rows:

- **Row 1**: Field names
- **Row 2**: Field types
- **Row 3**: Field descriptions
- **Row 4 onwards**: Data

Example:
| ID | Name | Type | Level | Cost |
|----|------|------|-------|------|
| int | string | enum | int | float |
| Character ID | Character Name | Character Type | Level | Cost |
| 1 | Warrior | 1 | 10 | 100.5 |
| 2 | Mage | 2 | 15 | 200.0 |

#### Enum Table Format
Enum tables have only 3 columns:

- **Column 1**: Enum value name
- **Column 2**: Enum value
- **Column 3**: Description

Example:
| Name | Value | Description |
|------|-------|-------------|
| Warrior | 1 | Warrior |
| Mage | 2 | Mage |
| Archer | 3 | Archer |

### 3. Run the Tool

```bash
# Direct execution
./GameDataTool

# Or specify configuration file
./GameDataTool --config custom_config.json
```

### 4. View Output

The tool will generate the following files in the `output/` directory:

#### JSON Format (for debugging)
```
output/json/
├── Character.json
├── Item.json
└── CharacterType.json
```

#### Binary Format (for game)
```
output/binary/
├── Character.data
├── Item.data
└── index.json
```

#### Code Format (for development)
```
output/code/
├── Enums.cs
├── Character.cs
├── Item.cs
└── DataLoader.cs
```

## 💾 Binary Data Format

### Advantages
- **Fast loading**: 10-100x faster than JSON
- **Small file size**: 50-80% smaller than JSON
- **Memory efficient**: Direct memory mapping
- **Type safe**: Compile-time type checking

### Format Description
```
[Field count: int32]
[Row count: int32]
[Field info: field name + type] * field count
[Data rows] * row count
```

### Usage Example
```csharp
// Use in game
var characterData = DataLoader.GetData<Character[]>("Character");
var itemData = DataLoader.GetData<Item[]>("Item");
```

## 🔧 Configuration Options

Edit `config/settings.json`:

```json
{
  "excelPath": "excels/",
  "enumPath": "EnumTypes",
  "outputPaths": {
    "json": "output/json/",
    "binary": "output/binary/",
    "code": "output/code/"
  },
  "generators": {
    "enableJson": true,
    "enableBinary": true,
    "enableCode": true
  },
  "codeGeneration": {
    "namespace": "GameData",
    "generateEnum": true,
    "generateLoader": true
  }
}
```

## 🏭 Build and Distribution

### Build Single File Version
```bash
# Windows
build.bat

# Mac/Linux
chmod +x build.sh
./build.sh
```

### Distribute to Team
Only distribute the following files:
- `GameDataTool.exe` (Windows) or `GameDataTool` (Mac/Linux)
- `config/settings.json`
- Example Excel files

Team members can use without installing any dependencies.

## 📈 Performance Comparison

| Format | Loading Speed | File Size | Memory Usage | Use Case |
|--------|---------------|-----------|--------------|----------|
| JSON | Baseline | Baseline | Baseline | Debug, Development |
| Binary | 10-100x | 50-80% | 30-50% | Production |

## 🎯 Use Cases

### Game Development
- **Client data**: Character, item, skill configurations
- **Server data**: Level, quest, reward configurations
- **Localization data**: Text, audio, image configurations

### Team Collaboration
- **Design team**: Edit data using Excel
- **Programming team**: Use generated code and binary files
- **Testing team**: Use JSON files for data validation

## 🔍 Data Validation

The tool automatically performs the following validations:
- ✅ Type checking: Ensure data matches field types
- ✅ Required fields: Check ID, Name and other required fields
- ✅ Data integrity: Check if data rows are complete

## 🛠️ Extension Features

### Add New Data Types
1. Add new types to the `FieldType` enum
2. Update type conversion logic
3. Add type mappings in the code generator

### Custom Validation Rules
1. Add validation methods in `DataValidator`
2. Add validation options in configuration file
3. Call new methods in the validation process

## 📋 Best Practices

1. **File naming**: Use meaningful file names
2. **Data organization**: Put related data in the same Excel file
3. **Field naming**: Use clear field names
4. **Version control**: Include configuration files in version control
5. **Team sharing**: Regularly update executable files

## 🆘 Troubleshooting

### Common Issues
1. **Cannot read Excel files**: Check file format and path
2. **Data validation fails**: Check field types and data format
3. **Output files are empty**: Check Excel file format and data

### Getting Help
- Check console output error messages
- Check configuration file format
- Verify Excel file format

## 📄 License

MIT License

## 🤝 Contributing

Welcome to submit Issues and Pull Requests!

---

**Game Data Tool** - Making game data configuration simple and efficient! 