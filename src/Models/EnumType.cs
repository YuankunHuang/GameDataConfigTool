namespace GameDataTool.Models;

public sealed class EnumType
{
    public string          Name   { get; init; } = "";
    public List<EnumValue> Values { get; } = new();
}
