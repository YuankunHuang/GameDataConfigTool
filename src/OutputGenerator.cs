using GameDataTool.Core.Logging;
using System.Text.Json;
using GameDataTool.Parsers;
using GameDataTool.Core.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Linq;

namespace GameDataTool.Generators;

public class OutputGenerator
{
    public async Task GenerateJsonAsync(GameDataTool.Parsers.GameData data, string outputPath)
    {
        Logger.Info($"Generating JSON files to: {outputPath}");
        
        EnsureDirectoryExists(outputPath);

        // Generate enum JSON
        foreach (var enumType in data.Enums)
        {
            var enumData = enumType.Values.ToDictionary(v => v.Name, v => v.Value);
            var json = JsonSerializer.Serialize(enumData, new JsonSerializerOptions { WriteIndented = true });
            var filePath = Path.Combine(outputPath, $"{enumType.Name}.json");
            await File.WriteAllTextAsync(filePath, json);
        }

        // Generate data table JSON
        foreach (var table in data.Tables)
        {
            var tableData = new List<Dictionary<string, object>>();
            
            foreach (var row in table.Rows)
            {
                var rowData = new Dictionary<string, object>();
                for (int i = 0; i < table.Fields.Count && i < row.Values.Count; i++)
                {
                    var field = table.Fields[i];
                    var value = row.Values[i];
                    
                    if (!string.IsNullOrEmpty(value))
                    {
                        rowData[field.Name] = ConvertValue(field.Type, value);
                    }
                }
                tableData.Add(rowData);
            }

            var json = JsonSerializer.Serialize(tableData, new JsonSerializerOptions { WriteIndented = true });
            var filePath = Path.Combine(outputPath, $"{table.Name}.json");
            await File.WriteAllTextAsync(filePath, json);
        }
    }

    public async Task GenerateBinaryAsync(GameDataTool.Parsers.GameData data, string outputPath)
    {
        Logger.Info($"Generating binary files to: {outputPath}");
        
        EnsureDirectoryExists(outputPath);

        // Generate binary data files
        foreach (var table in data.Tables)
        {
            var binaryData = GenerateBinaryData(table);
            var filePath = Path.Combine(outputPath, $"{table.Name}.data");
            await File.WriteAllBytesAsync(filePath, binaryData);
        }

        // Generate data index file
        await GenerateBinaryIndexAsync(data, outputPath);
    }

    public async Task GenerateCodeAsync(GameDataTool.Parsers.GameData data, string outputPath, CodeGeneration config)
    {
        Logger.Info($"Generating code files to: {outputPath}");
        
        EnsureDirectoryExists(outputPath);

        if (config.GenerateEnum)
        {
            await GenerateEnumCodeAsync(data, outputPath, config);
        }

        await GenerateDataCodeAsync(data, outputPath, config);

        // Generate individual accessors for each table
        await GenerateTableAccessorsAsync(data, outputPath, config);

        // Generate centralized data manager
        await GenerateDataManagerAsync(data, outputPath, config);

        if (config.GenerateLoader)
        {
            await GenerateDataLoaderAsync(data, outputPath, config);
        }
    }

    private async Task GenerateEnumCodeAsync(GameDataTool.Parsers.GameData data, string outputPath, CodeGeneration config)
    {
        var enumCode = new StringBuilder();
        enumCode.AppendLine($"namespace {config.Namespace}");
        enumCode.AppendLine("{");

        foreach (var enumType in data.Enums)
        {
            enumCode.AppendLine($"    public enum {enumType.Name}");
            enumCode.AppendLine("    {");
            
            foreach (var value in enumType.Values)
            {
                enumCode.AppendLine($"        {value.Name} = {value.Value},");
            }
            
            enumCode.AppendLine("    }");
            enumCode.AppendLine();
        }

        enumCode.AppendLine("}");

        var filePath = Path.Combine(outputPath, "Enums.cs");
        await File.WriteAllTextAsync(filePath, enumCode.ToString());
    }

