using System.Text;
using GameDataTool.Core;
using GameDataTool.Models;

namespace GameDataTool.Generation;

/// <summary>
/// Generates strongly-typed C# code. Runtime loader uses direct property assignment — zero reflection.
/// </summary>
public sealed class CSharpCodeGenerator
{
    private readonly TemplateEngine _te;
    private readonly string _ns;

    public CSharpCodeGenerator(TemplateEngine te, string ns) { _te = te; _ns = ns; }

    public void Generate(GameData data, string outputPath, bool generateEnums)
    {
        Directory.CreateDirectory(outputPath);

        if (generateEnums && data.Enums.Count > 0)
            WriteEnums(data, outputPath);

        foreach (var table in data.Tables)
        {
            WriteTableData(table, outputPath);
            WriteTableDataExt(table, outputPath);
        }

        WriteManager(data, outputPath);
        Log.Info($"  C# → {outputPath}");
    }

    private void WriteEnums(GameData data, string outputPath)
    {
        var sb = new StringBuilder();
        foreach (var et in data.Enums)
        {
            sb.AppendLine($"    public enum {et.Name}");
            sb.AppendLine("    {");
            foreach (var v in et.Values)
            {
                if (v.Description.Length > 0)
                    sb.AppendLine($"        /// <summary>{v.Description}</summary>");
                sb.AppendLine($"        {v.Name} = {v.Value},");
            }
            sb.AppendLine("    }");
            sb.AppendLine();
        }

        var tpl = _te.Load("csharp", "Enums.cs.template");
        File.WriteAllText(
            Path.Combine(outputPath, "Enums.cs"),
            TemplateEngine.Replace(tpl, ("NAMESPACE", _ns), ("ENUMS", sb.ToString().TrimEnd())));
    }

    private void WriteTableData(Models.DataTable table, string outputPath)
    {
        var props  = BuildProperties(table);
        var loader = BuildLoader(table);

        var tpl = _te.Load("csharp", "TableData.cs.template");
        File.WriteAllText(
            Path.Combine(outputPath, $"{table.Name}Config.cs"),
            TemplateEngine.Replace(tpl,
                ("NAMESPACE", _ns),
                ("TABLE_NAME", table.Name),
                ("PROPERTIES", props),
                ("LOADER", loader)));
    }

    private void WriteTableDataExt(Models.DataTable table, string outputPath)
    {
        var extDir = Path.Combine(outputPath, "ext");
        Directory.CreateDirectory(extDir);
        var path = Path.Combine(extDir, $"{table.Name}Config.ext.cs");
        if (File.Exists(path)) return;

        var tpl = _te.Load("csharp", "TableDataExt.cs.template");
        File.WriteAllText(path, TemplateEngine.Replace(tpl, ("NAMESPACE", _ns), ("TABLE_NAME", table.Name)));
    }

    private void WriteManager(GameData data, string outputPath)
    {
        var sb = new StringBuilder();
        foreach (var t in data.Tables)
            sb.AppendLine($"            {t.Name}Config.LoadFromBinary(System.IO.Path.Combine(dataDir, \"{t.Name}.data\"));");

        var tpl = _te.Load("csharp", "GameDataManager.cs.template");
        File.WriteAllText(
            Path.Combine(outputPath, "GameDataManager.cs"),
            TemplateEngine.Replace(tpl, ("NAMESPACE", _ns), ("INIT_CALLS", sb.ToString().TrimEnd())));
    }

    // ─── Property generation ────────────────────────────────

    private static string BuildProperties(Models.DataTable table)
    {
        var sb = new StringBuilder();
        foreach (var f in table.Fields)
        {
            if (f.Description.Length > 0)
            {
                sb.AppendLine("        /// <summary>");
                foreach (var line in f.Description.ReplaceLineEndings("\n").Split('\n'))
                    sb.AppendLine($"        /// {line.TrimEnd()}");
                sb.AppendLine("        /// </summary>");
            }
            sb.AppendLine($"        public {CSharpType(f)} {Naming.ToPascalCase(f.Name)} {{ get; set; }}");
        }
        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// Generates a direct-assignment binary loader. Zero reflection at runtime.
    /// </summary>
    private static string BuildLoader(Models.DataTable table)
    {
        var sb = new StringBuilder();
        sb.AppendLine("        public static void LoadFromBinary(string path)");
        sb.AppendLine("        {");
        sb.AppendLine("            using var stream = System.IO.File.OpenRead(path);");
        sb.AppendLine("            using var r = new System.IO.BinaryReader(stream);");
        sb.AppendLine();
        sb.AppendLine("            int fieldCount = r.ReadInt32();");
        sb.AppendLine("            int rowCount   = r.ReadInt32();");
        sb.AppendLine();
        sb.AppendLine("            // Skip field headers (name + type per field)");
        sb.AppendLine("            for (int i = 0; i < fieldCount; i++) { r.ReadString(); r.ReadInt32(); }");
        sb.AppendLine();
        sb.AppendLine($"            var list = new {table.Name}Data[rowCount];");
        sb.AppendLine($"            var dict = new Dictionary<int, {table.Name}Data>(rowCount);");
        sb.AppendLine();
        sb.AppendLine("            for (int i = 0; i < rowCount; i++)");
        sb.AppendLine("            {");
        sb.AppendLine($"                var item = new {table.Name}Data");
        sb.AppendLine("                {");

        for (var c = 0; c < table.Fields.Count; c++)
        {
            var f = table.Fields[c];
            var prop = Naming.ToPascalCase(f.Name);
            var reader = BinaryReaderCall(f);
            var comma = c < table.Fields.Count - 1 ? "," : "";
            sb.AppendLine($"                    {prop} = {reader}{comma}");
        }

        sb.AppendLine("                };");
        sb.AppendLine("                list[i] = item;");
        sb.AppendLine("                dict[item.Id] = item;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            _all  = list;");
        sb.AppendLine("            _byId = dict;");
        sb.AppendLine("        }");
        return sb.ToString().TrimEnd();
    }

    // ─── Type mappings ──────────────────────────────────────

    private static string CSharpType(Field f) => f.Type switch
    {
        FieldType.String   => "string",
        FieldType.Int      => "int",
        FieldType.Long     => "long",
        FieldType.Float    => "float",
        FieldType.Bool     => "bool",
        FieldType.DateTime => "DateTime",
        FieldType.Enum     => f.EnumType ?? "int",
        _                  => "object",
    };

    private static string BinaryReaderCall(Field f) => f.Type switch
    {
        FieldType.String   => "r.ReadString()",
        FieldType.Int      => "r.ReadInt32()",
        FieldType.Long     => "r.ReadInt64()",
        FieldType.Float    => "r.ReadSingle()",
        FieldType.Bool     => "r.ReadBoolean()",
        FieldType.DateTime => "new DateTime(r.ReadInt64())",
        FieldType.Enum     => $"({f.EnumType ?? "int"})r.ReadInt32()",
        _                  => "r.ReadString()",
    };
}
