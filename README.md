# GameDataConfigTool

**.NET 6** CLI tool: **Excel → validated binary / JSON / C# or TypeScript** — strongly-typed, zero-reflection runtime code.

**Author:** [Yuankun Huang](https://github.com/YuankunHuang)

---

## Pipeline

| Stage | What happens |
|-------|-------------|
| **Parse** | Read `.xlsx` tables and enums via EPPlus; values are strongly-typed at parse time (no string round-trips). |
| **Validate** | Types, ranges, non-nullable, enums, foreign keys, duplicate IDs, field names. Fails fast with non-zero exit. |
| **Generate** | Binary `.data`, optional JSON, and **C# or TypeScript** code from `.template` files. |

---

## Project structure

```
src/
  Program.cs                  Entry point
  Core/
    ToolConfig.cs              Settings model + loader + validation
    Log.cs                     Minimal logger (file handle properly disposed)
    Naming.cs                  PascalCase / camelCase utilities
  Models/
    FieldType.cs               Type enum
    FieldTypeInfo.cs           Parsed type declaration (record)
    Field.cs                   Column definition
    CellValue.cs               Strongly-typed cell (struct, no boxing for primitives)
    DataRow.cs / DataTable.cs  Row and table models
    EnumType.cs / EnumValue.cs Enum models
    GameData.cs                Top-level container
  Parsing/
    TypeParser.cs              Column type syntax parser
    ExcelParser.cs             Excel → GameData (synchronous, honest API)
  Validation/
    DataValidator.cs           All validation rules
  Generation/
    TemplateEngine.cs          Loads .template files, placeholder replacement
    JsonGenerator.cs           GameData → .json
    BinaryGenerator.cs         GameData → .data + index.json
    CSharpCodeGenerator.cs     GameData → .cs (direct assignment, zero reflection)
    TypeScriptCodeGenerator.cs GameData → .ts (interfaces + typed accessors)
templates/
  csharp/                      C# code generation templates
  typescript/                  TypeScript code generation templates
config/
  settings.json                Tool configuration
```

---

## Key improvements over v1

| Before | After |
|--------|-------|
| 1041-line `OutputGenerator` with `AppendLine` string concatenation | Template files + dedicated generators per language |
| Runtime reflection (`PropertyInfo.SetValue`) in generated loader | Direct property assignment in generated code — **zero reflection** |
| Fake async (`Task.FromResult`) | Honest synchronous API |
| `List<string>` data model, triple `TryParse` | `CellValue` struct with typed value at parse time |
| 7-element `ValueTuple` from `ParseFieldType` | `FieldTypeInfo` record |
| `Logger` file handle leak | `Log.Dispose()` in `finally` block |
| C# only | **C# + TypeScript** via `codeGeneration.language` |

---

## Quick start

1. Place this tool directory inside your project root:

```
MyProject/                          ← project root (auto-detected)
  GameDataConfig/                   ← this tool
    excels/
      BrushConfig.xlsx
      EnumTypes/BrushType.xlsx
    config/
      profile.json                  ← { "active": "cocos" }
      cocos.json                    ← TS + JSON pipeline
      unity.json                    ← C# + binary pipeline
    templates/
  assets/                           ← project assets (Cocos / Unity)
```

2. `outputPaths` in config are **relative to the project root** (parent of this tool dir), not the tool dir itself. So `"assets/resources/data/"` resolves to `MyProject/assets/resources/data/`.

3. Run from the tool directory:

```bash
cd GameDataConfig
dotnet run                          # uses active profile from profile.json
dotnet run -- --profile unity       # override: use config/unity.json
dotnet run -- --profile cocos       # override: use config/cocos.json
```

---

## Excel conventions

### Header format

`FieldName|Type` or `FieldName|Type|nullable`

- **Column A** must be `id|int` (non-nullable primary key).
- Cell **comments** become doc comments on generated properties.

### Type syntax

| Pattern | Meaning |
|---------|---------|
| `int`, `long`, `float`, `string`, `bool`, `datetime` | Scalars |
| `enum(EnumTypeName)` | Enum (cell = number or member name) |
| `int^Range(min,max)` | Range check `[min, max)` |
| `int^id(RefTable)` | Foreign key to another table's `id` |

---

## Profile system

The tool uses a two-layer config:

1. **`config/profile.json`** — selects which pipeline is active:
   ```json
   { "active": "cocos" }
   ```
2. **`config/<name>.json`** — full pipeline config (one per engine/project).

Profile resolution order:
1. `--profile <name>` CLI argument (highest priority)
2. `profile.json` → `active` field
3. Fallback → `config/settings.json`

## Pipeline config options

| Key | Purpose |
|-----|---------|
| `excelPath` | Table workbooks folder (default `excels/`) |
| `enumPath` | Enum subfolder under `excelPath` |
| `cleanBeforeGenerate` | Clear output dirs before writing (preserves `ext/`) |
| `outputPaths.json/binary/code` | Output directories (relative to tool root) |
| `generators.enableJson/Binary/Code` | Toggle each output |
| `codeGeneration.namespace` | C# namespace (required for C#, ignored for TS) |
| `codeGeneration.language` | `csharp` or `typescript` |
| `validation.*` | Type checks, non-nullable enforcement |

---

## Generated code

### C# (zero-reflection loader)

```csharp
// Generated: direct assignment, no PropertyInfo.SetValue
var item = new MonsterData
{
    Id   = r.ReadInt32(),
    Name = r.ReadString(),
    Hp   = r.ReadSingle(),
};
```

### TypeScript

```typescript
export interface MonsterData {
    readonly id: number;
    readonly name: string;
    readonly hp: number;
}

export const MonsterConfig = {
    initialize(rows: MonsterData[]): void { ... },
    getById(id: number): MonsterData | undefined { ... },
    getAll(): readonly MonsterData[] { ... },
};
```

---

## License

Honor [EPPlus licensing](https://epplussoftware.com/developers/licensenotice) for commercial use.