    private async Task GenerateDataCodeAsync(GameDataTool.Parsers.GameData data, string outputPath, CodeGeneration config)
    {
        foreach (var table in data.Tables)
        {
            var classCode = GenerateDataClass(table, config);
            var filePath = Path.Combine(outputPath, $"{table.Name}.cs");
            await File.WriteAllTextAsync(filePath, classCode);
        }
    }

    private async Task GenerateTableAccessorsAsync(GameDataTool.Parsers.GameData data, string outputPath, CodeGeneration config)
    {
        foreach (var table in data.Tables)
        {
            var accessorCode = GenerateTableAccessor(table, config);
            var filePath = Path.Combine(outputPath, $"{table.Name}Accessor.cs");
            await File.WriteAllTextAsync(filePath, accessorCode);
        }
    }

    private async Task GenerateDataManagerAsync(GameDataTool.Parsers.GameData data, string outputPath, CodeGeneration config)
    {
        var managerCode = new StringBuilder();
        managerCode.AppendLine("using System;");
        managerCode.AppendLine("using System.Collections.Generic;");
        managerCode.AppendLine("using System.IO;");
        managerCode.AppendLine("using System.Text.Json;");
        managerCode.AppendLine();
        managerCode.AppendLine($"namespace {config.Namespace}");
        managerCode.AppendLine("{");
        managerCode.AppendLine("    /// <summary>");
        managerCode.AppendLine("    /// Centralized Game Data Manager");
        managerCode.AppendLine("    /// Provides unified access to all game data");
        managerCode.AppendLine("    /// </summary>");
        managerCode.AppendLine("    public static class GameDataManager");
        managerCode.AppendLine("    {");
        managerCode.AppendLine("        private static bool _isInitialized = false;");
        managerCode.AppendLine();
        managerCode.AppendLine("        /// <summary>");
        managerCode.AppendLine("        /// Initialize all game data");
        managerCode.AppendLine("        /// </summary>");
        managerCode.AppendLine("        public static void Initialize()");
        managerCode.AppendLine("        {");
        managerCode.AppendLine("            if (_isInitialized) return;");
        managerCode.AppendLine();
        managerCode.AppendLine("            try");
        managerCode.AppendLine("            {");
        managerCode.AppendLine("                LoadAllData();");
        managerCode.AppendLine("                _isInitialized = true;");
        managerCode.AppendLine("                Console.WriteLine(\"GameDataManager: All data loaded successfully\");");
        managerCode.AppendLine("            }");
        managerCode.AppendLine("            catch (Exception ex)");
        managerCode.AppendLine("            {");
        managerCode.AppendLine("                Console.WriteLine($\"GameDataManager: Failed to load data - {ex.Message}\");");
        managerCode.AppendLine("            }");
        managerCode.AppendLine("        }");
        managerCode.AppendLine();
        managerCode.AppendLine("        /// <summary>");
        managerCode.AppendLine("        /// Reload all data");
        managerCode.AppendLine("        /// </summary>");
        managerCode.AppendLine("        public static void ReloadData()");
        managerCode.AppendLine("        {");
        managerCode.AppendLine("            _isInitialized = false;");
        managerCode.AppendLine("            Initialize();");
        managerCode.AppendLine("        }");
        managerCode.AppendLine();
        managerCode.AppendLine("        private static void LoadAllData()");
        managerCode.AppendLine("        {");

        foreach (var table in data.Tables)
        {
            managerCode.AppendLine($"            Load{table.Name}Data();");
        }

        managerCode.AppendLine("        }");
        managerCode.AppendLine();

        // Generate individual load methods for each table
        foreach (var table in data.Tables)
        {
            managerCode.AppendLine($"        private static void Load{table.Name}Data()");
            managerCode.AppendLine("        {");
            managerCode.AppendLine($"            try");
            managerCode.AppendLine("            {");
            managerCode.AppendLine($"                var jsonPath = Path.Combine(\"output\", \"json\", \"{table.Name}.json\");");
            managerCode.AppendLine("                if (File.Exists(jsonPath))");
            managerCode.AppendLine("                {");
            managerCode.AppendLine("                    var json = File.ReadAllText(jsonPath);");
            managerCode.AppendLine($"                    var data = JsonSerializer.Deserialize<{table.Name}[]>(json);");
            managerCode.AppendLine($"                    {table.Name}Accessor.Initialize(data);");
            managerCode.AppendLine($"                    Console.WriteLine($\"GameDataManager: Loaded {table.Name} data, records: {{data?.Length ?? 0}}\");");
            managerCode.AppendLine("                }");
            managerCode.AppendLine("                else");
            managerCode.AppendLine("                {");
            managerCode.AppendLine($"                    Console.WriteLine($\"GameDataManager: {table.Name} data file not found\");");
            managerCode.AppendLine("                }");
            managerCode.AppendLine("            }");
            managerCode.AppendLine("            catch (Exception ex)");
            managerCode.AppendLine("            {");
            managerCode.AppendLine($"                Console.WriteLine($\"GameDataManager: Failed to load {table.Name} data - {{ex.Message}}\");");
            managerCode.AppendLine("            }");
            managerCode.AppendLine("        }");
            managerCode.AppendLine();
        }

        managerCode.AppendLine("    }");
        managerCode.AppendLine("}");

        var filePath = Path.Combine(outputPath, "GameDataManager.cs");
        await File.WriteAllTextAsync(filePath, managerCode.ToString());
    }

