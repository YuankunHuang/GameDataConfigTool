using GameDataTool.Core;
using GameDataTool.Models;
using OfficeOpenXml;

namespace GameDataTool.Parsing;

public sealed class ExcelParser
{
    static ExcelParser()
    {
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
    }

    public GameData Parse(string excelPath, string enumPath)
    {
        var data = new GameData();

        var enumDir = Path.IsPathRooted(enumPath) ? enumPath : Path.Combine(excelPath, enumPath);
        ParseEnumTypes(data, enumDir);
        ParseDataTables(data, excelPath);

        return data;
    }

    // ─── Enums ──────────────────────────────────────────────

    private static void ParseEnumTypes(GameData data, string enumDir)
    {
        if (!Directory.Exists(enumDir)) { Log.Warn($"Enum dir not found: {enumDir}"); return; }

        foreach (var file in Directory.GetFiles(enumDir, "*.xlsx"))
        {
            var et = new EnumType { Name = Path.GetFileNameWithoutExtension(file) };
            using var pkg = new ExcelPackage(new FileInfo(file));
            var ws = pkg.Workbook.Worksheets[0];

            RequireMinRows(ws, file, 2);

            for (var row = 2; row <= ws.Dimension.Rows; row++)
            {
                var name = ws.Cells[row, 1].Value?.ToString();
                var val  = ws.Cells[row, 2].Value;
                if (name is null || val is null) continue;
                et.Values.Add(new EnumValue(name, Convert.ToInt32(val), ws.Cells[row, 3].Value?.ToString() ?? ""));
            }

            if (et.Values.Count == 0)
                throw new InvalidDataException($"Enum '{et.Name}' has no values.");

            data.Enums.Add(et);
            Log.Info($"  enum  {et.Name} ({et.Values.Count} values)");
        }
    }

    // ─── Data tables ────────────────────────────────────────

    private void ParseDataTables(GameData data, string excelPath)
    {
        foreach (var file in Directory.GetFiles(excelPath, "*.xlsx", SearchOption.TopDirectoryOnly))
        {
            var table = ParseTable(file, data.Enums);
            data.Tables.Add(table);
            Log.Info($"  table {table.Name} ({table.Fields.Count} cols, {table.Rows.Count} rows)");
        }
    }

    private static Models.DataTable ParseTable(string file, List<EnumType> enums)
    {
        var name = Path.GetFileNameWithoutExtension(file);
        var table = new Models.DataTable { Name = name };

        using var pkg = new ExcelPackage(new FileInfo(file));
        var ws = pkg.Workbook.Worksheets[0];

        RequireMinRows(ws, file, 2);

        ParseHeader(table, ws, name);
        ValidateIdColumn(table);
        ParseRows(table, ws, enums);

        if (table.Rows.Count == 0)
            throw new InvalidDataException($"Table '{name}' has no data rows.");

        return table;
    }

    // ─── Header ─────────────────────────────────────────────

    private static void ParseHeader(Models.DataTable table, ExcelWorksheet ws, string tableName)
    {
        for (var col = 1; col <= ws.Dimension.Columns; col++)
        {
            var text = ws.Cells[1, col].Text.Trim();
            if (string.IsNullOrWhiteSpace(text) || !text.Contains('|'))
                throw new InvalidDataException($"[{tableName}] col {col}: header must be FieldName|Type[|nullable]");

            var parts = text.Split('|', StringSplitOptions.TrimEntries);
            if (parts.Length is < 2 or > 3)
                throw new InvalidDataException($"[{tableName}] col {col}: bad header segment count ({parts.Length})");

            var info = TypeParser.Parse(parts[1], tableName, col);
            var nullable = parts.Length == 3 && parts[2].Equals("nullable", StringComparison.OrdinalIgnoreCase);

            if (info.Type == FieldType.Enum && string.IsNullOrWhiteSpace(info.EnumType))
                throw new InvalidDataException($"[{tableName}] col {col}: enum() must name a type.");

            table.Fields.Add(new Field
            {
                Name        = parts[0],
                Type        = info.Type,
                RawType     = info.RawType,
                Description = ws.Cells[1, col].Comment?.Text ?? "",
                RefTable    = info.RefTable,
                RefField    = info.RefField,
                EnumType    = info.EnumType,
                RangeMin    = info.RangeMin,
                RangeMax    = info.RangeMax,
                Nullable    = nullable,
            });
        }
    }

