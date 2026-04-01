using GameDataTool.Core.Configuration;

namespace GameDataTool.Parsers;

/// <summary>
/// Validation rules and Excel coordinates (row 1 = header, row 2+ = data).
/// </summary>
public class DataValidator
{
    private static int ExcelDataRow(int zeroBasedRowIndex) => zeroBasedRowIndex + 2;

    public Task<ValidationResult> ValidateAsync(GameData data, Validation config)
    {
        var result = new ValidationResult();

        Console.WriteLine();
        Console.WriteLine("Start data validation...");
        Console.WriteLine();

        if (config.EnableTypeCheck)
            ValidateTypes(data, result);

        if (config.EnforceNonNullableColumns)
            ValidateNonNullableFields(data, result);

        ValidateEnumReferences(data, result);
        ValidateDuplicateIds(data, result);
        ValidateFieldNames(data, result);
        ValidateForeignKeyReferences(data, result);

        return Task.FromResult(result);
    }

    private void ValidateTypes(GameData data, ValidationResult result)
    {
        foreach (var table in data.Tables)
        {
            for (int rowIndex = 0; rowIndex < table.Rows.Count; rowIndex++)
            {
                var row = table.Rows[rowIndex];
                for (int colIndex = 0; colIndex < table.Fields.Count && colIndex < row.Values.Count; colIndex++)
                {
                    var field = table.Fields[colIndex];
                    var value = row.Values[colIndex];

                    if (string.IsNullOrEmpty(value))
                        continue;

                    var (isValid, errorMessage) = ValidateFieldTypeWithMessage(field, value);
                    if (!isValid)
                    {
                        result.Errors.Add(
                            $"[{table.Name}] Excel row {ExcelDataRow(rowIndex)}, col {colIndex + 1} ({field.Name}): {errorMessage}");
                    }
                }
            }
        }
    }

    private static (bool isValid, string errorMessage) ValidateFieldTypeWithMessage(Field field, string value)
    {
        var typeValid = field.Type switch
        {
            FieldType.Int => int.TryParse(value, out _),
            FieldType.Long => long.TryParse(value, out _),
            FieldType.Float => float.TryParse(value, out _),
            FieldType.Bool => value.Trim().ToLowerInvariant() is "0" or "1" or "true" or "false",
            FieldType.String => true,
            FieldType.Enum => int.TryParse(value, out _),
            FieldType.DateTime => DateTime.TryParseExact(value, "yyyy-MM-dd HH:mm:ss", null,
                System.Globalization.DateTimeStyles.None, out _),
            _ => true
        };

        if (!typeValid)
            return (false, $"value '{value}' does not match type {field.Type}");

        if (field.Type == FieldType.Int && field.RangeMin.HasValue && field.RangeMax.HasValue)
        {
            if (!int.TryParse(value, out var intValue))
                return (false, $"value '{value}' is not a valid integer for range validation");

            if (intValue < field.RangeMin.Value || intValue >= field.RangeMax.Value)
            {
                return (false,
                    $"value '{value}' is out of range [{field.RangeMin}, {field.RangeMax}) (inclusive min, exclusive max)");
            }
        }

        return (true, string.Empty);
    }

    /// <summary>
    /// Non-nullable columns must have a non-whitespace value (id is always non-nullable by schema).
    /// </summary>
    private void ValidateNonNullableFields(GameData data, ValidationResult result)
    {
        foreach (var table in data.Tables)
        {
            for (int rowIndex = 0; rowIndex < table.Rows.Count; rowIndex++)
            {
                var row = table.Rows[rowIndex];
                for (int colIndex = 0; colIndex < table.Fields.Count && colIndex < row.Values.Count; colIndex++)
                {
                    var field = table.Fields[colIndex];
                    var value = row.Values[colIndex];

                    if (!field.Nullable && string.IsNullOrWhiteSpace(value))
                    {
                        result.Errors.Add(
                            $"[{table.Name}] Excel row {ExcelDataRow(rowIndex)}, col {colIndex + 1} ({field.Name}): non-nullable field is empty (add '|nullable' to the header if this column may be blank)");
                    }
                }
            }
        }
    }

