namespace GameDataTool.Models;

public sealed class GameData
{
    public List<DataTable> Tables { get; } = new();
    public List<EnumType>  Enums  { get; } = new();
}
