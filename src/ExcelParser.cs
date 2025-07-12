using ExcelDataReader;
using System.Data;
using GameDataTool.Core.Logging;

namespace GameDataTool.Parsers;

public class ExcelParser
{
    public async Task<GameData> ParseAsync(string excelPath, string enumPath)
    {
        Logger.Info($"Parsing Excel files, path: {excelPath}");
        
        var data = new GameData();
        
        // Register encoding provider
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

        try
        {
            // Parse enum types
            string enumDir = Path.IsPathRooted(enumPath) ? enumPath : Path.Combine(excelPath, enumPath);
            ParseEnumTypes(data, enumDir);
            
            // Parse data tables
            ParseDataTables(data, excelPath);

            Logger.Info($"Excel parsing complete: {data.Tables.Count} data tables, {data.Enums.Count} enum types");
        }
        catch (Exception ex)
        {
            Logger.Error($"Excel parsing failed: {ex.Message}");
            throw;
        }
        
        return data;
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
            try
            {
                var enumType = ParseEnumFile(file);
                data.Enums.Add(enumType);
                Logger.Debug($"Parsed enum file: {Path.GetFileName(file)}");
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to parse enum file {file}: {ex.Message}");
                Console.WriteLine($"⚠️  Warning: Failed to parse enum file {Path.GetFileName(file)}: {ex.Message}");
            }
        }
    }

    private EnumType ParseEnumFile(string filePath)
    {
        var enumType = new EnumType
        {
            Name = Path.GetFileNameWithoutExtension(filePath)
        };

        using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read);
        using var reader = ExcelReaderFactory.CreateReader(stream);
        
        var dataSet = reader.AsDataSet();
        var table = dataSet.Tables[0];

        if (table.Rows.Count < 2)
        {
            throw new InvalidDataException($"Enum file {Path.GetFileName(filePath)} does not have enough rows, at least 2 required (header + data)");
        }

        for (int row = 1; row < table.Rows.Count; row++) // Skip header
        {
            var rowData = table.Rows[row];
            if (rowData[0] != DBNull.Value && rowData[1] != DBNull.Value)
            {
                var enumValue = new EnumValue
                {
                    Name = rowData[0].ToString() ?? "",
                    Value = Convert.ToInt32(rowData[1]),
                    Description = rowData[2]?.ToString() ?? ""
                };
                enumType.Values.Add(enumValue);
            }
        }

        if (enumType.Values.Count == 0)
        {
            throw new InvalidDataException($"Enum file {Path.GetFileName(filePath)} does not contain any valid enum values");
        }

        return enumType;
    }

    private void ParseDataTables(GameData data, string excelPath)
    {
        // Only read .xlsx files in the main directory, do not recurse
        var excelFiles = Directory.GetFiles(excelPath, "*.xlsx", SearchOption.TopDirectoryOnly);
        Logger.Info($"Found {excelFiles.Length} data table file(s)");

        foreach (var file in excelFiles)
        {
            try
            {
                var table = ParseDataTable(file);
                data.Tables.Add(table);
                Logger.Debug($"Parsed data table: {Path.GetFileName(file)}");
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to parse data table {file}: {ex.Message}");
                Console.WriteLine($"⚠️  Warning: Failed to parse data table {Path.GetFileName(file)}: {ex.Message}");
            }
        }
    }

    private DataTable ParseDataTable(string filePath)
    {
        var table = new DataTable
        {
            Name = Path.GetFileNameWithoutExtension(filePath)
        };

        using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read);
        using var reader = ExcelReaderFactory.CreateReader(stream);
        
        var dataSet = reader.AsDataSet();
        var excelTable = dataSet.Tables[0];

        if (excelTable.Rows.Count < 4)
        {
            throw new InvalidDataException($"Data table {Path.GetFileName(filePath)} does not have enough rows, at least 4 required (field name + type + description + data)");
        }

        // Parse field definitions (first 3 rows)
        ParseFields(table, excelTable);
        
        // Parse data rows
        ParseDataRows(table, excelTable);

        if (table.Rows.Count == 0)
        {
            throw new InvalidDataException($"Data table {Path.GetFileName(filePath)} does not contain any valid data rows");
        }

        return table;
    }

    private void ParseFields(DataTable table, System.Data.DataTable excelTable)
    {
        // First row: field names
        var fieldNames = new List<string>();
        for (int col = 0; col < excelTable.Columns.Count; col++)
        {
            var value = excelTable.Rows[0][col];
            var fieldName = value?.ToString() ?? $"Field{col}";
            
            if (string.IsNullOrWhiteSpace(fieldName))
            {
                fieldName = $"Field{col}";
            }
            
            fieldNames.Add(fieldName);
        }

        // Second row: field types
        var fieldTypes = new List<string>();
        for (int col = 0; col < excelTable.Columns.Count; col++)
        {
            var value = excelTable.Rows[1][col];
            var fieldType = value?.ToString() ?? "string";
            fieldTypes.Add(fieldType);
        }

        // Third row: field descriptions
        var fieldDescriptions = new List<string>();
        for (int col = 0; col < excelTable.Columns.Count; col++)
        {
            var value = excelTable.Rows[2][col];
            fieldDescriptions.Add(value?.ToString() ?? "");
        }

        // Create field definitions
        for (int i = 0; i < fieldNames.Count; i++)
        {
            var field = new Field
            {
                Name = fieldNames[i],
                Type = ParseFieldType(fieldTypes[i]),
                Description = fieldDescriptions[i]
            };
            table.Fields.Add(field);
        }
    }

    private FieldType ParseFieldType(string typeStr)
    {
        if (string.IsNullOrWhiteSpace(typeStr))
            return FieldType.String;

        return typeStr.ToLower().Trim() switch
        {
            "int" or "integer" => FieldType.Int,
            "long" => FieldType.Long,
            "float" or "double" => FieldType.Float,
            "string" or "text" => FieldType.String,
            "bool" or "boolean" => FieldType.Bool,
            "enum" => FieldType.Enum,
            _ => FieldType.String
        };
    }

    private void ParseDataRows(DataTable table, System.Data.DataTable excelTable)
    {
        for (int row = 3; row < excelTable.Rows.Count; row++) // Start from row 4 (skip first 3 rows)
        {
            var dataRow = new DataRow();
            var excelRow = excelTable.Rows[row];

            for (int col = 0; col < table.Fields.Count && col < excelRow.ItemArray.Length; col++)
            {
                var value = excelRow[col];
                if (value != DBNull.Value)
                {
                    dataRow.Values.Add(value.ToString() ?? "");
                }
                else
                {
                    dataRow.Values.Add("");
                }
            }

            // Only add non-empty rows
            if (dataRow.Values.Any(v => !string.IsNullOrWhiteSpace(v)))
            {
                table.Rows.Add(dataRow);
            }
        }
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
    Enum
} 