    private void ValidateEnumReferences(GameData data, ValidationResult result)
    {
        foreach (var table in data.Tables)
        {
            for (int fieldIndex = 0; fieldIndex < table.Fields.Count; fieldIndex++)
            {
                var field = table.Fields[fieldIndex];
                if (field.Type != FieldType.Enum)
                    continue;

                if (string.IsNullOrWhiteSpace(field.EnumType))
                {
                    result.Errors.Add($"[{table.Name}] field '{field.Name}': enum column must use enum(TypeName) in the header.");
                    continue;
                }

                var enumType = data.Enums.FirstOrDefault(e =>
                    e.Name.Equals(field.EnumType, StringComparison.OrdinalIgnoreCase));
                if (enumType == null)
                {
                    result.Errors.Add($"[{table.Name}] field '{field.Name}': enum type '{field.EnumType}' is not defined.");
                    continue;
                }

                for (int rowIndex = 0; rowIndex < table.Rows.Count; rowIndex++)
                {
                    var row = table.Rows[rowIndex];
                    if (fieldIndex >= row.Values.Count)
                        continue;

                    var value = row.Values[fieldIndex];
                    if (string.IsNullOrEmpty(value))
                        continue;

                    if (int.TryParse(value, out var enumValue))
                    {
                        if (!enumType.Values.Any(v => v.Value == enumValue))
                        {
                            result.Errors.Add(
                                $"[{table.Name}] Excel row {ExcelDataRow(rowIndex)}, col {fieldIndex + 1}: enum value {enumValue} is not defined in {field.EnumType}");
                        }
                    }
                    else
                    {
                        var found = enumType.Values.FirstOrDefault(ev =>
                            ev.Name.Equals(value, StringComparison.OrdinalIgnoreCase));
                        if (found == null)
                        {
                            result.Errors.Add(
                                $"[{table.Name}] Excel row {ExcelDataRow(rowIndex)}, col {fieldIndex + 1}: enum name '{value}' is not defined in {field.EnumType}");
                        }
                    }
                }
            }
        }
    }

    private void ValidateDuplicateIds(GameData data, ValidationResult result)
    {
        const int idCol = 0;

        foreach (var table in data.Tables)
        {
            if (table.Fields.Count == 0)
                continue;

            var ids = new HashSet<int>();
            for (int rowIndex = 0; rowIndex < table.Rows.Count; rowIndex++)
            {
                var row = table.Rows[rowIndex];
                if (idCol >= row.Values.Count)
                    continue;

                var raw = row.Values[idCol]?.Trim() ?? "";
                if (string.IsNullOrEmpty(raw))
                    continue;

                if (!int.TryParse(raw, out var id))
                {
                    result.Errors.Add(
                        $"[{table.Name}] Excel row {ExcelDataRow(rowIndex)}: id must be int, got '{raw}'");
                    continue;
                }

                if (!ids.Add(id))
                {
                    result.Errors.Add(
                        $"[{table.Name}] Excel row {ExcelDataRow(rowIndex)}: duplicate id {id}");
                }
            }
        }
    }

    private void ValidateFieldNames(GameData data, ValidationResult result)
    {
        foreach (var table in data.Tables)
        {
            var fieldNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < table.Fields.Count; i++)
            {
                var field = table.Fields[i];

                if (string.IsNullOrWhiteSpace(field.Name))
                {
                    result.Errors.Add($"[{table.Name}] header col {i + 1}: field name cannot be empty");
                    continue;
                }

                if (!fieldNames.Add(field.Name))
                {
                    result.Errors.Add($"[{table.Name}] header col {i + 1}: duplicate field name '{field.Name}'");
                }

                if (field.Name.Any(c => !char.IsLetterOrDigit(c) && c != '_'))
                {
                    result.Errors.Add(
                        $"[{table.Name}] header col {i + 1}: field name '{field.Name}' should use only letters, digits, and underscores");
                }
            }
        }
    }

    private void ValidateForeignKeyReferences(GameData data, ValidationResult result)
    {
        foreach (var table in data.Tables)
        {
            for (int fieldIndex = 0; fieldIndex < table.Fields.Count; fieldIndex++)
            {
                var field = table.Fields[fieldIndex];
                if (string.IsNullOrEmpty(field.ReferenceTable) || string.IsNullOrEmpty(field.ReferenceField))
                    continue;

                var refTable = data.Tables.FirstOrDefault(t =>
                    t.Name.Equals(field.ReferenceTable, StringComparison.OrdinalIgnoreCase));
                if (refTable == null)
                {
                    result.Errors.Add(
                        $"[{table.Name}] field '{field.Name}': referenced table '{field.ReferenceTable}' does not exist");
                    continue;
                }

                var refFieldIndex = refTable.Fields.FindIndex(f =>
                    f.Name.Equals(field.ReferenceField, StringComparison.OrdinalIgnoreCase));
                if (refFieldIndex < 0)
                {
                    result.Errors.Add(
                        $"[{table.Name}] field '{field.Name}': referenced field '{field.ReferenceField}' not found in '{field.ReferenceTable}'");
                    continue;
                }

                var refValues = new HashSet<string>(
                    refTable.Rows.Select(r =>
                        refFieldIndex < r.Values.Count ? r.Values[refFieldIndex] : ""),
                    StringComparer.Ordinal);

                for (int rowIndex = 0; rowIndex < table.Rows.Count; rowIndex++)
                {
                    var row = table.Rows[rowIndex];
                    if (fieldIndex >= row.Values.Count)
                        continue;

                    var value = row.Values[fieldIndex];
                    if (string.IsNullOrEmpty(value))
                        continue;

                    if (!refValues.Contains(value))
                    {
                        result.Errors.Add(
                            $"[{table.Name}] Excel row {ExcelDataRow(rowIndex)}, col {fieldIndex + 1} ({field.Name}): value '{value}' not found in {field.ReferenceTable}.{field.ReferenceField}");
                    }
                }
            }
        }
    }
}

public class ValidationResult
{
    public bool IsValid => Errors.Count == 0;
    public List<string> Errors { get; set; } = new();
}
