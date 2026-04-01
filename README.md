# GameDataConfigTool

Standalone **.NET 6** CLI that turns **Excel (.xlsx)** game design tables into **validated binary data**, optional **JSON**, and **C#** types for **Unity** (or any consumer of the same outputs). The goal is to keep authoring in spreadsheets while catching errors **before** they reach the engine—ideal for CI and for teams that do not want validation locked inside the Unity Editor.

**Author:** [Yuankun Huang](https://github.com/YuankunHuang)

---

## Design philosophy

1. **Excel as the single source of truth** — Designers work in `.xlsx`; the tool is the gate between sheets and runtime.
2. **Fail fast** — Parse, then **validate**; generation runs only if validation passes (non-zero exit on failure).
3. **Editor-independent pipeline** — Runs with `dotnet run` / published exe so builds and designers’ machines do not require Unity for export.
4. **Multiple outputs from one model** — JSON for inspection and tooling, compact **binary** for runtime, **generated C#** for typed access and `GameDataManager` bootstrap.
5. **Safe customization** — Regenerated code lives in main files; hand-written logic goes in **`ext/`** partials that are **not overwritten** once they exist.

---

## How it works

```
excels/*.xlsx  +  excels/<EnumPath>/*.xlsx
        │
        ▼
   ExcelParser  ──►  GameData (tables + enums)
        │
        ▼
   DataValidator (types, FKs, enums, IDs, …)
        │
        ├── fail ──► stderr + exit code 1
        │
        └── pass
                │
                ├── OutputGenerator → *.data + index.json  (binary)
                ├── OutputGenerator → *.json               (optional)
                └── OutputGenerator → Enums.cs, *Config.cs, BaseConfigData.cs, GameDataManager.cs
```

- **Data sheets**: one `.xlsx` per table in `excels/` (top level only; first worksheet used). Row 1 defines columns (see below). Data starts at row 2.
- **Enum sheets**: under the configured enum subdirectory; columns are name / int value / optional comment (row 1 is header).

### Table shape (primary key)

- **Column A (first column) must be `id|int`**, not nullable. This lines up with generated `XXXData.Id` and runtime **`GetById`** (which indexes on the `id` property).
- Duplicate **`id`** values are rejected at export time.

### Column header format

`FieldName|Type` or `FieldName|Type|nullable`

- **Default (no flag)** — the cell is **non-nullable**: it must contain a value (after trim). Content is validated against **Type** when non-empty.
- **`nullable`** — an empty cell is allowed; it is normalized to the **type default** before validation and binary export (`0` / `false` / `""` / **`DateTime.MinValue`** as `0001-01-01 00:00:00` for `datetime`, `0` for `enum`).
- **Examples**: `id|int` — primary key; `name|string` — must have a value; `subtitle|string|nullable` — blank → default.

### Type mini-language (middle segment)

| Pattern | Meaning |
|--------|---------|
| `int`, `long`, `float`, `string`, `bool`, `datetime` | Scalars (`date` aliases to `datetime`). Unknown spellings are **errors** (fail fast). |
| `enum(EnumTypeName)` | Integer enum; cells may use numbers or symbolic names from the enum workbook. |
| `int^Range(min,max)` | Range check: **inclusive min, exclusive max** (validated for `int` columns). |
| `int^id(RefTable)` | Foreign key: value must exist in `RefTable`’s referenced id column. |

Cell **comments** on the header row become **XML doc comments** on generated C# properties.

**Nullable `datetime` default:** uses `DateTime.MinValue` (`0001-01-01 00:00:00`). Its ticks are inside the .NET range, so binary read/write and generated `ReadValidDateTime` do not overflow. If you need a different sentinel (e.g. for SQL `datetime` min 1753, or “unset” vs “real min date”), use a separate flag column or `int` epoch instead of relying on this default.

---

## Requirements

- [.NET 6 SDK](https://dotnet.microsoft.com/download/dotnet/6.0)
- `.xlsx` files readable by **EPPlus** (see [EPPlus licensing](https://epplussoftware.com/developers/licensenotice) for commercial use).

The `.csproj` is configured for **self-contained `win-x64`** publish by default; other platforms can adjust `RuntimeIdentifier` or publish with `-r`.

---

## Quick start

1. Put table workbooks in **`excels/`** (and enum workbooks under the folder named in `enumPath`, e.g. `excels/EnumTypes/`).
2. Edit **`config/settings.json`** — especially `outputPaths`, `codeGeneration.namespace`, and generator toggles.
3. From the repo root:

   ```bash
   dotnet build GameDataConfigTool.sln
   dotnet run --project GameDataConfigTool.csproj
   ```

   On Windows you can use **`build.bat`** (builds then runs; requires at least one `.xlsx` under `excels/`).

4. Outputs (default Unity-relative layout in sample config):
   - **Binary**: `StreamingAssets/ConfigData/*.data` + `index.json`
   - **Code**: `Assets/Scripts/ConfigData/code/` (+ `ext/` partials)

Always run from the **tool repository root** so `excels/`, `config/settings.json`, and relative `outputPaths` resolve correctly.

Run with `--help` / `-h` for CLI help.

---

## Configuration (`config/settings.json`)

| Section | Role |
|--------|------|
| `excelPath` / `enumPath` | Root folder for tables; enum path relative to `excelPath` unless absolute. |
| `cleanOutputsBeforeGenerate` | When `true`, deletes previous outputs under each **enabled** output path (always keeps an `ext/` folder). |
| `outputPaths` | `json`, `binary`, `code` (supports `../Assets/...` style paths). |
| `generators` | Toggle JSON / binary / code generation. |
| `codeGeneration` | `namespace`, `language`, `generateEnum`. |
| `validation` | `enableTypeCheck`; `enforceNonNullableColumns` — when `true`, columns **without** `|nullable` may not be left blank. |
| `logging` | Level and optional file output under `logs/tool.log`. |

---

## Unity integration (generated side)

- **`BaseConfigData<T>`** — Loads `.data` binaries; **`GetById`** uses the **`id`** property when present (matches first-column `id|int`).
- **`GameDataManager`** — Editor/native: discovers `*Config` types via reflection and calls `Initialize()`. WebGL player: parallel async init per table.
- **`ext/*Config.ext.cs`** — Create once; add partial methods / helpers without losing them on the next export.

Copy or point `outputPaths` at your Unity project’s `StreamingAssets` and script folders.

---

## Repository layout

| Path | Purpose |
|------|---------|
| `src/Program.cs` | Entry: discovery, clean, parse, validate, generate. |
| `src/ExcelParser.cs` | EPPlus parsing, `nullable` defaults, `id|int` schema. |
| `src/DataValidator.cs` | Type checks, non-nullable, enum, duplicate `id`, FKs, field names. |
| `src/OutputGenerator.cs` | JSON, binary, C#, index, `GameDataManager`. |
| `src/Configuration.cs` | `settings.json` load + validation. |
| `src/Logger.cs` | Console/file logging. |
| `config/settings.json` | Tool configuration. |
| `guide/README.md` | Pointer to this file. |

---

## Migrating older workbooks

*(Only if you are upgrading from an older revision of this tool; new projects can skip this section.)*

1. Put **`id|int` in column A** on every data table (no `nullable` on `id`).
2. Replace **`|required`** with nothing (non-nullable is the default).
3. Replace optional columns with **`|nullable`** where blanks are allowed.
4. Remove **`|key`** — duplicate detection is always on **`id`** (column A).
5. Unknown scalar types are **parse errors** (no silent string fallback).
6. In `settings.json`, rename **`enableRequiredFieldCheck`** → **`enforceNonNullableColumns`** (same meaning).

---

## License

Add a `LICENSE` file at the repo root if you distribute the tool; respect **EPPlus** and third-party package licenses.