    private string GenerateTableAccessor(DataTable table, CodeGeneration config)
    {
        var code = new StringBuilder();
        code.AppendLine("using System;");
        code.AppendLine("using System.Collections.Generic;");
        code.AppendLine("using System.Linq;");
        code.AppendLine();
        code.AppendLine($"namespace {config.Namespace}");
        code.AppendLine("{");
        code.AppendLine($"    /// <summary>");
        code.AppendLine($"    /// {table.Name} data accessor with custom logic support");
        code.AppendLine($"    /// </summary>");
        code.AppendLine($"    public static class {table.Name}Accessor");
        code.AppendLine("    {");
        code.AppendLine($"        private static {table.Name}[] _data;");
        code.AppendLine();
        code.AppendLine($"        /// <summary>");
        code.AppendLine($"        /// Initialize {table.Name} data");
        code.AppendLine($"        /// </summary>");
        code.AppendLine($"        public static void Initialize({table.Name}[] data)");
        code.AppendLine("        {");
        code.AppendLine("            _data = data;");
        code.AppendLine("        }");
        code.AppendLine();
        code.AppendLine($"        /// <summary>");
        code.AppendLine($"        /// Get all {table.Name} data");
        code.AppendLine($"        /// </summary>");
        code.AppendLine($"        public static {table.Name}[] GetAll()");
        code.AppendLine("        {");
        code.AppendLine("            return _data ?? Array.Empty<" + table.Name + ">();");
        code.AppendLine("        }");
        code.AppendLine();
        code.AppendLine($"        /// <summary>");
        code.AppendLine($"        /// Get {table.Name} by ID");
        code.AppendLine($"        /// </summary>");
        code.AppendLine($"        public static {table.Name} GetById(int id)");
        code.AppendLine("        {");
        code.AppendLine("            return _data?.FirstOrDefault(x => x.ID == id);");
        code.AppendLine("        }");
        code.AppendLine();
        code.AppendLine($"        /// <summary>");
        code.AppendLine($"        /// Get {table.Name} by custom condition");
        code.AppendLine($"        /// </summary>");
        code.AppendLine($"        public static {table.Name}[] GetByCondition(Func<{table.Name}, bool> condition)");
        code.AppendLine("        {");
        code.AppendLine("            return _data?.Where(condition).ToArray() ?? Array.Empty<" + table.Name + ">();");
        code.AppendLine("        }");
        code.AppendLine();
        code.AppendLine($"        /// <summary>");
        code.AppendLine($"        /// Add your custom logic methods here");
        code.AppendLine($"        /// </summary>");
        code.AppendLine();
        code.AppendLine("    }");
        code.AppendLine("}");

        return code.ToString();
    }

