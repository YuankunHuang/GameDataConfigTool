using GameDataTool.Core;
using GameDataTool.Models;
using Newtonsoft.Json;

namespace GameDataTool.Generation;

public static class BinaryGenerator
{
    public static void Generate(GameData data, string outputPath)
    {
        Directory.CreateDirectory(outputPath);

        foreach (var table in data.Tables)
        {
            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);

            bw.Write(table.Fields.Count);
            bw.Write(table.Rows.Count);

            foreach (var f in table.Fields)
            {
                bw.Write(f.Name);
                bw.Write((int)f.Type);
            }

            foreach (var row in table.Rows)
            {
                for (var c = 0; c < table.Fields.Count; c++)
                {
                    var ft = table.Fields[c].Type;
                    var cv = c < row.Values.Count ? row.Values[c] : CellValue.Empty(ft);
                    WriteCellValue(bw, ft, cv);
                }
            }

            File.WriteAllBytes(Path.Combine(outputPath, $"{table.Name}.data"), ms.ToArray());
        }

        WriteIndex(data, outputPath);
        Log.Info($"  Binary → {outputPath}");
    }

    private static void WriteCellValue(BinaryWriter w, FieldType ft, CellValue cv)
    {
        switch (ft)
        {
            case FieldType.String:   w.Write(cv.AsString());              break;
            case FieldType.Int:      w.Write(cv.AsInt());                 break;
            case FieldType.Long:     w.Write(cv.AsLong());                break;
            case FieldType.Float:    w.Write(cv.AsFloat());               break;
            case FieldType.Bool:     w.Write(cv.AsBool());                break;
            case FieldType.DateTime: w.Write(cv.AsDateTime().Ticks);      break;
            case FieldType.Enum:     w.Write(cv.AsInt());                 break;
        }
    }

    private static void WriteIndex(GameData data, string outputPath)
    {
        var index = data.Tables.ToDictionary(
            t => t.Name,
            t => new
            {
                t.Fields.Count,
                RowCount = t.Rows.Count,
                Fields = t.Fields.Select(f => new { f.Name, Type = f.Type.ToString() }).ToArray()
            });

        File.WriteAllText(
            Path.Combine(outputPath, "index.json"),
            JsonConvert.SerializeObject(index, Formatting.Indented));
    }
}
