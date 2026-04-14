using GameDataTool.Core;
using GameDataTool.Models;
using Newtonsoft.Json;

namespace GameDataTool.Generation;

public static class JsonGenerator
{
    public static void Generate(GameData data, string outputPath)
    {
        Directory.CreateDirectory(outputPath);

        foreach (var et in data.Enums)
        {
            var dict = et.Values.ToDictionary(v => v.Name, v => v.Value);
            var json = JsonConvert.SerializeObject(dict, Formatting.Indented);
            File.WriteAllText(Path.Combine(outputPath, $"{et.Name}.json"), json);
        }

        foreach (var table in data.Tables)
        {
            var rows = new List<Dictionary<string, object?>>(table.Rows.Count);
            foreach (var row in table.Rows)
            {
                var dict = new Dictionary<string, object?>(table.Fields.Count);
                for (var i = 0; i < table.Fields.Count && i < row.Values.Count; i++)
                {
                    var v = row.Values[i];
                    if (!v.IsEmpty)
                        dict[table.Fields[i].Name] = v.Typed;
                }
                rows.Add(dict);
            }

            var json = JsonConvert.SerializeObject(rows, Formatting.Indented);
            File.WriteAllText(Path.Combine(outputPath, $"{table.Name}.json"), json);
        }

        Log.Info($"  JSON → {outputPath}");
    }
}
