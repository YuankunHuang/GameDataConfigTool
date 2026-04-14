namespace GameDataTool.Models;

/// <summary>Result of parsing a column type declaration like <c>int^Range(1,100)</c>.</summary>
public sealed record FieldTypeInfo(
    FieldType Type,
    string    RawType,
    string?   RefTable  = null,
    string?   RefField  = null,
    string?   EnumType  = null,
    int?      RangeMin  = null,
    int?      RangeMax  = null);
