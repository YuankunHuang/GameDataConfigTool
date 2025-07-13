using ExcelDataReader;
using OfficeOpenXml;
using System.Data;
using GameDataTool.Core.Logging;

namespace GameDataTool.Parsers;

public class ExcelParser
{
    private List<EnumType> _enums = new List<EnumType>();

    public Task<GameData> ParseAsync(string excelPath, string enumPath)
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
            _enums = data.Enums;
            
            // Parse data tables
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
            try
            {
                var enumType = ParseEnumFile(file);
                data.Enums.Add(enumType);
            }
            catch (Exception ex)
            {
                throw;
            }
        }
    }

    private EnumType ParseEnumFile(string filePath)
    {
        var enumName = Path.GetFileNameWithoutExtension(filePath);
        var enumType = new EnumType
        {
            Name = enumName
        };

        Logger.Info($"Parsing {enumName}");

        // Use EPPlus to read Excel
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        using var package = new ExcelPackage(new FileInfo(filePath));
        var worksheet = package.Workbook.Worksheets[0];

        if (worksheet.Dimension == null || worksheet.Dimension.Rows < 2)
        {
            throw new InvalidDataException($"Enum file {Path.GetFileName(filePath)} does not have enough rows, at least 2 required (header + data)");
        }

        for (int row = 2; row <= worksheet.Dimension.Rows; row++) // Skip header
        {
            var nameCell = worksheet.Cells[row, 1];
            var valueCell = worksheet.Cells[row, 2];
            var commentCell = worksheet.Cells[row, 3];
            
            if (nameCell.Value != null && valueCell.Value != null)
            {
                var enumValue = new EnumValue
                {
                    Name = nameCell.Value.ToString() ?? "",
                    Value = Convert.ToInt32(valueCell.Value),
                    Description = commentCell.Value?.ToString() ?? ""
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
            }
            catch (Exception ex)
            {
                throw;
            }
        }
    }

    private DataTable ParseDataTable(string filePath)
    {
        var tableName = Path.GetFileNameWithoutExtension(filePath);
        var table = new DataTable
        {
            Name = tableName
        };

        Logger.Info($"Parsing {tableName}");

        // Use EPPlus to read Excel with comments
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        using var package = new ExcelPackage(new FileInfo(filePath));
        var worksheet = package.Workbook.Worksheets[0];

        if (worksheet.Dimension == null || worksheet.Dimension.Rows < 2)
        {
            throw new InvalidDataException($"Data table {Path.GetFileName(filePath)} does not have enough rows, at least 2 required (field name + type + data)");
        }

        // Parse field definitions (first row)
        ParseFieldsWithComments(table, worksheet);
        
        // Parse data rows
        ParseDataRowsWithEPPlus(table, worksheet);

        if (table.Rows.Count == 0)
        {
            throw new InvalidDataException($"Data table {Path.GetFileName(filePath)} does not contain any valid data rows");
        }

        return table;
    }

    private void ParseFieldsWithComments(DataTable table, ExcelWorksheet worksheet)
    {
        // Read the first row for field definitions
        for (int col = 1; col <= worksheet.Dimension.Columns; col++)
        {
            var cell = worksheet.Cells[1, col];
            var cellText = cell.Text;
            
            if (string.IsNullOrWhiteSpace(cellText) || !cellText.Contains("|"))
            {
                throw new InvalidDataException($"Header column {col} format error, must be FieldName|Type, e.g., id|int, name|string");
            }
            
            var parts = cellText.Split('|');
            if (parts.Length != 2)
            {
                throw new InvalidDataException($"Header column {col} format error, must be FieldName|Type");
            }
            
            var fieldName = parts[0].Trim();
            var typeStr = parts[1].Trim();
            
            // Get comment from the cell
            var comment = cell.Comment?.Text ?? string.Empty;
            
            // parse types and references
            var (fieldType, rawType, refTable, refField, enumType) = ParseFieldType(typeStr);
            var field = new Field
            {
                Name = fieldName,
                Type = fieldType,
                RawType = rawType,
                ReferenceTable = refTable,
                ReferenceField = refField,
                EnumType = enumType,
                Description = comment
            };
            table.Fields.Add(field);
        }
    }

    private void ParseDataRowsWithEPPlus(DataTable table, ExcelWorksheet worksheet)
    {
        for (int row = 2; row <= worksheet.Dimension.Rows; row++) // data starts from the 2nd row
        {
            var dataRow = new DataRow();
            
            for (int col = 1; col <= table.Fields.Count; col++) // Only read as many columns as we have fields
            {
                var cell = worksheet.Cells[row, col];
                var value = cell.Value;
                string strValue = value != null ? value.ToString() ?? "" : "";

                if (col <= table.Fields.Count)
                {
                    var field = table.Fields[col - 1];
                    
                    if (field.Type == FieldType.Enum && !string.IsNullOrEmpty(strValue))
                    {
                        // first, try to convert to integer
                        if (!int.TryParse(strValue, out var enumValue))
                        {
                            // if no match, then try enum name
                            var enumType = _enums.FirstOrDefault(e => e.Name.Equals(field.EnumType, StringComparison.OrdinalIgnoreCase));
                            if (enumType != null)
                            {
                                var found = enumType.Values.FirstOrDefault(ev => ev.Name.Equals(strValue, StringComparison.OrdinalIgnoreCase));
                                if (found != null)
                                {
                                    strValue = found.Value.ToString();
                                }
                                else
                                {
                                    throw new InvalidDataException($"Invalid enum name in table '{table.Name}', row {row}, column {col}: '{strValue}' is not defined in enum '{field.EnumType}'");
                                }
                            }
                            else
                            {
                                throw new InvalidDataException($"Enum type '{field.EnumType}' not found for table '{table.Name}', row {row}, column {col}");
                            }
                        }
                    }
                    if (field.Type == FieldType.DateTime && !string.IsNullOrEmpty(strValue))
                    {
                        // Parse DateTime using exact format yyyy-MM-dd HH:mm:ss
                        if (DateTime.TryParseExact(strValue, "yyyy-MM-dd HH:mm:ss", null, System.Globalization.DateTimeStyles.None, out var dateTimeValue))
                        {
                            // Standardize to yyyy-MM-dd HH:mm:ss
                            strValue = dateTimeValue.ToString("yyyy-MM-dd HH:mm:ss");
                        }
                        else
                        {
                            throw new InvalidDataException($"Invalid datetime format in table '{table.Name}', row {row}, column {col}: '{strValue}'. Expected format: yyyy-MM-dd HH:mm:ss");
                        }
                    }
                }

                dataRow.Values.Add(strValue);
            }

            // Only add non-empty rows
            if (dataRow.Values.Any(v => !string.IsNullOrWhiteSpace(v)))
            {
                table.Rows.Add(dataRow);
            }
        }
    }

    // supports complex header
    private (FieldType, string, string?, string?, string?) ParseFieldType(string typeStr)
    {
        string? refTable = null;
        string? refField = null;
        string? enumType = null;
        var rawType = typeStr;
        var lower = typeStr.ToLower();
        if (lower.StartsWith("enum(") && lower.EndsWith(")"))
        {
            enumType = typeStr.Substring(5, typeStr.Length - 6);
            return (FieldType.Enum, rawType, null, null, enumType);
        }
        if (lower.Contains("^id("))
        {
            // e.g. int^id(Resource)
            var typeMain = typeStr.Split('^')[0].Trim();
            var refInfo = typeStr.Substring(typeStr.IndexOf('^') + 1);
            var refFieldStart = refInfo.IndexOf('(');
            var refFieldEnd = refInfo.IndexOf(')');
            if (refFieldStart > 0 && refFieldEnd > refFieldStart)
            {
                refField = refInfo.Substring(0, refFieldStart);
                refTable = refInfo.Substring(refFieldStart + 1, refFieldEnd - refFieldStart - 1);
            }
            var fieldType = typeMain.ToLower() switch
            {
                "int" or "integer" => FieldType.Int,
                "long" => FieldType.Long,
                "float" or "double" => FieldType.Float,
                "string" or "text" => FieldType.String,
                "bool" or "boolean" => FieldType.Bool,
                "datetime" or "date" => FieldType.DateTime,
                _ => FieldType.String // default
            };
            return (fieldType, rawType, refTable, refField, null);
        }
        // normal types
        var type = lower switch
        {
            "int" or "integer" => FieldType.Int,
            "long" => FieldType.Long,
            "float" or "double" => FieldType.Float,
            "string" or "text" => FieldType.String,
            "bool" or "boolean" => FieldType.Bool,
            "datetime" or "date" => FieldType.DateTime,
            _ => FieldType.String
        };
        return (type, rawType, null, null, null);
    }

    private void ParseDataRows(DataTable table, System.Data.DataTable excelTable)
    {
        for (int row = 1; row < excelTable.Rows.Count; row++) // data starts from the 2nd row
        {
            var dataRow = new DataRow();
            var excelRow = excelTable.Rows[row];

            for (int col = 0; col < table.Fields.Count && col < excelRow.ItemArray.Length; col++)
            {
                var field = table.Fields[col];
                var value = excelRow[col];
                string strValue = value != DBNull.Value ? value.ToString() ?? "" : "";

                if (field.Type == FieldType.Enum && !string.IsNullOrEmpty(strValue))
                {
                    // first, try to convert to integer
                    if (!int.TryParse(strValue, out var enumValue))
                    {
                        // if no match, then try enum name
                        var enumType = _enums.FirstOrDefault(e => e.Name.Equals(field.EnumType, StringComparison.OrdinalIgnoreCase));
                        if (enumType != null)
                        {
                            var found = enumType.Values.FirstOrDefault(ev => ev.Name.Equals(strValue, StringComparison.OrdinalIgnoreCase));
                            if (found != null)
                            {
                                strValue = found.Value.ToString();
                            }
                            else
                            {
                                throw new InvalidDataException($"Invalid enum name in table '{table.Name}', row {row + 1}, column {col + 1}: '{strValue}' is not defined in enum '{field.EnumType}'");
                            }
                        }
                        else
                        {
                            throw new InvalidDataException($"Enum type '{field.EnumType}' not found for table '{table.Name}', row {row + 1}, column {col + 1}");
                        }
                    }
                }
                if (field.Type == FieldType.DateTime && !string.IsNullOrEmpty(strValue))
                {
                    // Parse DateTime using exact format yyyy-MM-dd HH:mm:ss
                    if (DateTime.TryParseExact(strValue, "yyyy-MM-dd HH:mm:ss", null, System.Globalization.DateTimeStyles.None, out var dateTimeValue))
                    {
                        // Standardize to yyyy-MM-dd HH:mm:ss
                        strValue = dateTimeValue.ToString("yyyy-MM-dd HH:mm:ss");
                    }
                    else
                    {
                        throw new InvalidDataException($"Invalid datetime format in table '{table.Name}', row {row + 1}, column {col + 1}: '{strValue}'. Expected format: yyyy-MM-dd HH:mm:ss");
                    }
                }

                dataRow.Values.Add(strValue);
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
    public string RawType { get; set; } = string.Empty;
    public string? ReferenceTable { get; set; }
    public string? ReferenceField { get; set; }
    public string? EnumType { get; set; }
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