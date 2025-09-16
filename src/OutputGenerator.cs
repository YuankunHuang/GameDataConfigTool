using GameDataTool.Core.Logging;
using System.Text.Encodings.Web;
using Newtonsoft.Json;
using GameDataTool.Parsers;
using GameDataTool.Core.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Linq;
using System.Reflection;

namespace GameDataTool.Generators;

public class OutputGenerator
{
    public async Task GenerateJsonAsync(GameDataTool.Parsers.GameData data, string outputPath)
    {
        EnsureDirectoryExists(outputPath);

        // Generate enum JSON
        foreach (var enumType in data.Enums)
        {
            var enumData = enumType.Values.ToDictionary(v => v.Name, v => v.Value);
            var json = JsonConvert.SerializeObject(enumData, Formatting.Indented);
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

            var json = JsonConvert.SerializeObject(tableData, Formatting.Indented);
            var filePath = Path.Combine(outputPath, $"{table.Name}.json");
            await File.WriteAllTextAsync(filePath, json);
        }
    }

    public async Task GenerateBinaryAsync(GameDataTool.Parsers.GameData data, string outputPath)
    {
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
        EnsureDirectoryExists(outputPath);

        if (config.GenerateEnum)
        {
            await GenerateEnumCodeAsync(data, outputPath, config);
        }

        await GenerateDataCodeAsync(data, outputPath, config);

        // Generate centralized data manager
        await GenerateDataManagerAsync(data, outputPath, config);

        if (config.GenerateLoader)
        {
            // await GenerateDataLoaderAsync(data, outputPath, config); // Removed as per edit hint
        }
    }

    private async Task GenerateEnumCodeAsync(GameDataTool.Parsers.GameData data, string outputPath, CodeGeneration config)
    {
        var enumCode = new StringBuilder();
        enumCode.AppendLine($"namespace {config.Namespace}");
        enumCode.AppendLine("{");

        // Add FieldType enum for binary data reading
        enumCode.AppendLine("    /// <summary>");
        enumCode.AppendLine("    /// Field types for binary data reading");
        enumCode.AppendLine("    /// </summary>");
        enumCode.AppendLine("    public enum FieldType");
        enumCode.AppendLine("    {");
        enumCode.AppendLine("        String,");
        enumCode.AppendLine("        Int,");
        enumCode.AppendLine("        Long,");
        enumCode.AppendLine("        Float,");
        enumCode.AppendLine("        Bool,");
        enumCode.AppendLine("        DateTime,");
        enumCode.AppendLine("        Enum");
        enumCode.AppendLine("    }");
        enumCode.AppendLine();

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
        // Generate BaseConfigData base class (always regenerate)
        var baseConfigPath = Path.Combine(outputPath, "BaseConfigData.cs");
        var baseConfigCode = GenerateBaseConfigDataClass(config);
        await File.WriteAllTextAsync(baseConfigPath, baseConfigCode);
        
        foreach (var table in data.Tables)
        {
            // Generate ConfigData main class
            var configDataCode = GenerateConfigDataClass(table, config);
            var filePath = Path.Combine(outputPath, $"{table.Name}Config.cs");
            await File.WriteAllTextAsync(filePath, configDataCode);
            // Generate corresponding ext ConfigData class
            var extConfigDataCode = GenerateExtConfigDataClass(table, config);
            // Write .ext.cs files to 'ext' subdirectory
            var extDir = Path.Combine(outputPath, "ext");
            if (!Directory.Exists(extDir))
                Directory.CreateDirectory(extDir);
            var extFilePath = Path.Combine(extDir, $"{table.Name}Config.ext.cs");
            if (!File.Exists(extFilePath))
            {
                await File.WriteAllTextAsync(extFilePath, extConfigDataCode);
            }
        }
    }

