# GameDataConfigTool

**.NET 6** command-line tool: **Excel (`.xlsx`) → validated outputs** — compact **binary** for runtime, optional **JSON**, and **C#** types plus a **Unity-friendly** loader pattern (`BaseConfigData<T>`, `GameDataManager`). Validation runs **before** any file is written; failure exits with a non-zero code (CI-friendly).

**Author:** [Yuankun Huang](https://github.com/YuankunHuang)

---

## What it does

| Stage | Responsibility |
|--------|------------------|
| **Parse** | Read data tables and enum workbooks ([EPPlus](https://github.com/EPPlusSoftware/EPPlus)). |
| **Validate** | Types, non-nullable cells, enums, foreign keys, duplicate `id`, field names. |
| **Generate** | `*.data` + `index.json`, optional `*.json`, and C# (`Enums.cs`, `*Config.cs`, `*Data`, `BaseConfigData.cs`, `GameDataManager.cs`). |

Hand-written code lives in **`ext/`** partials; those files are **not overwritten** after the first creation.

---

## Layout & conventions

**Run from the repository root** so `excels/`, `config/settings.json`, and relative `outputPaths` resolve correctly.

### Workbooks

- **Tables:** one `.xlsx` per table in `excels/` (top level only, **first sheet**). Row 1 = headers, row 2+ = data.
- **Enums:** `.xlsx` files under `excels/<enumPath>/` (e.g. `excels/EnumTypes/`). Row 1 = header; columns: name, int value, optional description.

### Primary key

- **Column A must be `id|int`.** It cannot use `|nullable`.
- Duplicate **`id`** values are rejected.
- Generated **`GetById`** indexes on the `Id` property (same convention).

### Column header

`FieldName|Type` or `FieldName|Type|nullable`

| Part | Meaning |
|------|---------|
| **FieldName** | Becomes the C# property (PascalCase). |
| **Type** | See [Type syntax](#type-syntax) below. |
| **nullable** (optional) | Blank cells are allowed and are filled with the **type default** before validation and export (`0` / `false` / `""` / `0001-01-01 00:00:00` for `datetime` → `DateTime.MinValue`). |

If **`enforceNonNullableColumns`** is on in settings, any column **without** `|nullable` must have a non-empty value (after trim). Non-empty values are checked against the declared type.

Header **cell comments** become **XML documentation** on the generated properties.

### Type syntax

| Pattern | Meaning |
|---------|---------|
| `int`, `long`, `float`, `string`, `bool`, `datetime` | Scalars (`date` → `datetime`). Unknown names are errors. |
| `enum(EnumTypeName)` | Integer enum; cell may be a number or an enum member name from the enum workbook. |
| `int^Range(min,max)` | For `int`: value in **[min, max)** (inclusive min, exclusive max). |
| `int^id(RefTable)` | Foreign key: value must exist in the **`id`** column of the data table whose workbook is named `RefTable`. |

---

## Requirements

- [.NET 6 SDK](https://dotnet.microsoft.com/download/dotnet/6.0)
- [EPPlus](https://www.nuget.org/packages/EPPlus) (bundled via NuGet). **Check [licensing](https://epplussoftware.com/developers/licensenotice)** for commercial products.

Default publish profile targets **self-contained `win-x64`**; use `dotnet publish -r <RID>` for other platforms.

---

## Quick start

1. Add table `.xlsx` files under **`excels/`** and enums under **`excels/<enumPath>/`**.
2. Adjust **`config/settings.json`** (`outputPaths`, `codeGeneration.namespace`, toggles).
3. Execute:

   ```bash
   dotnet build GameDataConfigTool.sln
   dotnet run --project GameDataConfigTool.csproj
   ```

   On Windows, **`build.bat`** builds and runs (expects at least one `.xlsx` in `excels/`).

4. **`dotnet run -- --help`** — CLI help.

Sample paths in `settings.json` point at a sibling Unity tree (`../Assets/...`); change them to match your project.

---

## Configuration (`config/settings.json`)

| Key | Purpose |
|-----|---------|
| `excelPath` | Folder containing table workbooks (default `excels/`). |
| `enumPath` | Subfolder under `excelPath` for enum workbooks, or an absolute path. |
| `cleanOutputsBeforeGenerate` | If `true`, clears each **enabled** output directory before writing (always keeps an `ext/` subfolder). Default in code: `true` if omitted. |
| `outputPaths.json` / `binary` / `code` | Output roots; may use `../` relative to the tool root. |
| `generators` | `enableJson`, `enableBinary`, `enableCode`. |
| `codeGeneration` | `namespace`, `language`, `generateEnum`. |
| `validation.enableTypeCheck` | Type / range checks on non-empty cells. |
| `validation.enforceNonNullableColumns` | Enforce non-empty cells for columns without `|nullable`. |
| `logging` | `level`, `outputToFile`. |

---

## Unity (generated code)

- **`XXXData`** — Row shape.
- **`XXXConfig` : `BaseConfigData<XXXData>`** — `Initialize()` loads `StreamingAssets/ConfigData/<Table>.data`; optional **`PostInitialize`** in `ext/`.
- **`GameDataManager`** — Calls each config’s `Initialize` (reflection on non-WebGL; async path on WebGL player).
- Data is held in **static** collections until **`Reload`** or domain unload; there is no automatic unload API.

---

## Source layout

```
src/Program.cs           Entry
src/ExcelParser.cs       Sheets → model; defaults for nullable cells
src/DataValidator.cs     Rules
src/OutputGenerator.cs   Writers + C# templates
src/Configuration.cs     Settings
src/FieldValueDefaults.cs Shared default strings (e.g. datetime min)
src/Logger.cs
config/settings.json
```

---

## License

Ship a **`LICENSE`** if you redistribute the tool. Honor **EPPlus** and other NuGet package terms.
