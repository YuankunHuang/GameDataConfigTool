using OfficeOpenXml;
using GameDataTool.Core.Logging;
using static GameDataTool.FieldValueDefaults;

namespace GameDataTool.Parsers;

public class ExcelParser
{
    private List<EnumType> _enums = new();

    public Task<GameData> ParseAsync(string excelPath, string enumPath)
    {
        Logger.Info($"Parsing Excel files, path: {excelPath}");

        var data = new GameData();

        try
        {
            string enumDir = Path.IsPathRooted(enumPath) ? enumPath : Path.Combine(excelPath, enumPath);
            ParseEnumTypes(data, enumDir);
            _enums = data.Enums;

            ParseDataTables(data, excelPath);
        }
        catch (Exception ex)
        {
            Logger.Error($"Excel parsing failed: {ex.Message}");
            throw;
        }

        return Task.FromResult(data);
    }

    private void ParseEnumTypes(GameData data, string enumPath)
    {
        if (!Directory.Exists(enumPath))
        {
            Logger.Warning($"Enum directory not found: {enumPath}");
            return;
        }

        var enumFiles = Directory.GetFiles(enumPath, "*.xlsx");
        Logger.Info($"Found {enumFiles.Length} enum file(s)");

        foreach (var file in enumFiles)
        {
            var enumType = ParseEnumFile(file);
            data.Enums.Add(enumType);
        }
    }

    private EnumType ParseEnumFile(string filePath)
    {
        var enumName = Path.GetFileNameWithoutExtension(filePath);
        var enumType = new EnumType { Name = enumName };

        Logger.Info($"Parsing {enumName}");

        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        using var package = new ExcelPackage(new FileInfo(filePath));
        var worksheet = package.Workbook.Worksheets[0];

        if (worksheet.Dimension == null || worksheet.Dimension.Rows < 2)
        {
            throw new InvalidDataException(
                $"Enum file {Path.GetFileName(filePath)} does not have enough rows, at least 2 required (header + data)");
        }

        for (int row = 2; row <= worksheet.Dimension.Rows; row++)
        {
            var nameCell = worksheet.Cells[row, 1];
            var valueCell = worksheet.Cells[row, 2];
            var commentCell = worksheet.Cells[row, 3];

            if (nameCell.Value != null && valueCell.Value != null)
            {
                enumType.Values.Add(new EnumValue
                {
                    Name = nameCell.Value.ToString() ?? "",
                    Value = Convert.ToInt32(valueCell.Value),
                    Description = commentCell.Value?.ToString() ?? ""
                });
            }
        }

        if (enumType.Values.Count == 0)
        {
            throw new InvalidDataException(
                $"Enum file {Path.GetFileName(filePath)} does not contain any valid enum values");
        }

        return enumType;
    }

    private void ParseDataTables(GameData data, string excelPath)
    {
        var excelFiles = Directory.GetFiles(excelPath, "*.xlsx", SearchOption.TopDirectoryOnly);
        Logger.Info($"Found {excelFiles.Length} data table file(s)");

        foreach (var file in excelFiles)
        {
            var table = ParseDataTable(file);
            data.Tables.Add(table);
        }
    }

    private DataTable ParseDataTable(string filePath)
    {
        var tableName = Path.GetFileNameWithoutExtension(filePath);
        var table = new DataTable { Name = tableName };

        Logger.Info($"Parsing {tableName}");

        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        using var package = new ExcelPackage(new FileInfo(filePath));
        var worksheet = package.Workbook.Worksheets[0];

        if (worksheet.Dimension == null || worksheet.Dimension.Rows < 2)
        {
            throw new InvalidDataException(
                $"Data table {Path.GetFileName(filePath)} does not have enough rows, at least 2 required (header + data)");
        }

        ParseFieldsWithComments(table, worksheet);
        ValidateTableIdColumn(table);
        ParseDataRowsWithEPPlus(table, worksheet);

        if (table.Rows.Count == 0)
        {
            throw new InvalidDataException(
                $"Data table {Path.GetFileName(filePath)} does not contain any valid data rows");
        }

        return table;
    }

    /// <summary>First column must be <c>id|int</c> (not nullable) so runtime <c>GetById</c> indexes on <c>Id</c>.</summary>
    private static void ValidateTableIdColumn(DataTable table)
    {
        if (table.Fields.Count == 0)
        {
            throw new InvalidDataException($"Table '{table.Name}' has no columns.");
        }

        var id = table.Fields[0];
        if (!id.Name.Equals("id", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                $"Table '{table.Name}': first column must be id|int (found '{id.Name}'). Move the primary key to column A.");
        }

        if (id.Type != FieldType.Int)
        {
            throw new InvalidDataException(
                $"Table '{table.Name}': first column must be id|int (found type '{id.RawType}').");
        }

        if (id.Nullable)
        {
            throw new InvalidDataException($"Table '{table.Name}': id cannot be marked nullable.");
        }
    }