    private async Task GenerateDataLoaderAsync(GameDataTool.Parsers.GameData data, string outputPath, CodeGeneration config)
    {
        var loaderCode = new StringBuilder();
        loaderCode.AppendLine($"namespace {config.Namespace}");
        loaderCode.AppendLine("{");
        loaderCode.AppendLine("    using System;");
        loaderCode.AppendLine("    using System.Collections.Generic;");
        loaderCode.AppendLine("    using System.IO;");
        loaderCode.AppendLine("    using System.Text.Json;");
        loaderCode.AppendLine();
        loaderCode.AppendLine("    public static class DataLoader");
        loaderCode.AppendLine("    {");
        loaderCode.AppendLine("        private static Dictionary<string, object> _dataCache = new();");
        loaderCode.AppendLine();
        loaderCode.AppendLine("        public static T GetData<T>(string tableName) where T : class");
        loaderCode.AppendLine("        {");
        loaderCode.AppendLine("            if (_dataCache.TryGetValue(tableName, out var data))");
        loaderCode.AppendLine("            {");
        loaderCode.AppendLine("                return data as T;");
        loaderCode.AppendLine("            }");
        loaderCode.AppendLine();
        loaderCode.AppendLine("            var bytes = LoadBinaryData(tableName);");
        loaderCode.AppendLine("            var result = DeserializeData<T>(bytes);");
        loaderCode.AppendLine("            _dataCache[tableName] = result;");
        loaderCode.AppendLine("            return result;");
        loaderCode.AppendLine("        }");
        loaderCode.AppendLine();
        loaderCode.AppendLine("        private static byte[] LoadBinaryData(string tableName)");
        loaderCode.AppendLine("        {");
        loaderCode.AppendLine("            var path = Path.Combine(\"Data\", $\"{tableName}.data\");");
        loaderCode.AppendLine("            if (!File.Exists(path))");
        loaderCode.AppendLine("            {");
        loaderCode.AppendLine("                throw new FileNotFoundException($\"Data file not found: {path}\");");
        loaderCode.AppendLine("            }");
        loaderCode.AppendLine("            return File.ReadAllBytes(path);");
        loaderCode.AppendLine("        }");
        loaderCode.AppendLine();
        loaderCode.AppendLine("        private static T DeserializeData<T>(byte[] bytes) where T : class");
        loaderCode.AppendLine("        {");
        loaderCode.AppendLine("            try");
        loaderCode.AppendLine("            {");
        loaderCode.AppendLine("                using var stream = new MemoryStream(bytes);");
        loaderCode.AppendLine("                using var reader = new BinaryReader(stream);");
        loaderCode.AppendLine();
        loaderCode.AppendLine("                // Read header");
        loaderCode.AppendLine("                var fieldCount = reader.ReadInt32();");
        loaderCode.AppendLine("                var rowCount = reader.ReadInt32();");
        loaderCode.AppendLine();
        loaderCode.AppendLine("                // Read field info");
        loaderCode.AppendLine("                var fields = new List<(string Name, int Type)>();");
        loaderCode.AppendLine("                for (int i = 0; i < fieldCount; i++)");
        loaderCode.AppendLine("                {");
        loaderCode.AppendLine("                    var name = reader.ReadString();");
        loaderCode.AppendLine("                    var type = reader.ReadInt32();");
        loaderCode.AppendLine("                    fields.Add((name, type));");
        loaderCode.AppendLine("                }");
        loaderCode.AppendLine();
        loaderCode.AppendLine("                // Read data rows");
        loaderCode.AppendLine("                var dataList = new List<Dictionary<string, object>>();");
        loaderCode.AppendLine("                for (int row = 0; row < rowCount; row++)");
        loaderCode.AppendLine("                {");
        loaderCode.AppendLine("                    var rowData = new Dictionary<string, object>();");
        loaderCode.AppendLine("                    for (int col = 0; col < fieldCount; col++)");
        loaderCode.AppendLine("                    {");
        loaderCode.AppendLine("                        var field = fields[col];");
        loaderCode.AppendLine("                        var value = ReadValue(reader, field.Type);");
        loaderCode.AppendLine("                        rowData[field.Name] = value;");
        loaderCode.AppendLine("                    }");
        loaderCode.AppendLine("                    dataList.Add(rowData);");
        loaderCode.AppendLine("                }");
        loaderCode.AppendLine();
        loaderCode.AppendLine("                // Convert to target type");
        loaderCode.AppendLine("                var json = JsonSerializer.Serialize(dataList);");
        loaderCode.AppendLine("                return JsonSerializer.Deserialize<T>(json);");
        loaderCode.AppendLine("            }");
        loaderCode.AppendLine("            catch (Exception ex)");
        loaderCode.AppendLine("            {");
        loaderCode.AppendLine("                Console.WriteLine($\"Failed to deserialize data: {ex.Message}\");");
        loaderCode.AppendLine("                return null;");
        loaderCode.AppendLine("            }");
        loaderCode.AppendLine("        }");
        loaderCode.AppendLine();
        loaderCode.AppendLine("        private static object ReadValue(BinaryReader reader, int type)");
        loaderCode.AppendLine("        {");
        loaderCode.AppendLine("            return type switch");
        loaderCode.AppendLine("            {");
        loaderCode.AppendLine("                0 => reader.ReadString(), // String");
        loaderCode.AppendLine("                1 => reader.ReadInt32(),   // Int");
        loaderCode.AppendLine("                2 => reader.ReadInt64(),   // Long");
        loaderCode.AppendLine("                3 => reader.ReadSingle(),  // Float");
        loaderCode.AppendLine("                4 => reader.ReadBoolean(), // Bool");
        loaderCode.AppendLine("                5 => reader.ReadInt32(),   // Enum");
        loaderCode.AppendLine("                _ => reader.ReadString()");
        loaderCode.AppendLine("            };");
        loaderCode.AppendLine("        }");
        loaderCode.AppendLine("    }");
        loaderCode.AppendLine("}");

        var filePath = Path.Combine(outputPath, "DataLoader.cs");
        await File.WriteAllTextAsync(filePath, loaderCode.ToString());
    }