    private async Task GenerateDataManagerAsync(GameDataTool.Parsers.GameData data, string outputPath, CodeGeneration config)
    {
        var managerCode = new StringBuilder();
        managerCode.AppendLine("using System;");
        managerCode.AppendLine("using System.Reflection;");
        managerCode.AppendLine("using System.Linq;");
        managerCode.AppendLine("using UnityEngine;");
        managerCode.AppendLine();
        managerCode.AppendLine($"namespace {config.Namespace}");
        managerCode.AppendLine("{");
        managerCode.AppendLine("    /// <summary>");
        managerCode.AppendLine("    /// Centralized Game Data Manager");
        managerCode.AppendLine("    /// Automatically initializes all Config classes via reflection");
        managerCode.AppendLine("    /// </summary>");
        managerCode.AppendLine("    public static class GameDataManager");
        managerCode.AppendLine("    {");
        managerCode.AppendLine("        private static bool _isInitialized = false;");
        managerCode.AppendLine();
        managerCode.AppendLine("        /// <summary>");
        managerCode.AppendLine("        /// Initialize all Config classes (WebGL-compatible version)");
        managerCode.AppendLine("        /// </summary>");
        managerCode.AppendLine("        public static void Initialize()");
        managerCode.AppendLine("        {");
        managerCode.AppendLine("            if (_isInitialized) return;");
        managerCode.AppendLine();
        managerCode.AppendLine("            Debug.Log(\"[GameDataManager] Starting initialization...\");");
        managerCode.AppendLine();
        managerCode.AppendLine("#if UNITY_WEBGL && !UNITY_EDITOR");
        managerCode.AppendLine("            // WebGL requires async initialization - start the async process");
        managerCode.AppendLine("            _ = InitializeWebGLAsync();");
        managerCode.AppendLine("#else");
        managerCode.AppendLine("            InitializeDefault();");
        managerCode.AppendLine("            _isInitialized = true;");
        managerCode.AppendLine("            Debug.Log(\"[GameDataManager] Initialization completed\");");
        managerCode.AppendLine("#endif");
        managerCode.AppendLine("        }");
        managerCode.AppendLine();
        managerCode.AppendLine("#if UNITY_WEBGL && !UNITY_EDITOR");
        managerCode.AppendLine("        /// <summary>");
        managerCode.AppendLine("        /// Async initialization for WebGL platform");
        managerCode.AppendLine("        /// </summary>");
        managerCode.AppendLine("        public static async System.Threading.Tasks.Task InitializeWebGLAsync()");
        managerCode.AppendLine("        {");
        managerCode.AppendLine("            Debug.Log(\"[GameDataManager] Using WebGL async initialization mode\");");
        managerCode.AppendLine();
        managerCode.AppendLine("            var initTasks = new System.Collections.Generic.List<System.Threading.Tasks.Task>();");
        managerCode.AppendLine();
        managerCode.AppendLine("            // Start all config initializations in parallel");

        // Generate async initialization for each table
        foreach (var table in data.Tables)
        {
            managerCode.AppendLine($"            initTasks.Add(InitializeConfigAsync<{config.Namespace}.{table.Name}Data>(\"{table.Name}Config\",");
            managerCode.AppendLine($"                () => {config.Namespace}.BaseConfigData<{config.Namespace}.{table.Name}Data>.InitializeAsync(");
            managerCode.AppendLine($"                    System.IO.Path.Combine(UnityEngine.Application.streamingAssetsPath, \"ConfigData\", \"{table.Name}.data\"))));");
            managerCode.AppendLine();
        }

        managerCode.AppendLine("            // Wait for all configurations to load");
        managerCode.AppendLine("            await System.Threading.Tasks.Task.WhenAll(initTasks);");
        managerCode.AppendLine();
        managerCode.AppendLine("            _isInitialized = true;");
        managerCode.AppendLine("            Debug.Log(\"[GameDataManager] WebGL async initialization completed\");");
        managerCode.AppendLine("        }");
        managerCode.AppendLine();
        managerCode.AppendLine("        private static async System.Threading.Tasks.Task InitializeConfigAsync<T>(string configName, System.Func<System.Threading.Tasks.Task> initFunc)");
        managerCode.AppendLine("        {");
        managerCode.AppendLine("            try");
        managerCode.AppendLine("            {");
        managerCode.AppendLine("                await initFunc();");
        managerCode.AppendLine("                Debug.Log($\"[GameDataManager] {configName} initialized successfully\");");
        managerCode.AppendLine("            }");
        managerCode.AppendLine("            catch (System.Exception e)");
        managerCode.AppendLine("            {");
        managerCode.AppendLine("                Debug.LogWarning($\"[GameDataManager] {configName} failed: {e.Message}\");");
        managerCode.AppendLine("            }");
        managerCode.AppendLine("        }");
        managerCode.AppendLine("#endif");
        managerCode.AppendLine();
        managerCode.AppendLine("        /// <summary>");
        managerCode.AppendLine("        /// Default initialization using reflection (for other platforms)");
        managerCode.AppendLine("        /// </summary>");
        managerCode.AppendLine("        private static void InitializeDefault()");
        managerCode.AppendLine("        {");
        managerCode.AppendLine("            Debug.Log(\"[GameDataManager] Using reflection-based initialization mode\");");
        managerCode.AppendLine();
        managerCode.AppendLine("            // Search in multiple assemblies for better detection");
        managerCode.AppendLine("            var assemblies = new[]");
        managerCode.AppendLine("            {");
        managerCode.AppendLine("                Assembly.GetExecutingAssembly(),");
        managerCode.AppendLine("                typeof(GameDataManager).Assembly");
        managerCode.AppendLine("            };");
        managerCode.AppendLine();
        managerCode.AppendLine("            var allTypes = new System.Collections.Generic.List<System.Type>();");
        managerCode.AppendLine("            foreach (var assembly in assemblies.Distinct())");
        managerCode.AppendLine("            {");
        managerCode.AppendLine("                if (assembly != null)");
        managerCode.AppendLine("                {");
        managerCode.AppendLine("                    try");
        managerCode.AppendLine("                    {");
        managerCode.AppendLine("                        allTypes.AddRange(assembly.GetTypes());");
        managerCode.AppendLine("                    }");
        managerCode.AppendLine("                    catch (Exception e)");
        managerCode.AppendLine("                    {");
        managerCode.AppendLine("                        Debug.LogWarning($\"[GameDataManager] Failed to get types from assembly {assembly.FullName}: {e.Message}\");");
        managerCode.AppendLine("                    }");
        managerCode.AppendLine("                }");
        managerCode.AppendLine("            }");
        managerCode.AppendLine();
        managerCode.AppendLine("            var configTypes = allTypes");
        managerCode.AppendLine("                .Where(t =>");
        managerCode.AppendLine("                {");
        managerCode.AppendLine("                    if (!t.IsClass || !t.IsPublic || !t.Name.EndsWith(\"Config\"))");
        managerCode.AppendLine("                        return false;");
        managerCode.AppendLine();
        managerCode.AppendLine("                    if (t.BaseType == null)");
        managerCode.AppendLine("                        return false;");
        managerCode.AppendLine();
        managerCode.AppendLine("                    // More flexible base type checking");
        managerCode.AppendLine("                    var baseType = t.BaseType;");
        managerCode.AppendLine("                    while (baseType != null)");
        managerCode.AppendLine("                    {");
        managerCode.AppendLine("                        if (baseType.IsGenericType)");
        managerCode.AppendLine("                        {");
        managerCode.AppendLine("                            var genericDef = baseType.GetGenericTypeDefinition();");
        managerCode.AppendLine("                            if (genericDef.Name.StartsWith(\"BaseConfigData\"))");
        managerCode.AppendLine("                                return true;");
        managerCode.AppendLine("                        }");
        managerCode.AppendLine("                        if (baseType.Name.StartsWith(\"BaseConfigData\"))");
        managerCode.AppendLine("                            return true;");
        managerCode.AppendLine("                        baseType = baseType.BaseType;");
        managerCode.AppendLine("                    }");
        managerCode.AppendLine("                    return false;");
        managerCode.AppendLine("                });");
        managerCode.AppendLine();
        managerCode.AppendLine("            foreach (var type in configTypes)");
        managerCode.AppendLine("            {");
        managerCode.AppendLine("                var method = type.GetMethod(\"Initialize\", BindingFlags.Public | BindingFlags.Static, null, Type.EmptyTypes, null);");
        managerCode.AppendLine("                if (method != null)");
        managerCode.AppendLine("                {");
        managerCode.AppendLine("                    try");
        managerCode.AppendLine("                    {");
        managerCode.AppendLine("                        method.Invoke(null, null);");
        managerCode.AppendLine("                        Debug.Log($\"[GameDataManager] {type.Name} initialized\");");
        managerCode.AppendLine("                    }");
        managerCode.AppendLine("                    catch (Exception e)");
        managerCode.AppendLine("                    {");
        managerCode.AppendLine("                        Debug.LogWarning($\"[GameDataManager] {type.Name} failed: {e.Message}\");");
        managerCode.AppendLine("                    }");
        managerCode.AppendLine("                }");
        managerCode.AppendLine("            }");
        managerCode.AppendLine("        }");
        managerCode.AppendLine("    }");
        managerCode.AppendLine("}");
        var filePath = Path.Combine(outputPath, "GameDataManager.cs");
        await File.WriteAllTextAsync(filePath, managerCode.ToString());
    }