    private void ParseFieldsWithComments(DataTable table, ExcelWorksheet worksheet)
    {
        for (int col = 1; col <= worksheet.Dimension.Columns; col++)
        {
            var cell = worksheet.Cells[1, col];
            var cellText = cell.Text.Trim();

            if (string.IsNullOrWhiteSpace(cellText) || !cellText.Contains('|'))
            {
                throw new InvalidDataException(
                    $"Table '{table.Name}' header column {col}: expected FieldName|Type or FieldName|Type|nullable");
            }

            var parts = cellText.Split('|', StringSplitOptions.TrimEntries);
            if (parts.Length < 2 || parts.Length > 3)
            {
                throw new InvalidDataException(
                    $"Table '{table.Name}' header column {col}: use FieldName|Type or FieldName|Type|flags — got {parts.Length} segment(s).");
            }

            var fieldName = parts[0];
            var typeStr = parts[1];
            var nullable = ParseNullableFlag(table.Name, col, parts.Length == 3 ? parts[2] : null);

            var comment = cell.Comment?.Text ?? string.Empty;
            var (fieldType, rawType, refTable, refField, enumType, rangeMin, rangeMax) = ParseFieldType(typeStr, table.Name, col);

            if (fieldType == FieldType.Enum && string.IsNullOrWhiteSpace(enumType))
            {
                throw new InvalidDataException(
                    $"Table '{table.Name}' header column {col}: enum() must name a type, e.g. enum(ItemRarity).");
            }

            table.Fields.Add(new Field
            {
                Name = fieldName,
                Type = fieldType,
                RawType = rawType,
                ReferenceTable = refTable,
                ReferenceField = refField,
                EnumType = enumType,
                RangeMin = rangeMin,
                RangeMax = rangeMax,
                Description = comment,
                Nullable = nullable
            });
        }
    }

    private static bool ParseNullableFlag(string tableName, int col, string? flagsSegment)
    {
        if (string.IsNullOrEmpty(flagsSegment))
            return false;

        var nullable = false;
        foreach (var flag in flagsSegment.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (flag.Equals("nullable", StringComparison.OrdinalIgnoreCase))
                nullable = true;
            else
            {
                throw new InvalidDataException(
                    $"Table '{tableName}' header column {col}: unknown flag '{flag}'. Only 'nullable' is supported.");
            }
        }

        return nullable;
    }

    /// <summary>Canonical string used when a nullable cell is left blank (matches binary / runtime defaults).</summary>
    public static string DefaultCellString(Field field)
    {
        return field.Type switch
        {
            FieldType.String => "",
            FieldType.Int or FieldType.Long or FieldType.Float or FieldType.Enum => "0",
            FieldType.Bool => "false",
            FieldType.DateTime => DateTimeMinValueIso,
            _ => ""
        };
    }

    private void ParseDataRowsWithEPPlus(DataTable table, ExcelWorksheet worksheet)
    {
        for (int row = 2; row <= worksheet.Dimension.Rows; row++)
        {
            var dataRow = new DataRow();

            for (int col = 1; col <= table.Fields.Count; col++)
            {
                var field = table.Fields[col - 1];
                var cell = worksheet.Cells[row, col];
                var value = cell.Value;
                var strValue = (value?.ToString() ?? "").Trim();

                if (string.IsNullOrEmpty(strValue))
                {
                    if (field.Nullable)
                        strValue = DefaultCellString(field);
                    dataRow.Values.Add(strValue);
                    continue;
                }

                if (field.Type == FieldType.Enum)
                {
                    if (!int.TryParse(strValue, out _))
                    {
                        var enumType = _enums.FirstOrDefault(e =>
                            e.Name.Equals(field.EnumType, StringComparison.OrdinalIgnoreCase));
                        if (enumType == null)
                        {
                            throw new InvalidDataException(
                                $"Enum type '{field.EnumType}' not found for table '{table.Name}', Excel row {row}, column {col}");
                        }

                        var found = enumType.Values.FirstOrDefault(ev =>
                            ev.Name.Equals(strValue, StringComparison.OrdinalIgnoreCase));
                        if (found == null)
                        {
                            throw new InvalidDataException(
                                $"Invalid enum name in table '{table.Name}', Excel row {row}, column {col}: '{strValue}' is not defined in enum '{field.EnumType}'");
                        }

                        strValue = found.Value.ToString();
                    }
                }

                if (field.Type == FieldType.DateTime)
                {
                    if (DateTime.TryParseExact(strValue, "yyyy-MM-dd HH:mm:ss", null,
                            System.Globalization.DateTimeStyles.None, out var dateTimeValue))
                    {
                        strValue = dateTimeValue.ToString("yyyy-MM-dd HH:mm:ss");
                    }
                    else
                    {
                        throw new InvalidDataException(
                            $"Invalid datetime in table '{table.Name}', Excel row {row}, column {col}: '{strValue}'. Expected: yyyy-MM-dd HH:mm:ss");
                    }
                }

                dataRow.Values.Add(strValue);
            }

            if (dataRow.Values.Any(v => !string.IsNullOrWhiteSpace(v)))
                table.Rows.Add(dataRow);
        }
    }