    private string GenerateDataClass(DataTable table, CodeGeneration config)
    {
        var code = new StringBuilder();
        code.AppendLine($"namespace {config.Namespace}");
        code.AppendLine("{");
        code.AppendLine($"    /// <summary>");
        code.AppendLine($"    /// {table.Name} data class");
        code.AppendLine($"    /// </summary>");
        code.AppendLine($"    public class {table.Name}");
        code.AppendLine("    {");

        // Generate properties
        foreach (var field in table.Fields)
        {
            var csharpType = GetCSharpType(field.Type);
            var nullableType = GetNullableCSharpType(field.Type);
            
            code.AppendLine($"        /// <summary>");
            code.AppendLine($"        /// {field.Description}");
            code.AppendLine($"        /// </summary>");
            code.AppendLine($"        public {nullableType} {field.Name} {{ get; set; }}");
            code.AppendLine();
        }

        code.AppendLine("    }");
        code.AppendLine("}");

        return code.ToString();
    }

    private byte[] GenerateBinaryData(DataTable table)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        // Write header
        writer.Write(table.Fields.Count);
        writer.Write(table.Rows.Count);

        // Write field info
        foreach (var field in table.Fields)
        {
            writer.Write(field.Name);
            writer.Write((int)field.Type);
        }

        // Write data rows
        foreach (var row in table.Rows)
        {
            for (int i = 0; i < table.Fields.Count && i < row.Values.Count; i++)
            {
                var field = table.Fields[i];
                var value = row.Values[i];
                WriteBinaryValue(writer, field.Type, value);
            }
        }

