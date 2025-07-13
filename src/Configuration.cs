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

            // Automatically detect Unity project environment and adjust output paths
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
        // Detect if in a Unity project
        var unityProjectRoot = FindUnityProjectRoot();
        
        if (!string.IsNullOrEmpty(unityProjectRoot))
        {
            // In Unity project, output to Assets/Scripts/ConfigData under the Unity project root
            config.OutputPaths.Json = Path.Combine(unityProjectRoot, "Assets/Scripts/ConfigData/json/");
            config.OutputPaths.Binary = Path.Combine(unityProjectRoot, "Assets/Scripts/ConfigData/binary/");
            config.OutputPaths.Code = Path.Combine(unityProjectRoot, "Assets/Scripts/ConfigData/code/");
            
        }
        else
        {
            // Independent environment, output to local output directory
            config.OutputPaths.Json = "output/json/";
            config.OutputPaths.Binary = "output/binary/";
            config.OutputPaths.Code = "output/code/";
        }
    }

    private static string? FindUnityProjectRoot()
    {
        // Check if the current directory or its parent contains Unity project features
        var currentDir = Directory.GetCurrentDirectory();
        
        // Check current directory
        if (IsUnityProjectDirectory(currentDir))
            return currentDir;
            
        // Check parent directory
        var parentDir = Directory.GetParent(currentDir)?.FullName;
        if (!string.IsNullOrEmpty(parentDir) && IsUnityProjectDirectory(parentDir))
            return parentDir;
            
        // Check grandparent directory
        var grandParentDir = Directory.GetParent(parentDir)?.FullName;
        if (!string.IsNullOrEmpty(grandParentDir) && IsUnityProjectDirectory(grandParentDir))
            return grandParentDir;
            
        return null;
    }

    private static bool IsUnityProjectDirectory(string directory)
    {
        // Unity project features: Assets directory and ProjectSettings directory exist
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