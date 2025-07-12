# 🎨 Designer Guide

## 🚀 Quick Start (3 Steps)

### Step 1: Add Excel Data
Place your Excel files in the `excels/` directory:

```
excels/
├── Character.xlsx          # Character data
├── Item.xlsx              # Item data
├── Skill.xlsx             # Skill data
└── EnumTypes/
    ├── CharacterType.xlsx  # Character type enum
    ├── ItemType.xlsx       # Item type enum
    └── SkillType.xlsx      # Skill type enum
```

**💡 Tip:** You can copy `Template.xlsx` as a template for new tables.

### Step 2: Fill in the Data Format

#### Data Table Format (e.g. Character.xlsx)
| ID | Name | Type | Level | Cost |
|----|------|------|-------|------|
| int | string | enum | int | float |
| Character ID | Character Name | Character Type | Level | Cost |
| 1 | Warrior | 1 | 10 | 100.5 |
| 2 | Mage | 2 | 15 | 200.0 |

**📋 Format Explanation:**
- **Row 1:** Data type (int/string/float/bool/enum)
- **Row 2:** Field name (in English, for code generation)
- **Row 3:** Field description (in your language, for comments)
- **Row 4+:** Actual data

#### Enum Table Format (e.g. CharacterType.xlsx)
| Name | Value | Description |
|------|-------|-------------|
| Warrior | 1 | Warrior |
| Mage | 2 | Mage |

### Step 3: One-Click Generation
Double-click the `generate.bat` file and wait for completion!

## 📁 Output Files

After generation, check the `output/` directory:

- `json/` - JSON format data (for debugging)
- `binary/` - Binary format data (for the game)
- `code/` - C# code files (for programmers)

## 🔧 Advanced Features

### Auto-Deploy to Unity Project
1. Run `deploy_to_unity.bat`
2. Enter your Unity project path
3. All files will be copied automatically

### Supported Data Types
- `int` - Integer
- `string` - String
- `float` - Float
- `bool` - Boolean
- `enum` - Enum (requires a corresponding enum table)

## 🆘 FAQ

**Q: What if generation fails?**
A:
1. Check if the Excel file format is correct
2. Make sure the first 3 rows are definition rows
3. Check the console for error messages
4. Refer to the format in `Template.xlsx`

**Q: How to add a new data type?**
A:
1. Add a new column in Excel
2. Fill in the type row with the correct type (int/string/float/bool/enum)
3. Fill in the name row with an English field name
4. Fill in the description row

**Q: Enum value mismatch?**
A:
1. Make sure enum values in the data table are defined in the corresponding enum table
2. Check the Value column in the enum table
3. Make sure the enum table is in the `EnumTypes/` directory

**Q: Garbled characters in non-English?**
A:
1. Make sure your Excel file is saved as UTF-8
2. Use Excel's "Save As" and select UTF-8 encoding

**Q: How to batch add data?**
A:
1. Use Excel's fill handle
2. Copy and paste data
3. Use Excel formulas to generate sequences

## 📋 Best Practices

1. **Naming conventions:**
   - Use English for file names, e.g. `Character.xlsx`
   - Use English for field names, e.g. `Name`, `Level`
   - Use your language for descriptions, for clarity

2. **Data validation:**
   - Ensure ID fields are unique
   - Check value ranges are reasonable
   - Validate enum values are correct

3. **Version control:**
   - Add Excel files to version control
   - Generated files can be ignored (regenerate as needed)

4. **Team collaboration:**
   - Designers manage Excel data
   - Programmers handle code integration
   - The tool automatically handles format conversion 