    private string GenerateConfigDataClass(DataTable table, CodeGeneration config)
    {
        var code = new StringBuilder();
        code.AppendLine("using System;");
        code.AppendLine("using System.Collections.Generic;");
        code.AppendLine("using System.IO;");
        code.AppendLine();
        code.AppendLine($"namespace {config.Namespace}");
        code.AppendLine("{");
        // Data structure class
        code.AppendLine($"    public class {table.Name}Data");
        code.AppendLine("    {");
        foreach (var field in table.Fields)
        {
            var type = GetCSharpType(field.Type, field.EnumType);
            var propName = ToPascalCase(field.Name);
            // Only keep Excel field comment as is, all other comments in English
            if (!string.IsNullOrWhiteSpace(field.Description))
            {
            code.AppendLine($"        /// <summary>");
                foreach (var line in field.Description.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n'))
                {
                    code.AppendLine($"        /// {line.TrimEnd()}");
                }
            code.AppendLine($"        /// </summary>");
            }
            code.AppendLine($"        public {type} {propName} {{ get; set; }}");
            code.AppendLine();
        }
        code.AppendLine("    }");
        code.AppendLine();
        // Config accessor class
        code.AppendLine($"    public partial class {table.Name}Config : BaseConfigData<{table.Name}Data>");
        code.AppendLine("    {");
        code.AppendLine($"        /// <summary>");
        code.AppendLine($"        /// Initializes and loads binary data file");
        code.AppendLine($"        /// </summary>");
        code.AppendLine($"        public static void Initialize()");
        code.AppendLine($"        {{");
        code.AppendLine($"            string binaryPath = System.IO.Path.Combine(UnityEngine.Application.streamingAssetsPath, \"ConfigData\", \"{table.Name}.data\");");
        code.AppendLine($"            Initialize(binaryPath);");
        code.AppendLine($"        }}");
        code.AppendLine();
        code.AppendLine($"        /// <summary>");
        code.AppendLine($"        /// Custom post-initialization logic (optional, see .ext.cs)");
        code.AppendLine($"        /// </summary>");
        code.AppendLine($"        static partial void PostInitialize();");
        code.AppendLine("        // You can add your custom logic in the ext file");
        code.AppendLine("    }");
        code.AppendLine("}");
        return code.ToString();
    }

    private string GenerateExtConfigDataClass(DataTable table, CodeGeneration config)
    {
        var code = new StringBuilder();
        code.AppendLine("using System;");
        code.AppendLine("using System.Collections.Generic;");
        code.AppendLine("using System.Linq;");
        code.AppendLine();
        code.AppendLine($"namespace {config.Namespace}");
        code.AppendLine("{");
        code.AppendLine($"    /// <summary>");
        code.AppendLine($"    /// {table.Name}Config extension class");
        code.AppendLine($"    /// Add your custom logic and methods here");
        code.AppendLine($"    /// This file is NOT overwritten during build");
        code.AppendLine($"    /// </summary>");
        code.AppendLine($"    public partial class {table.Name}Config : BaseConfigData<{table.Name}Data>");
        code.AppendLine("    {");
        code.AppendLine("        // Add your custom methods here");
        code.AppendLine();
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
        for (int i = 0; i < table.Fields.Count; i++)
        {
            var field = table.Fields[i];
            writer.Write(field.Name);
            writer.Write((int)field.Type);
        }

        // Write data rows
        for (int rowIdx = 0; rowIdx < table.Rows.Count; rowIdx++)
        {
            var row = table.Rows[rowIdx];
            for (int colIdx = 0; colIdx < table.Fields.Count; colIdx++)
            {
                var field = table.Fields[colIdx];
                string value = (colIdx < row.Values.Count && !string.IsNullOrEmpty(row.Values[colIdx])) ? row.Values[colIdx] : GetDefaultValueForFieldType(field.Type);
                WriteBinaryValue(writer, field.Type, value);
            }
        }

        return stream.ToArray();
    }

    private string GetDefaultValueForFieldType(FieldType type)
    {
        return type switch
        {
            FieldType.String => "",
            FieldType.Int => "0",
            FieldType.Long => "0",
            FieldType.Float => "0",
            FieldType.Bool => "false",
            FieldType.Enum => "0",
            FieldType.DateTime => "0001-01-01 00:00:00", // DateTime.MinValue
            _ => ""
        };
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

        var json = JsonConvert.SerializeObject(index, Formatting.Indented);
        var filePath = Path.Combine(outputPath, "index.json");
        await File.WriteAllTextAsync(filePath, json);
    }

    private void WriteBinaryValue(BinaryWriter writer, FieldType type, string value)
    {
        switch (type)
        {
            case FieldType.String:
                writer.Write(value ?? "");
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
                writer.Write(ParseBool(value));
                break;
            case FieldType.Enum:
                if (int.TryParse(value, out var enumValue))
                    writer.Write(enumValue);
                else
                    writer.Write(0);
                break;
            case FieldType.DateTime:
                // Always parse using yyyy-MM-dd HH:mm:ss
                if (DateTime.TryParseExact(value, "yyyy-MM-dd HH:mm:ss", null, System.Globalization.DateTimeStyles.None, out var dtValue))
                    writer.Write(dtValue.Ticks);
                else
                    writer.Write(0L);
                break;
        }
    }

    private object? ConvertValue(FieldType type, string value)
    {
        if (string.IsNullOrEmpty(value))
            return null;

        return type switch
        {
            FieldType.String => value,
            FieldType.Int => int.TryParse(value, out var intVal) ? intVal : 0,
            FieldType.Long => long.TryParse(value, out var longVal) ? longVal : 0L,
            FieldType.Float => float.TryParse(value, out var floatVal) ? floatVal : 0.0f,
            FieldType.Bool => ParseBool(value),
            FieldType.Enum => int.TryParse(value, out var enumVal) ? enumVal : 0,
            FieldType.DateTime => DateTime.TryParse(value, out var dtVal) ? dtVal : (DateTime?)null,
            _ => value
        };
    }

    private bool ParseBool(string value)
    {
        return value.Trim().ToLower() switch
        {
            "1" or "true" => true,
            "0" or "false" => false,
            _ => false
        };
    }

    private string GetCSharpType(FieldType type, string? enumType = null)
    {
        return type switch
        {
            FieldType.String => "string",
            FieldType.Int => "int",
            FieldType.Long => "long",
            FieldType.Float => "float",
            FieldType.Bool => "bool",
            FieldType.Enum => enumType ?? "int", // Use enum type name if provided
            FieldType.DateTime => "DateTime",
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
            "DateTime" => "DateTime?",
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

    private string ToPascalCase(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;
        
        // Handle cases with separators (underscore, hyphen, space)
        if (input.Contains('_') || input.Contains('-') || input.Contains(' '))
        {
            var words = input.Split(new char[] { '_', '-', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var result = new StringBuilder();
            
            foreach (var word in words)
            {
                if (word.Length > 0)
                {
                    result.Append(char.ToUpper(word[0]));
                    if (word.Length > 1)
                        result.Append(word.Substring(1).ToLower());
                }
            }
            
            return result.ToString();
        }
        
        // Handle single word - just capitalize first letter, keep rest as is
        return char.ToUpper(input[0]) + input.Substring(1);
    }

    // 1. In the code generation logic, generate the BaseConfigData.cs file with the following content:
    private string GenerateBaseConfigDataClass(CodeGeneration config)
    {
        var code = new StringBuilder();
        code.AppendLine("using System;");
        code.AppendLine("using System.Collections.Generic;");
        code.AppendLine("using System.IO;");
        code.AppendLine("using System.Reflection;");
        code.AppendLine("using System.Linq;");
        code.AppendLine("using UnityEngine;");
        code.AppendLine("#if UNITY_WEBGL && !UNITY_EDITOR");
        code.AppendLine("using System.Threading.Tasks;");
        code.AppendLine("using UnityEngine.Networking;");
        code.AppendLine("#endif");
        code.AppendLine();
        code.AppendLine($"namespace {config.Namespace}");
        code.AppendLine("{");
        code.AppendLine("    public abstract class BaseConfigData<T> where T : new()\n    {");
        code.AppendLine("        protected static Dictionary<int, T> _dataById;");
        code.AppendLine("        protected static List<T> _allData;");
        code.AppendLine("        protected static bool _isInitialized = false;");
        code.AppendLine();
        code.AppendLine("        public static void Initialize(string binaryPath)");
        code.AppendLine("        {");
        code.AppendLine("            if (_isInitialized) return;");
        code.AppendLine("#if UNITY_WEBGL && !UNITY_EDITOR");
        code.AppendLine("            // WebGL requires async initialization");
        code.AppendLine("            _ = InitializeAsync(binaryPath);");
        code.AppendLine("#else");
        code.AppendLine("            LoadFromBinary(binaryPath);");
        code.AppendLine("            _isInitialized = true;");
        code.AppendLine("            CallPostInitialize();");
        code.AppendLine("#endif");
        code.AppendLine("        }");
        code.AppendLine();
        code.AppendLine("#if UNITY_WEBGL && !UNITY_EDITOR");
        code.AppendLine("        public static async Task InitializeAsync(string binaryPath)");
        code.AppendLine("        {");
        code.AppendLine("            if (_isInitialized) return;");
        code.AppendLine();
        code.AppendLine("            try");
        code.AppendLine("            {");
        code.AppendLine("                await LoadFromBinaryAsync(binaryPath);");
        code.AppendLine("                _isInitialized = true;");
        code.AppendLine("                CallPostInitialize();");
        code.AppendLine("                Debug.Log($\"[BaseConfigData] {typeof(T).Name} loaded successfully from {binaryPath}\");");
        code.AppendLine("            }");
        code.AppendLine("            catch (Exception e)");
        code.AppendLine("            {");
        code.AppendLine("                Debug.LogWarning($\"[BaseConfigData] Failed to load {typeof(T).Name} from {binaryPath}: {e.Message}\");");
        code.AppendLine("                // Initialize empty collections to prevent null reference");
        code.AppendLine("                _allData = new List<T>();");
        code.AppendLine("                _dataById = new Dictionary<int, T>();");
        code.AppendLine("                _isInitialized = true;");
        code.AppendLine("                CallPostInitialize();");
        code.AppendLine("            }");
        code.AppendLine("        }");
        code.AppendLine("#endif");
        code.AppendLine();
        code.AppendLine("        public static T GetById(int id)");
        code.AppendLine("        {");
        code.AppendLine("            if (!_isInitialized) throw new Exception(\"Not initialized\");");
        code.AppendLine("            if (_dataById.TryGetValue(id, out var value))");
        code.AppendLine("                return value;");
        code.AppendLine("            return default!;");
        code.AppendLine("        }");
        code.AppendLine();
        code.AppendLine("        public static List<T> GetAll()");
        code.AppendLine("        {");
        code.AppendLine("            if (!_isInitialized) throw new Exception(\"Not initialized\");");
        code.AppendLine("            return _allData ?? new List<T>();");
        code.AppendLine("        }");
        code.AppendLine();
        code.AppendLine("        public static int Count => _allData?.Count ?? 0;");
        code.AppendLine();
        code.AppendLine("        public static void Reload(string binaryPath)");
        code.AppendLine("        {");
        code.AppendLine("            _isInitialized = false;");
        code.AppendLine("            Initialize(binaryPath);");
        code.AppendLine("        }");
        code.AppendLine();
        code.AppendLine("        private static void CallPostInitialize()");
        code.AppendLine("        {");
        code.AppendLine("            // Call PostInitialize method if it exists on the concrete Config class");
        code.AppendLine("            // We need to find the actual Config class that inherits from BaseConfigData<T>");
        code.AppendLine("            var assemblies = new[] { Assembly.GetExecutingAssembly(), typeof(T).Assembly };");
        code.AppendLine("            foreach (var assembly in assemblies.Distinct())");
        code.AppendLine("            {");
        code.AppendLine("                if (assembly != null)");
        code.AppendLine("                {");
        code.AppendLine("                    try");
        code.AppendLine("                    {");
        code.AppendLine("                        var configTypes = assembly.GetTypes()");
        code.AppendLine("                            .Where(type => type.IsClass && !type.IsAbstract)");
        code.AppendLine("                            .Where(type => type.BaseType != null && type.BaseType.IsGenericType)");
        code.AppendLine("                            .Where(type => type.BaseType.GetGenericTypeDefinition() == typeof(BaseConfigData<>))");
        code.AppendLine("                            .Where(type => type.BaseType.GetGenericArguments()[0] == typeof(T));");
        code.AppendLine("                        ");
        code.AppendLine("                        foreach (var configType in configTypes)");
        code.AppendLine("                        {");
        code.AppendLine("                            var postInitMethod = configType.GetMethod(\"PostInitialize\", BindingFlags.NonPublic | BindingFlags.Static);");
        code.AppendLine("                            if (postInitMethod != null)");
        code.AppendLine("                            {");
        code.AppendLine("                                try");
        code.AppendLine("                                {");
        code.AppendLine("                                    postInitMethod.Invoke(null, null);");
        code.AppendLine("                                    return; // Found and called, exit");
        code.AppendLine("                                }");
        code.AppendLine("                                catch (Exception e)");
        code.AppendLine("                                {");
        code.AppendLine("                                    Debug.LogWarning($\"[BaseConfigData] PostInitialize failed for {configType.Name}: {e.Message}\");");
        code.AppendLine("                                }");
        code.AppendLine("                            }");
        code.AppendLine("                        }");
        code.AppendLine("                    }");
        code.AppendLine("                    catch (Exception e)");
        code.AppendLine("                    {");
        code.AppendLine("                        Debug.LogWarning($\"[BaseConfigData] Failed to search for PostInitialize in assembly {assembly.FullName}: {e.Message}\");");
        code.AppendLine("                    }");
        code.AppendLine("                }");
        code.AppendLine("            }");
        code.AppendLine("        }");
        code.AppendLine();
        code.AppendLine("        private static DateTime ReadValidDateTime(BinaryReader reader)");
        code.AppendLine("        {");
        code.AppendLine("            long ticks = reader.ReadInt64();");
        code.AppendLine("            if (ticks < DateTime.MinValue.Ticks || ticks > DateTime.MaxValue.Ticks)");
        code.AppendLine("            {");
        code.AppendLine("                // Return default DateTime for invalid ticks");
        code.AppendLine("                return DateTime.MinValue;");
        code.AppendLine("            }");
        code.AppendLine("            return new DateTime(ticks);");
        code.AppendLine("        }");
        code.AppendLine();
        code.AppendLine("        protected static void LoadFromBinary(string binaryPath)");
        code.AppendLine("        {");
        code.AppendLine("            _allData = new List<T>();");
        code.AppendLine("            _dataById = new Dictionary<int, T>();");
        code.AppendLine();
        code.AppendLine("            if (!File.Exists(binaryPath))");
        code.AppendLine("                throw new FileNotFoundException($\"Binary file not found: {binaryPath}\");");
        code.AppendLine();
        code.AppendLine("            using var stream = File.OpenRead(binaryPath);");
        code.AppendLine("            using var reader = new BinaryReader(stream);");
        code.AppendLine();
        code.AppendLine("            try");
        code.AppendLine("            {");
        code.AppendLine("                // Check if we have enough bytes for header");
        code.AppendLine("                if (stream.Length < 8)");
        code.AppendLine("                    throw new InvalidDataException($\"Binary file too small for header: {stream.Length} bytes\");");
        code.AppendLine();
        code.AppendLine("                int fieldCount = reader.ReadInt32();");
        code.AppendLine("                int rowCount = reader.ReadInt32();");
        code.AppendLine();
        code.AppendLine("                // Validate field count");
        code.AppendLine("                if (fieldCount < 0 || fieldCount > 1000)");
        code.AppendLine("                    throw new InvalidDataException($\"Invalid field count: {fieldCount}\");");
        code.AppendLine();
        code.AppendLine("                // Validate row count");
        code.AppendLine("                if (rowCount < 0 || rowCount > 1000000)");
        code.AppendLine("                    throw new InvalidDataException($\"Invalid row count: {rowCount}\");");
        code.AppendLine();
        code.AppendLine("                var fields = new (string name, int type)[fieldCount];");
        code.AppendLine("                for (int i = 0; i < fieldCount; i++)");
        code.AppendLine("                {");
        code.AppendLine("                    if (stream.Position >= stream.Length)");
        code.AppendLine("                        throw new EndOfStreamException($\"Unexpected end of stream while reading field {i}. Position: {stream.Position}, Length: {stream.Length}\");");
        code.AppendLine("                    fields[i] = (reader.ReadString(), reader.ReadInt32());");
        code.AppendLine("                }");
        code.AppendLine();
        code.AppendLine("                var props = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);");
        code.AppendLine();
        code.AppendLine("                for (int row = 0; row < rowCount; row++)");
        code.AppendLine("                {");
        code.AppendLine("                    if (stream.Position >= stream.Length)");
        code.AppendLine("                        throw new EndOfStreamException($\"Unexpected end of stream while reading row {row}\");");
        code.AppendLine();
        code.AppendLine("                    var item = new T();");
        code.AppendLine("                    for (int col = 0; col < fieldCount; col++)");
        code.AppendLine("                    {");
        code.AppendLine("                        if (stream.Position >= stream.Length)");
        code.AppendLine("                            throw new EndOfStreamException($\"Unexpected end of stream while reading row {row}, column {col}. Position: {stream.Position}, Length: {stream.Length}\");");
        code.AppendLine("                        var (fieldName, fieldType) = fields[col];");
        code.AppendLine("                        var prop = props.FirstOrDefault(p => p.Name.Equals(fieldName, StringComparison.OrdinalIgnoreCase));");
        code.AppendLine("                        if (prop != null)");
        code.AppendLine("                        {");
        code.AppendLine("                            object value = fieldType switch");
        code.AppendLine("                            {");
        code.AppendLine("                                0 => (object)reader.ReadString(),");
        code.AppendLine("                                1 => (object)reader.ReadInt32(),");
        code.AppendLine("                                2 => (object)reader.ReadInt64(),");
        code.AppendLine("                                3 => (object)reader.ReadSingle(),");
        code.AppendLine("                                4 => (object)reader.ReadBoolean(),");
        code.AppendLine("                                5 => (object)ReadValidDateTime(reader),");
        code.AppendLine("                                6 => (object)reader.ReadInt32(),");
        code.AppendLine("                                _ => (object)reader.ReadString()");
        code.AppendLine("                            };");
        code.AppendLine("                            // Type-safe assignment for DateTime and other types");
        code.AppendLine("                            var propType = prop.PropertyType;");
        code.AppendLine("                            if (fieldType == 5 && propType == typeof(int))");
        code.AppendLine("                            {");
        code.AppendLine("                                value = (int)(((DateTime)value).Ticks / TimeSpan.TicksPerSecond);");
        code.AppendLine("                            }");
        code.AppendLine("                            else if (fieldType == 5 && propType == typeof(long))");
        code.AppendLine("                            {");
        code.AppendLine("                                value = ((DateTime)value).Ticks;");
        code.AppendLine("                            }");
        code.AppendLine("                            else if (fieldType == 5 && propType == typeof(string))");
        code.AppendLine("                            {");
        code.AppendLine("                                value = ((DateTime)value).ToString(\"yyyy-MM-dd HH:mm:ss\");");
        code.AppendLine("                            }");
        code.AppendLine("                            else if (fieldType != 5 && propType == typeof(DateTime))");
        code.AppendLine("                            {");
        code.AppendLine("                                if (value is long longValue)");
        code.AppendLine("                                    value = new DateTime(longValue);");
        code.AppendLine("                                else if (value is int intValue)");
        code.AppendLine("                                    value = new DateTime(intValue * TimeSpan.TicksPerSecond);");
        code.AppendLine("                                else if (value is string strValue && DateTime.TryParseExact(strValue, \"yyyy-MM-dd HH:mm:ss\", null, System.Globalization.DateTimeStyles.None, out var dt))");
        code.AppendLine("                                    value = dt;");
        code.AppendLine("                            }");
        code.AppendLine("                            // Robust type conversion");
        code.AppendLine("                            if (value != null && propType != value.GetType())");
        code.AppendLine("                            {");
        code.AppendLine("                                if (propType.IsEnum)");
        code.AppendLine("                                {");
        code.AppendLine("                                    if (value is string str && System.Enum.TryParse(propType, str, out var enumVal))");
        code.AppendLine("                                        value = enumVal;");
        code.AppendLine("                                    else if (value is int intVal)");
        code.AppendLine("                                        value = System.Enum.ToObject(propType, intVal);");
        code.AppendLine("                                }");
        code.AppendLine("                                else");
        code.AppendLine("                                    value = System.Convert.ChangeType(value, propType);");
        code.AppendLine("                            }");
        code.AppendLine("                            if (value != null)");
        code.AppendLine("                                prop.SetValue(item, value);");
        code.AppendLine("                        }");
        code.AppendLine("                        else");
        code.AppendLine("                        {");
        code.AppendLine("                            _ = fieldType switch");
        code.AppendLine("                            {");
        code.AppendLine("                                0 => (object)reader.ReadString(),");
        code.AppendLine("                                1 => (object)reader.ReadInt32(),");
        code.AppendLine("                                2 => (object)reader.ReadInt64(),");
        code.AppendLine("                                3 => (object)reader.ReadSingle(),");
        code.AppendLine("                                4 => (object)reader.ReadBoolean(),");
        code.AppendLine("                                5 => (object)ReadValidDateTime(reader),");
        code.AppendLine("                                6 => (object)reader.ReadInt32(),");
        code.AppendLine("                                _ => (object)reader.ReadString()");
        code.AppendLine("                            };");
        code.AppendLine("                        }");
        code.AppendLine("                    }");
        code.AppendLine("                    _allData.Add(item);");
        code.AppendLine();
        code.AppendLine("                    // Index by id property if exists");
        code.AppendLine("                    var idProp = props.FirstOrDefault(p => p.Name.Equals(\"id\", StringComparison.OrdinalIgnoreCase));");
        code.AppendLine("                    if (idProp != null)");
        code.AppendLine("                    {");
        code.AppendLine("                        int id = (int)(idProp.GetValue(item) ?? 0);");
        code.AppendLine("                        if (id > 0)");
        code.AppendLine("                            _dataById[id] = item;");
        code.AppendLine("                    }");
        code.AppendLine("                }");
        code.AppendLine("            }");
        code.AppendLine("            catch (EndOfStreamException ex)");
        code.AppendLine("            {");
        code.AppendLine("                throw new InvalidDataException($\"Binary file is corrupted or incomplete: {ex.Message}\", ex);");
        code.AppendLine("            }");
        code.AppendLine("            catch (Exception ex)");
        code.AppendLine("            {");
        code.AppendLine("                throw new InvalidDataException($\"Error reading binary file: {ex.Message}\", ex);");
        code.AppendLine("            }");
        code.AppendLine("        }");
        code.AppendLine();
        code.AppendLine("#if UNITY_WEBGL && !UNITY_EDITOR");
        code.AppendLine("        protected static async Task LoadFromBinaryAsync(string binaryPath)");
        code.AppendLine("        {");
        code.AppendLine("            _allData = new List<T>();");
        code.AppendLine("            _dataById = new Dictionary<int, T>();");
        code.AppendLine();
        code.AppendLine("            // Convert Unity StreamingAssets path to WebGL URL");
        code.AppendLine("            string url = binaryPath;");
        code.AppendLine("            if (!url.StartsWith(\"http\"))");
        code.AppendLine("            {");
        code.AppendLine("                // Convert local path to StreamingAssets URL for WebGL");
        code.AppendLine("                var fileName = Path.GetFileName(binaryPath);");
        code.AppendLine("                url = Path.Combine(Application.streamingAssetsPath, \"ConfigData\", fileName);");
        code.AppendLine("            }");
        code.AppendLine();
        code.AppendLine("            using UnityWebRequest www = UnityWebRequest.Get(url);");
        code.AppendLine("            await SendWebRequest(www);");
        code.AppendLine();
        code.AppendLine("            if (www.result != UnityWebRequest.Result.Success)");
        code.AppendLine("            {");
        code.AppendLine("                throw new FileNotFoundException($\"Failed to load binary file from {url}: {www.error}\");");
        code.AppendLine("            }");
        code.AppendLine();
        code.AppendLine("            byte[] data = www.downloadHandler.data;");
        code.AppendLine("            using var stream = new MemoryStream(data);");
        code.AppendLine("            using var reader = new BinaryReader(stream);");
        code.AppendLine();
        code.AppendLine("            // Same binary reading logic as LoadFromBinary");
        code.AppendLine("            // ... (copy the entire LoadFromBinary logic here)");
        code.AppendLine("            try");
        code.AppendLine("            {");
        code.AppendLine("                if (stream.Length < 8)");
        code.AppendLine("                    throw new InvalidDataException($\"Binary file too small for header: {stream.Length} bytes\");");
        code.AppendLine();
        code.AppendLine("                int fieldCount = reader.ReadInt32();");
        code.AppendLine("                int rowCount = reader.ReadInt32();");
        code.AppendLine();
        code.AppendLine("                if (fieldCount < 0 || fieldCount > 1000)");
        code.AppendLine("                    throw new InvalidDataException($\"Invalid field count: {fieldCount}\");");
        code.AppendLine();
        code.AppendLine("                if (rowCount < 0 || rowCount > 1000000)");
        code.AppendLine("                    throw new InvalidDataException($\"Invalid row count: {rowCount}\");");
        code.AppendLine();
        code.AppendLine("                var fields = new (string name, int type)[fieldCount];");
        code.AppendLine("                for (int i = 0; i < fieldCount; i++)");
        code.AppendLine("                {");
        code.AppendLine("                    if (stream.Position >= stream.Length)");
        code.AppendLine("                        throw new EndOfStreamException($\"Unexpected end of stream while reading field {i}\");");
        code.AppendLine("                    fields[i] = (reader.ReadString(), reader.ReadInt32());");
        code.AppendLine("                }");
        code.AppendLine();
        code.AppendLine("                var props = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);");
        code.AppendLine();
        code.AppendLine("                for (int row = 0; row < rowCount; row++)");
        code.AppendLine("                {");
        code.AppendLine("                    if (stream.Position >= stream.Length)");
        code.AppendLine("                        throw new EndOfStreamException($\"Unexpected end of stream while reading row {row}\");");
        code.AppendLine();
        code.AppendLine("                    var item = new T();");
        code.AppendLine("                    for (int col = 0; col < fieldCount; col++)");
        code.AppendLine("                    {");
        code.AppendLine("                        var (fieldName, fieldType) = fields[col];");
        code.AppendLine("                        var prop = props.FirstOrDefault(p => p.Name.Equals(fieldName, StringComparison.OrdinalIgnoreCase));");
        code.AppendLine();
        code.AppendLine("                        if (prop != null)");
        code.AppendLine("                        {");
        code.AppendLine("                            object value = fieldType switch");
        code.AppendLine("                            {");
        code.AppendLine("                                0 => reader.ReadString(),");
        code.AppendLine("                                1 => reader.ReadInt32(),");
        code.AppendLine("                                2 => reader.ReadInt64(),");
        code.AppendLine("                                3 => reader.ReadSingle(),");
        code.AppendLine("                                4 => reader.ReadBoolean(),");
        code.AppendLine("                                5 => ReadValidDateTime(reader),");
        code.AppendLine("                                6 => reader.ReadInt32(),");
        code.AppendLine("                                _ => reader.ReadString()");
        code.AppendLine("                            };");
        code.AppendLine();
        code.AppendLine("                            // Type conversion logic (same as LoadFromBinary)");
        code.AppendLine("                            var propType = prop.PropertyType;");
        code.AppendLine("                            if (fieldType == 5 && propType == typeof(int))");
        code.AppendLine("                            {");
        code.AppendLine("                                value = (int)(((DateTime)value).Ticks / TimeSpan.TicksPerSecond);");
        code.AppendLine("                            }");
        code.AppendLine("                            else if (fieldType == 5 && propType == typeof(long))");
        code.AppendLine("                            {");
        code.AppendLine("                                value = ((DateTime)value).Ticks;");
        code.AppendLine("                            }");
        code.AppendLine("                            else if (fieldType == 5 && propType == typeof(string))");
        code.AppendLine("                            {");
        code.AppendLine("                                value = ((DateTime)value).ToString(\"yyyy-MM-dd HH:mm:ss\");");
        code.AppendLine("                            }");
        code.AppendLine("                            else if (fieldType != 5 && propType == typeof(DateTime))");
        code.AppendLine("                            {");
        code.AppendLine("                                if (value is long longValue)");
        code.AppendLine("                                    value = new DateTime(longValue);");
        code.AppendLine("                                else if (value is int intValue)");
        code.AppendLine("                                    value = new DateTime(intValue * TimeSpan.TicksPerSecond);");
        code.AppendLine("                                else if (value is string strValue && DateTime.TryParseExact(strValue, \"yyyy-MM-dd HH:mm:ss\", null, System.Globalization.DateTimeStyles.None, out var dt))");
        code.AppendLine("                                    value = dt;");
        code.AppendLine("                            }");
        code.AppendLine();
        code.AppendLine("                            if (value != null && propType != value.GetType())");
        code.AppendLine("                            {");
        code.AppendLine("                                if (propType.IsEnum)");
        code.AppendLine("                                {");
        code.AppendLine("                                    if (value is string str && System.Enum.TryParse(propType, str, out var enumVal))");
        code.AppendLine("                                        value = enumVal;");
        code.AppendLine("                                    else if (value is int intVal)");
        code.AppendLine("                                        value = System.Enum.ToObject(propType, intVal);");
        code.AppendLine("                                }");
        code.AppendLine("                                else");
        code.AppendLine("                                    value = System.Convert.ChangeType(value, propType);");
        code.AppendLine("                            }");
        code.AppendLine("                            if (value != null)");
        code.AppendLine("                                prop.SetValue(item, value);");
        code.AppendLine("                        }");
        code.AppendLine("                        else");
        code.AppendLine("                        {");
        code.AppendLine("                            // Skip unknown field");
        code.AppendLine("                            _ = fieldType switch");
        code.AppendLine("                            {");
        code.AppendLine("                                0 => (object)reader.ReadString(),");
        code.AppendLine("                                1 => (object)reader.ReadInt32(),");
        code.AppendLine("                                2 => (object)reader.ReadInt64(),");
        code.AppendLine("                                3 => (object)reader.ReadSingle(),");
        code.AppendLine("                                4 => (object)reader.ReadBoolean(),");
        code.AppendLine("                                5 => (object)ReadValidDateTime(reader),");
        code.AppendLine("                                6 => (object)reader.ReadInt32(),");
        code.AppendLine("                                _ => (object)reader.ReadString()");
        code.AppendLine("                            };");
        code.AppendLine("                        }");
        code.AppendLine("                    }");
        code.AppendLine("                    _allData.Add(item);");
        code.AppendLine();
        code.AppendLine("                    // Index by id property if exists");
        code.AppendLine("                    var idProp = props.FirstOrDefault(p => p.Name.Equals(\"id\", StringComparison.OrdinalIgnoreCase));");
        code.AppendLine("                    if (idProp != null)");
        code.AppendLine("                    {");
        code.AppendLine("                        int id = (int)(idProp.GetValue(item) ?? 0);");
        code.AppendLine("                        if (id > 0)");
        code.AppendLine("                            _dataById[id] = item;");
        code.AppendLine("                    }");
        code.AppendLine("                }");
        code.AppendLine("            }");
        code.AppendLine("            catch (EndOfStreamException ex)");
        code.AppendLine("            {");
        code.AppendLine("                throw new InvalidDataException($\"Binary file is corrupted or incomplete: {ex.Message}\", ex);");
        code.AppendLine("            }");
        code.AppendLine("            catch (Exception ex)");
        code.AppendLine("            {");
        code.AppendLine("                throw new InvalidDataException($\"Error reading binary file: {ex.Message}\", ex);");
        code.AppendLine("            }");
        code.AppendLine("        }");
        code.AppendLine();
        code.AppendLine("        private static Task SendWebRequest(UnityWebRequest www)");
        code.AppendLine("        {");
        code.AppendLine("            var tcs = new TaskCompletionSource<bool>();");
        code.AppendLine("            var operation = www.SendWebRequest();");
        code.AppendLine();
        code.AppendLine("            operation.completed += _ =>");
        code.AppendLine("            {");
        code.AppendLine("                if (www.result == UnityWebRequest.Result.Success)");
        code.AppendLine("                    tcs.SetResult(true);");
        code.AppendLine("                else");
        code.AppendLine("                    tcs.SetException(new Exception($\"WebRequest failed: {www.error}\"));");
        code.AppendLine("            };");
        code.AppendLine();
        code.AppendLine("            return tcs.Task;");
        code.AppendLine("        }");
        code.AppendLine("#endif");
        code.AppendLine("    }");
        code.AppendLine("}");
        return code.ToString();
    }
} 