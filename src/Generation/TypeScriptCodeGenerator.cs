using System.Text;
using GameDataTool.Core;
using GameDataTool.Models;

namespace GameDataTool.Generation;

/// <summary>
/// Generates TypeScript interfaces + JSON-based config accessors.
/// </summary>
public sealed class TypeScriptCodeGenerator
{
    private readonly TemplateEngine _te;

    public TypeScriptCodeGenerator(TemplateEngine te) { _te = te; }

    public void Generate(GameData data, string outputPath, bool generateEnums)
    {
        Directory.CreateDirectory(outputPath);

        if (generateEnums && data.Enums.Count > 0)
            WriteEnums(data, outputPath);

        foreach (var table in data.Tables)
            WriteTableData(table, outputPath);

        WriteManager(data, outputPath);
        Log.Info($"  TS → {outputPath}");
    }

    private void WriteEnums(GameData data, string outputPath)
    {
        var sb = new StringBuilder();
        foreach (var et in data.Enums)
        {
            sb.AppendLine($"export const enum {et.Name} {{");
            foreach (var v in et.Values)
            {
                var desc = v.Description.Length > 0 ? $" // {v.Description}" : "";
                sb.AppendLine($"    {v.Name} = {v.Value},{desc}");
            }
            sb.AppendLine("}");
            sb.AppendLine();
        }

        var tpl = _te.Load("typescript", "Enums.ts.template");
        File.WriteAllText(
            Path.Combine(outputPath, "Enums.ts"),
            TemplateEngine.Replace(tpl, ("ENUMS", sb.ToString().TrimEnd())));
    }

    private void WriteTableData(Models.DataTable table, string outputPath)
    {
        var props = BuildProperties(table);

        var tpl = _te.Load("typescript", "TableData.ts.template");
        File.WriteAllText(
            Path.Combine(outputPath, $"{table.Name}Config.ts"),
            TemplateEngine.Replace(tpl, ("TABLE_NAME", table.Name), ("PROPERTIES", props)));
    }

    private void WriteManager(GameData data, string outputPath)
    {
        var imports = new StringBuilder();
        var calls   = new StringBuilder();

        foreach (var t in data.Tables)
        {
            imports.AppendLine($"import {{ {t.Name}Config, {t.Name}Data }} from './{t.Name}Config';");
            calls.AppendLine($"        {t.Name}Config.initialize(loader('{t.Name}') as {t.Name}Data[]);");
        }

        var tpl = _te.Load("typescript", "GameDataManager.ts.template");
        File.WriteAllText(
            Path.Combine(outputPath, "GameDataManager.ts"),
            TemplateEngine.Replace(tpl,
                ("IMPORTS", imports.ToString().TrimEnd()),
                ("INIT_CALLS", calls.ToString().TrimEnd())));
    }

    private static string BuildProperties(Models.DataTable table)
    {
        var sb = new StringBuilder();
        foreach (var f in table.Fields)
        {
            var tsType = TypeScriptType(f);
            sb.AppendLine($"    readonly {Naming.ToCamelCase(f.Name)}: {tsType};");
        }
        return sb.ToString().TrimEnd();
    }

    private static string TypeScriptType(Field f) => f.Type switch
    {
        FieldType.String   => "string",
        FieldType.Int      => "number",
        FieldType.Long     => "number",
        FieldType.Float    => "number",
        FieldType.Bool     => "boolean",
        FieldType.DateTime => "string",
        FieldType.Enum     => f.EnumType ?? "number",
        _                  => "unknown",
    };
}