    private (FieldType fieldType, string rawType, string? refTable, string? refField, string? enumType, int? rangeMin, int? rangeMax)
        ParseFieldType(string typeStr, string tableName, int col)
    {
        string? refTable = null;
        string? refField = null;
        string? enumType = null;
        int? rangeMin = null;
        int? rangeMax = null;
        var rawType = typeStr;
        var lower = typeStr.Trim().ToLowerInvariant();

        if (lower.StartsWith("enum(", StringComparison.Ordinal) && lower.EndsWith(')'))
        {
            enumType = typeStr.Substring(5, typeStr.Length - 6).Trim();
            return (FieldType.Enum, rawType, null, null, enumType, null, null);
        }

        if (lower.Contains("^range(", StringComparison.OrdinalIgnoreCase))
        {
            var typeMain = typeStr.Split('^')[0].Trim();
            var rangeInfo = typeStr[typeStr.IndexOf('^', StringComparison.Ordinal)..];
            var rangeStart = rangeInfo.IndexOf('(', StringComparison.Ordinal);
            var rangeEnd = rangeInfo.IndexOf(')', StringComparison.Ordinal);
            if (rangeStart > 0 && rangeEnd > rangeStart)
            {
                var rangeContent = rangeInfo.Substring(rangeStart + 1, rangeEnd - rangeStart - 1);
                var rangeParts = rangeContent.Split(',');
                if (rangeParts.Length == 2 &&
                    int.TryParse(rangeParts[0].Trim(), out var min) &&
                    int.TryParse(rangeParts[1].Trim(), out var max))
                {
                    rangeMin = min;
                    rangeMax = max;
                }
            }

            var fieldType = typeMain.ToLowerInvariant() switch
            {
                "int" or "integer" => FieldType.Int,
                "long" => FieldType.Long,
                "float" or "double" => FieldType.Float,
                _ => FieldType.Int
            };
            return (fieldType, rawType, null, null, null, rangeMin, rangeMax);
        }

        if (lower.Contains("^id(", StringComparison.OrdinalIgnoreCase))
        {
            var typeMain = typeStr.Split('^')[0].Trim();
            var refInfo = typeStr[typeStr.IndexOf('^', StringComparison.Ordinal)..];
            var refFieldStart = refInfo.IndexOf('(', StringComparison.Ordinal);
            var refFieldEnd = refInfo.IndexOf(')', StringComparison.Ordinal);
            if (refFieldStart > 0 && refFieldEnd > refFieldStart)
            {
                refField = refInfo.Substring(0, refFieldStart);
                refTable = refInfo.Substring(refFieldStart + 1, refFieldEnd - refFieldStart - 1);
            }

            var fieldType = typeMain.ToLowerInvariant() switch
            {
                "int" or "integer" => FieldType.Int,
                "long" => FieldType.Long,
                "float" or "double" => FieldType.Float,
                "string" or "text" => FieldType.String,
                "bool" or "boolean" => FieldType.Bool,
                "datetime" or "date" => FieldType.DateTime,
                _ => FieldType.String
            };
            return (fieldType, rawType, refTable, refField, null, null, null);
        }

        var type = lower switch
        {
            "int" or "integer" => FieldType.Int,
            "long" => FieldType.Long,
            "float" or "double" => FieldType.Float,
            "string" or "text" => FieldType.String,
            "bool" or "boolean" => FieldType.Bool,
            "datetime" or "date" => FieldType.DateTime,
            _ => throw new InvalidDataException(
                $"Table '{tableName}' header column {col}: unknown type '{typeStr}'. Use int, long, float, string, bool, datetime, enum(Name), int^id(Table), int^Range(min,max).")
        };
        return (type, rawType, null, null, null, null, null);
    }
}

public class GameData
{
    public List<DataTable> Tables { get; set; } = new();
    public List<EnumType> Enums { get; set; } = new();
}

public class DataTable
{
    public string Name { get; set; } = string.Empty;
    public List<Field> Fields { get; set; } = new();
    public List<DataRow> Rows { get; set; } = new();
}

public class Field
{
    public string Name { get; set; } = string.Empty;
    public FieldType Type { get; set; }
    public string Description { get; set; } = string.Empty;
    public string RawType { get; set; } = string.Empty;
    public string? ReferenceTable { get; set; }
    public string? ReferenceField { get; set; }
    public string? EnumType { get; set; }
    public int? RangeMin { get; set; }
    public int? RangeMax { get; set; }

    /// <summary>When true, an empty cell is allowed and is normalized to the type default before validation and export.</summary>
    public bool Nullable { get; set; }
}

public class DataRow
{
    public List<string> Values { get; set; } = new();
}

public class EnumType
{
    public string Name { get; set; } = string.Empty;
    public List<EnumValue> Values { get; set; } = new();
}

public class EnumValue
{
    public string Name { get; set; } = string.Empty;
    public int Value { get; set; }
    public string Description { get; set; } = string.Empty;
}

public enum FieldType
{
    String,
    Int,
    Long,
    Float,
    Bool,
    DateTime,
    Enum
}
