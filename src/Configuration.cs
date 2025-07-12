using System.Text.Json;

namespace GameDataTool.Core.Configuration;

public class ConfigurationManager
{
    private const string CONFIG_FILE = "config/settings.json";

    public static async Task<ToolConfiguration> LoadAsync()
    {
        try
        {
            if (!File.Exists(CONFIG_FILE))
            {
                throw new FileNotFoundException($"Config file not found: {CONFIG_FILE}");
            }

            var json = await File.ReadAllTextAsync(CONFIG_FILE);
            var config = JsonSerializer.Deserialize<ToolConfiguration>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (config == null)
            {
                throw new InvalidOperationException("Config file format error");
            }

            // 自动检测Unity项目环境并调整输出路径
            AdjustOutputPathsForEnvironment(config);

            // Validate config
            ValidateConfiguration(config);

            return config;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to load config file: {ex.Message}", ex);
        }
    }

    private static void AdjustOutputPathsForEnvironment(ToolConfiguration config)
    {
        // 检测是否在Unity项目中
        var unityProjectRoot = FindUnityProjectRoot();
        
        if (!string.IsNullOrEmpty(unityProjectRoot))
        {
            // 在Unity项目中，输出到Unity项目根目录下的Assets/Scripts/ConfigData
            config.OutputPaths.Json = Path.Combine(unityProjectRoot, "Assets/Scripts/ConfigData/json/");
            config.OutputPaths.Binary = Path.Combine(unityProjectRoot, "Assets/Scripts/ConfigData/binary/");
            config.OutputPaths.Code = Path.Combine(unityProjectRoot, "Assets/Scripts/ConfigData/code/");
            
            Console.WriteLine($"🎮 检测到Unity项目环境，输出到: {unityProjectRoot}/Assets/Scripts/ConfigData/");
        }
        else
        {
            // 独立环境，输出到本地output目录
            config.OutputPaths.Json = "output/json/";
            config.OutputPaths.Binary = "output/binary/";
            config.OutputPaths.Code = "output/code/";
            
            Console.WriteLine("🛠️  独立工具环境，输出到本地 output/ 目录");
        }
    }

    private static string? FindUnityProjectRoot()
    {
        // 检测当前目录或上级目录是否存在Unity项目特征
        var currentDir = Directory.GetCurrentDirectory();
        
        // 检查当前目录
        if (IsUnityProjectDirectory(currentDir))
            return currentDir;
            
        // 检查上级目录
        var parentDir = Directory.GetParent(currentDir)?.FullName;
        if (!string.IsNullOrEmpty(parentDir) && IsUnityProjectDirectory(parentDir))
            return parentDir;
            
        // 检查上上级目录
        var grandParentDir = Directory.GetParent(parentDir)?.FullName;
        if (!string.IsNullOrEmpty(grandParentDir) && IsUnityProjectDirectory(grandParentDir))
            return grandParentDir;
            
        return null;
    }

    private static bool IsUnityProjectDirectory(string directory)
    {
        // Unity项目特征：存在Assets目录和ProjectSettings目录
        var assetsPath = Path.Combine(directory, "Assets");
        var projectSettingsPath = Path.Combine(directory, "ProjectSettings");
        
        return Directory.Exists(assetsPath) && Directory.Exists(projectSettingsPath);
    }

    private static void ValidateConfiguration(ToolConfiguration config)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(config.ExcelPath))
            errors.Add("ExcelPath cannot be empty");

        if (string.IsNullOrWhiteSpace(config.EnumPath))
            errors.Add("EnumPath cannot be empty");

        if (string.IsNullOrWhiteSpace(config.OutputPaths.Json))
            errors.Add("OutputPaths.Json cannot be empty");

        if (string.IsNullOrWhiteSpace(config.OutputPaths.Binary))
            errors.Add("OutputPaths.Binary cannot be empty");

        if (string.IsNullOrWhiteSpace(config.OutputPaths.Code))
            errors.Add("OutputPaths.Code cannot be empty");

        if (string.IsNullOrWhiteSpace(config.CodeGeneration.Namespace))
            errors.Add("CodeGeneration.Namespace cannot be empty");

        if (errors.Count > 0)
        {
            throw new InvalidOperationException($"Config file validation failed:\n{string.Join("\n", errors)}");
        }
    }
}

public class ToolConfiguration
{
    public string ExcelPath { get; set; } = string.Empty;
    public string EnumPath { get; set; } = string.Empty;
    public OutputPaths OutputPaths { get; set; } = new();
    public Generators Generators { get; set; } = new();
    public CodeGeneration CodeGeneration { get; set; } = new();
    public Validation Validation { get; set; } = new();
    public Logging Logging { get; set; } = new();
}

public class OutputPaths
{
    public string Json { get; set; } = string.Empty;
    public string Binary { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
}

public class Generators
{
    public bool EnableJson { get; set; } = true;
    public bool EnableBinary { get; set; } = true;
    public bool EnableCode { get; set; } = true;
}

public class CodeGeneration
{
    public string Namespace { get; set; } = "GameData";
    public string Language { get; set; } = "csharp";
    public bool GenerateEnum { get; set; } = true;
    public bool GenerateLoader { get; set; } = true;
}

public class Validation
{
    public bool EnableTypeCheck { get; set; } = true;
    public bool EnableRequiredFieldCheck { get; set; } = true;
}

public class Logging
{
    public string Level { get; set; } = "Info";
    public bool OutputToFile { get; set; } = false;
} 