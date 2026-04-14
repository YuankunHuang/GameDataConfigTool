using System.Text.Json;

namespace GameDataTool.Core;

public sealed class ToolConfig
{
    public string      ExcelPath  { get; set; } = "excels/";
    public string      EnumPath   { get; set; } = "EnumTypes";
    public bool        CleanBeforeGenerate { get; set; } = true;
    public OutputPaths OutputPaths { get; set; } = new();
    public Generators  Generators  { get; set; } = new();
    public CodeGen     CodeGeneration { get; set; } = new();
    public Validation  Validation  { get; set; } = new();

    private const string ProfilePath = "config/profile.json";
    private const string ConfigDir   = "config";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private static string? _projectRootOverride;

    /// <summary>
    /// Set by --project-root CLI argument.
    /// When set, excelPath and outputPaths are resolved relative to this directory.
    /// </summary>
    public static void SetProjectRoot(string projectRoot)
    {
        _projectRootOverride = Path.GetFullPath(projectRoot);
    }

    /// <summary>
    /// Returns the project root directory.
    /// If set via --project-root, returns that path.
    /// Otherwise returns the parent of CWD (backward compatible: tool lives in a subdirectory of the project).
    /// </summary>
    public static string GetProjectRoot()
    {
        if (_projectRootOverride != null)
            return _projectRootOverride;
        var toolDir = Directory.GetCurrentDirectory();
        return Path.GetFullPath(Path.Combine(toolDir, ".."));
    }

    /// <summary>
    /// Resolves excelPath to an absolute path.
    /// If --project-root is set, resolves relative to projectRoot.
    /// Otherwise resolves relative to CWD (backward compatible).
    /// </summary>
    public string ResolveExcelPath()
    {
        if (_projectRootOverride != null)
            return Path.GetFullPath(ExcelPath, _projectRootOverride);
        return Path.GetFullPath(ExcelPath, Directory.GetCurrentDirectory());
    }

    /// <summary>
    /// Resolves enumPath to an absolute path (under the resolved excelPath).
    /// </summary>
    public string ResolveEnumPath(string resolvedExcelPath)
    {
        if (Path.IsPathRooted(EnumPath)) return EnumPath;
        return Path.Combine(resolvedExcelPath, EnumPath);
    }

    /// <summary>
    /// Resolves an output sub-path relative to project root.
    /// </summary>
    public string ResolveOutputPath(string subPath)
    {
        return Path.GetFullPath(subPath, GetProjectRoot());
    }

    /// <summary>
    /// Loads config via profile resolution:
    ///   1. CLI override:  --profile cocos  →  config/cocos.json
    ///   2. profile.json:  { "active": "cocos" }  →  config/cocos.json
    ///   3. Fallback:      config/settings.json
    ///
    /// When configPath is provided directly (via --config), profile resolution is bypassed.
    /// </summary>
    public static ToolConfig Load(string? profileOverride = null, string? configPath = null)
    {
        var configFile = configPath != null
            ? Path.GetFullPath(configPath)
            : ResolveConfigFile(profileOverride ?? ReadActiveProfile());

        if (!File.Exists(configFile))
            throw new FileNotFoundException($"Config not found: {configFile}");

        var json = File.ReadAllText(configFile);
        var cfg = JsonSerializer.Deserialize<ToolConfig>(json, JsonOpts)
            ?? throw new InvalidOperationException($"Failed to deserialize: {configFile}");

        cfg.Validate();

        var label = configPath != null ? configPath : (profileOverride ?? "default");
        Log.Info($"Profile: {label} ({configFile})");
        return cfg;
    }

    private static string? ReadActiveProfile()
    {
        if (!File.Exists(ProfilePath)) return null;

        var json = File.ReadAllText(ProfilePath);
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.TryGetProperty("active", out var prop))
            return prop.GetString();
        return null;
    }

    private static string ResolveConfigFile(string? profileName)
    {
        if (!string.IsNullOrWhiteSpace(profileName))
        {
            var named = Path.Combine(ConfigDir, $"{profileName}.json");
            if (File.Exists(named)) return named;
        }
        return Path.Combine(ConfigDir, "settings.json");
    }

    private void Validate()
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(ExcelPath)) errors.Add("excelPath is empty");
        if (string.IsNullOrWhiteSpace(EnumPath))   errors.Add("enumPath is empty");

        if (Generators.EnableJson && string.IsNullOrWhiteSpace(OutputPaths.Json))
            errors.Add("outputPaths.json is empty but JSON is enabled");
        if (Generators.EnableBinary && string.IsNullOrWhiteSpace(OutputPaths.Binary))
            errors.Add("outputPaths.binary is empty but binary is enabled");
        if (Generators.EnableCode && string.IsNullOrWhiteSpace(OutputPaths.Code))
            errors.Add("outputPaths.code is empty but code is enabled");

        var lang = CodeGeneration.Language.ToLowerInvariant();
        if (Generators.EnableCode && lang is "csharp" or "cs" && string.IsNullOrWhiteSpace(CodeGeneration.Namespace))
            errors.Add("codeGeneration.namespace is required for C#");

        if (errors.Count > 0)
            throw new InvalidOperationException("Config validation failed:\n" + string.Join('\n', errors));
    }
}

public sealed class OutputPaths
{
    public string Json   { get; set; } = "output/json/";
    public string Binary { get; set; } = "output/binary/";
    public string Code   { get; set; } = "output/code/";
}

public sealed class Generators
{
    public bool EnableJson   { get; set; } = true;
    public bool EnableBinary { get; set; } = true;
    public bool EnableCode   { get; set; } = true;
}

public sealed class CodeGen
{
    public string Namespace    { get; set; } = "GameData";
    public string Language     { get; set; } = "csharp";
    public bool   GenerateEnum { get; set; } = true;
}

public sealed class Validation
{
    public bool EnableTypeCheck             { get; set; } = true;
    public bool EnforceNonNullableColumns   { get; set; } = true;
}