        return stream.ToArray();
    }

    private async Task GenerateBinaryIndexAsync(GameDataTool.Parsers.GameData data, string outputPath)
    {
        var index = new Dictionary<string, object>();
        
        foreach (var table in data.Tables)
        {
            index[table.Name] = new
            {
                FieldCount = table.Fields.Count,
                RowCount = table.Rows.Count,
                Fields = table.Fields.Select(f => new { f.Name, Type = f.Type.ToString() }).ToArray()
            };
        }

        var json = JsonSerializer.Serialize(index, new JsonSerializerOptions { WriteIndented = true });
        var filePath = Path.Combine(outputPath, "index.json");
        await File.WriteAllTextAsync(filePath, json);
    }

    private void WriteBinaryValue(BinaryWriter writer, FieldType type, string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            // Write default values for empty fields
            switch (type)
            {
                case FieldType.String:
                    writer.Write("");
                    break;
                case FieldType.Int:
                case FieldType.Long:
                case FieldType.Enum:
                    writer.Write(0);
                    break;
                case FieldType.Float:
                    writer.Write(0.0f);
                    break;
                case FieldType.Bool:
                    writer.Write(false);
                    break;
            }
            return;
        }

        switch (type)
        {
            case FieldType.String:
                writer.Write(value);
                break;
            case FieldType.Int:
                if (int.TryParse(value, out var intValue))
                    writer.Write(intValue);
                else
                    writer.Write(0);
                break;
            case FieldType.Long:
                if (long.TryParse(value, out var longValue))
                    writer.Write(longValue);
                else
                    writer.Write(0L);
                break;
            case FieldType.Float:
                if (float.TryParse(value, out var floatValue))
                    writer.Write(floatValue);
                else
                    writer.Write(0.0f);
                break;
            case FieldType.Bool:
                if (bool.TryParse(value, out var boolValue))
                    writer.Write(boolValue);
                else
                    writer.Write(false);
                break;
            case FieldType.Enum:
                if (int.TryParse(value, out var enumValue))
                    writer.Write(enumValue);
                else
                    writer.Write(0);
                break;
        }
    }

    private object ConvertValue(FieldType type, string value)
    {
        if (string.IsNullOrEmpty(value))
            return null;

        return type switch
        {
            FieldType.String => value,
            FieldType.Int => int.TryParse(value, out var intVal) ? intVal : 0,
            FieldType.Long => long.TryParse(value, out var longVal) ? longVal : 0L,
            FieldType.Float => float.TryParse(value, out var floatVal) ? floatVal : 0.0f,
            FieldType.Bool => bool.TryParse(value, out var boolVal) && boolVal,
            FieldType.Enum => int.TryParse(value, out var enumVal) ? enumVal : 0,
            _ => value
        };
    }

    private string GetCSharpType(FieldType type)
    {
        return type switch
        {
            FieldType.String => "string",
            FieldType.Int => "int",
            FieldType.Long => "long",
            FieldType.Float => "float",
            FieldType.Bool => "bool",
            FieldType.Enum => "int",
            _ => "object"
        };
    }

    private string GetNullableCSharpType(FieldType type)
    {
        var baseType = GetCSharpType(type);
        return baseType switch
        {
            "string" => "string?",
            "object" => "object?",
            _ => $"{baseType}?"
        };
    }

    private void EnsureDirectoryExists(string path)
    {
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }
    }
} 