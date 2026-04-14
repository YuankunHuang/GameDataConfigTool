namespace GameDataTool.Models;

public sealed class DataTable
{
    public string        Name   { get; init; } = "";
    public List<Field>   Fields { get; } = new();
    public List<DataRow> Rows   { get; } = new();
}