    private static void ValidateIdColumn(Models.DataTable table)
    {
        if (table.Fields.Count == 0)
            throw new InvalidDataException($"Table '{table.Name}' has no columns.");
        var id = table.Fields[0];
        if (!id.Name.Equals("id", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"[{table.Name}] first column must be id|int (found '{id.Name}').");
        if (id.Type != FieldType.Int)
            throw new InvalidDataException($"[{table.Name}] id must be int (found '{id.RawType}').");
        if (id.Nullable)
            throw new InvalidDataException($"[{table.Name}] id cannot be nullable.");
    }

    // ─── Rows ───────────────────────────────────────────────

    private static void ParseRows(Models.DataTable table, ExcelWorksheet ws, List<EnumType> enums)
    {
        for (var row = 2; row <= ws.Dimension.Rows; row++)
        {
            var dr = new Models.DataRow();
            var allEmpty = true;

            for (var col = 0; col < table.Fields.Count; col++)
            {
                var field = table.Fields[col];
                var raw   = (ws.Cells[row, col + 1].Value?.ToString() ?? "").Trim();

                if (string.IsNullOrEmpty(raw))
                {
                    dr.Values.Add(field.Nullable ? CellValue.Empty(field.Type) : CellValue.From("", null));
                    continue;
                }

                allEmpty = false;
                dr.Values.Add(ParseCellValue(field, raw, table.Name, row, col + 1, enums));
            }

            if (!allEmpty)
                table.Rows.Add(dr);
        }
    }

    private static CellValue ParseCellValue(Field field, string raw, string table, int row, int col, List<EnumType> enums)
    {
        switch (field.Type)
        {
            case FieldType.Int:
                return int.TryParse(raw, out var iv) ? CellValue.From(raw, iv)
                    : throw new InvalidDataException($"[{table}] row {row}, col {col}: '{raw}' is not int");

            case FieldType.Long:
                return long.TryParse(raw, out var lv) ? CellValue.From(raw, lv)
                    : throw new InvalidDataException($"[{table}] row {row}, col {col}: '{raw}' is not long");

            case FieldType.Float:
                return float.TryParse(raw, out var fv) ? CellValue.From(raw, fv)
                    : throw new InvalidDataException($"[{table}] row {row}, col {col}: '{raw}' is not float");

            case FieldType.Bool:
                var bv = raw.ToLowerInvariant() is "1" or "true";
                return CellValue.From(raw, bv);

            case FieldType.DateTime:
                return DateTime.TryParseExact(raw, "yyyy-MM-dd HH:mm:ss", null,
                    System.Globalization.DateTimeStyles.None, out var dt)
                    ? CellValue.From(raw, dt)
                    : throw new InvalidDataException($"[{table}] row {row}, col {col}: '{raw}' is not yyyy-MM-dd HH:mm:ss");

            case FieldType.Enum:
                return ResolveEnum(field, raw, table, row, col, enums);

            default:
                return CellValue.From(raw, raw);
        }
    }

    private static CellValue ResolveEnum(Field field, string raw, string table, int row, int col, List<EnumType> enums)
    {
        if (int.TryParse(raw, out var numericVal))
            return CellValue.From(raw, numericVal);

        var et = enums.Find(e => e.Name.Equals(field.EnumType, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidDataException($"[{table}] row {row}, col {col}: enum type '{field.EnumType}' not found");

        var ev = et.Values.Find(v => v.Name.Equals(raw, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidDataException($"[{table}] row {row}, col {col}: '{raw}' not in enum '{field.EnumType}'");

        return CellValue.From(raw, ev.Value);
    }

    // ─── Helpers ────────────────────────────────────────────

    private static void RequireMinRows(ExcelWorksheet ws, string file, int min)
    {
        if (ws.Dimension == null || ws.Dimension.Rows < min)
            throw new InvalidDataException($"{Path.GetFileName(file)}: needs at least {min} rows (header + data)");
    }
}
