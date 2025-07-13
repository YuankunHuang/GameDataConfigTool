# GameDataConfigTool

A production-ready, cross-platform tool for managing game data in Excel and exporting to multiple formats (binary, code, etc.), with Unity-friendly output, robust data validation, and full English code/comments.

## Features
- Excel-based data editing and validation
- Automatic code and binary export
- Unity integration ready (auto-generated code and data)
- Data validation and error reporting
- Supports enum, DateTime, and custom field types
- All generated code and comments are in English
- Namespace is fully configurable via `settings.json`
- Custom extension files are preserved in the `ext/` directory

## Requirements
- .NET 6.0 SDK or newer
- Windows, macOS, or Linux
- Excel files in `.xlsx` format (EPPlus compatible)

## Quick Start
1. Place your Excel files in the `excels/` directory.
2. Configure settings in `config/settings.json` (including your preferred namespace).
3. **Run the tool:**
   - On Windows: Double-click `build.bat` or run `build.bat` in a terminal.
   - On Mac/Linux: Run `sh build.sh` in a terminal.
   - Or, run directly with .NET:
     ```sh
     dotnet run
     ```
4. Find generated code in `Assets/Scripts/ConfigData/code/` and binary data in `Assets/StreamingAssets/ConfigData/` (if using Unity), or in the `output/` directory for standalone use.
5. Custom extension files are generated and preserved in `Assets/Scripts/ConfigData/code/ext/` (or `output/code/ext/`).
6. Integrate the generated code and data into your Unity project or other engine.

## Packaging & Distribution
- Only include the following directories/files when sharing:
  - `src/` (source code)
  - `excels/` (your Excel data or samples)
  - `config/` (settings)
  - `guide/` (documentation)
  - `build.bat`, `build.sh`, `GameDataTool.csproj`
  - `ext/` (custom extension files, if any)
- Do **not** include `bin/`, `obj/`, or Unity-specific directories unless you want to share Unity integration code.
- All debug and validation code has been removed for production use.

## Documentation
- See `guide/USAGE.md` for usage details.
- See `guide/DESIGNER_GUIDE.md` for Excel editing tips.
- See `guide/PROGRAMMER_GUIDE.md` for integration and extension.

## License
Add your license here (MIT, Apache, proprietary, etc).

---
For any issues or contributions, please contact the maintainer or open an issue in your repository. 