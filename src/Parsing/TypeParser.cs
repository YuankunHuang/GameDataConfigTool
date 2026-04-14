using GameDataTool.Models;

namespace GameDataTool.Parsing;

/// <summary>Parses column header type declarations like <c>int</c>, <c>enum(Rarity)</c>, <c>int^Range(1,100)</c>, <c>int^id(Monster)</c>.</summary>
public static class TypeParser
{
    public static FieldTypeInfo Parse(string typeStr, string tableName, int col)
    {
        var raw   = typeStr;
        var lower = typeStr.Trim().ToLowerInvariant();

        if (lower.StartsWith("enum(") && lower.EndsWith(')'))
        {
            var enumName = typeStr[5..^1].Trim();
            return new FieldTypeInfo(FieldType.Enum, raw, EnumType: enumName);
        }

        if (lower.Contains("^range(", StringComparison.Ordinal))
            return ParseRange(typeStr, raw);

        if (lower.Contains("^id(", StringComparison.Ordinal))
            return ParseForeignKey(typeStr, raw);

        var ft = ParseScalar(lower)
            ?? throw new InvalidDataException(
                $"[{tableName}] col {col}: unknown type '{typeStr}'. " +
                "Use int, long, float, string, bool, datetime, enum(Name), int^id(Table), int^Range(min,max).");

        return new FieldTypeInfo(ft, raw);
    }

    public static FieldType? ParseScalar(string lower) => lower switch
    {
        "int" or "integer"      => FieldType.Int,
        "long"                  => FieldType.Long,
        "float" or "double"     => FieldType.Float,
        "string" or "text"      => FieldType.String,
        "bool" or "boolean"     => FieldType.Bool,
        "datetime" or "date"    => FieldType.DateTime,
        _                       => null,
    };

    private static FieldTypeInfo ParseRange(string typeStr, string raw)
    {
        var baseType = ResolveBaseType(typeStr.Split('^')[0].Trim());
        var content  = ExtractParens(typeStr, '^');
        int? min = null, max = null;

        if (content != null)
        {
            var parts = content.Split(',');
            if (parts.Length == 2 && int.TryParse(parts[0].Trim(), out var a) && int.TryParse(parts[1].Trim(), out var b))
            {
                min = a; max = b;
            }
        }

        return new FieldTypeInfo(baseType, raw, RangeMin: min, RangeMax: max);
    }

    private static FieldTypeInfo ParseForeignKey(string typeStr, string raw)
    {
        var baseType = ResolveBaseType(typeStr.Split('^')[0].Trim());
        var caret    = typeStr.AsSpan()[typeStr.IndexOf('^')..];
        var pStart   = caret.IndexOf('(');
        var pEnd     = caret.IndexOf(')');

        string? refField = null, refTable = null;
        if (pStart > 0 && pEnd > pStart)
        {
            refField = caret[..pStart].ToString();
            refTable = caret[(pStart + 1)..pEnd].ToString();
        }

        return new FieldTypeInfo(baseType, raw, RefTable: refTable, RefField: refField);
    }

    private static FieldType ResolveBaseType(string s) =>
        ParseScalar(s.ToLowerInvariant()) ?? FieldType.Int;

    private static string? ExtractParens(string input, char marker)
    {
        var idx = input.IndexOf(marker);
        if (idx < 0) return null;
        var sub  = input[idx..];
        var open = sub.IndexOf('(');
        var close = sub.IndexOf(')');
        return (open >= 0 && close > open) ? sub[(open + 1)..close] : null;
    }
}
