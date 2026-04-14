namespace GameDataTool.Models;

/// <summary>
/// Strongly-typed cell value. Eliminates repeated string↔value round-trips.
/// The raw string is kept for error messages; the typed value is used for output.
/// </summary>
public readonly struct CellValue
{
    public string  Raw   { get; }
    public object? Typed { get; }

    private CellValue(string raw, object? typed) { Raw = raw; Typed = typed; }

    public static CellValue From(string raw, object? typed) => new(raw, typed);
    public static CellValue Empty(FieldType type) => new(DefaultString(type), DefaultTyped(type));

    public int      AsInt()      => (int)(Typed ?? 0);
    public long     AsLong()     => (long)(Typed ?? 0L);
    public float    AsFloat()    => (float)(Typed ?? 0f);
    public bool     AsBool()     => (bool)(Typed ?? false);
    public string   AsString()   => (string)(Typed ?? "");
    public DateTime AsDateTime() => (DateTime)(Typed ?? DateTime.MinValue);

    public bool IsEmpty => Typed is null || (Typed is string s && s.Length == 0);

    private static string DefaultString(FieldType t) => t switch
    {
        FieldType.String   => "",
        FieldType.Bool     => "false",
        FieldType.DateTime => "0001-01-01 00:00:00",
        _                  => "0",
    };

    private static object? DefaultTyped(FieldType t) => t switch
    {
        FieldType.String   => "",
        FieldType.Int      => 0,
        FieldType.Long     => 0L,
        FieldType.Float    => 0f,
        FieldType.Bool     => false,
        FieldType.DateTime => DateTime.MinValue,
        FieldType.Enum     => 0,
        _                  => null,
    };
}
