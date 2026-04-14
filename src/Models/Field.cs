namespace GameDataTool.Models;

public sealed class Field
{
    public string    Name        { get; init; } = "";
    public FieldType Type        { get; init; }
    public string    RawType     { get; init; } = "";
    public string    Description { get; init; } = "";
    public string?   RefTable    { get; init; }
    public string?   RefField    { get; init; }
    public string?   EnumType    { get; init; }
    public int?      RangeMin    { get; init; }
    public int?      RangeMax    { get; init; }
    public bool      Nullable    { get; init; }
}
