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
    /// Must be set via --project-root; throws if not configured.
    /// </summary>
    public static string GetProjectRoot()
    {
        return _projectRootOverride
            ?? throw new InvalidOperationException("--project-root is required. Use the generated build_config script or pass --project-root explicitly.");
    }

    /// <summary>
    /// Resolves excelPath to an absolute path relative to projectRoot.
    /// </summary>
    public string ResolveExcelPath()
    {
        return Path.GetFullPath(ExcelPath, GetProjectRoot());
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
    /// Loads config from the given file path (provided via --config).
    /// </summary>
    public static ToolConfig Load(string configPath)
    {
        var configFile = Path.GetFullPath(configPath);

        if (!File.Exists(configFile))
            throw new FileNotFoundException($"Config not found: {configFile}");

        var json = File.ReadAllText(configFile);
        var cfg = JsonSerializer.Deserialize<ToolConfig>(json, JsonOpts)
            ?? throw new InvalidOperationException($"Failed to deserialize: {configFile}");

        cfg.Validate();

        Log.Info($"Config: {configFile}");
        return cfg;
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
