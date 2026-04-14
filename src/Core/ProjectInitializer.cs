using System.Text.Json;
using System.Text.Json.Nodes;

namespace GameDataTool.Core;

/// <summary>
/// Handles the --setup command: reads config/setup.json and generates project-side
/// config files, directory structure, and build scripts.
/// </summary>
public static class ProjectInitializer
{
    private const string SetupConfigPath = "config/setup.json";

    private static readonly JsonSerializerOptions WriteOpts = new()
    {
        WriteIndented = true,
    };

    public static int Run()
    {
        Console.WriteLine("\n=== GameDataConfigTool Setup ===\n");

        if (!File.Exists(SetupConfigPath))
        {
            Console.WriteLine($"Error: Setup config not found: {SetupConfigPath}");
            Console.WriteLine("Please ensure config/setup.json exists in the tool directory.");
            return 1;
        }

        JsonNode setupNode;
        try
        {
            var json = File.ReadAllText(SetupConfigPath);
            setupNode = JsonNode.Parse(json, documentOptions: new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
            }) ?? throw new InvalidOperationException("setup.json is empty or null");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error reading {SetupConfigPath}: {ex.Message}");
            return 1;
        }

        // --- Setup metadata ---
        var projectRootRel  = setupNode["projectRoot"]?.GetValue<string>() ?? ".";
        var configPathRel   = setupNode["projectConfigPath"]?.GetValue<string>() ?? "config/configtool.json";
        var excelPathRel    = setupNode["excelPath"]?.GetValue<string>() ?? "excels/";
        var enumPathRel     = setupNode["enumPath"]?.GetValue<string>() ?? "EnumTypes";

        var toolDir     = Directory.GetCurrentDirectory();
        var projectRoot = Path.GetFullPath(projectRootRel, toolDir);
        var configFile  = Path.GetFullPath(configPathRel, projectRoot);

        // Compute tool-relative path for generated build scripts
        var toolRelPath = Path.GetRelativePath(projectRoot, toolDir)
            .Replace('\\', '/');

        Console.WriteLine($"Project root  : {projectRoot}");
        Console.WriteLine($"Tool path     : {toolRelPath}");
        Console.WriteLine($"Config file   : {configFile}");
        Console.WriteLine();

        var generatedFiles = new List<string>();
        var skippedFiles   = new List<string>();

        // --- 1. Generate runtime config ---
        GenerateRuntimeConfig(setupNode, configFile, configPathRel, generatedFiles, skippedFiles);

        // --- 2. Create excel directory structure ---
        CreateExcelDirectories(projectRoot, excelPathRel, enumPathRel, generatedFiles);

        // --- 3. Generate build scripts ---
        GenerateBuildScripts(projectRoot, toolRelPath, configPathRel, generatedFiles, skippedFiles);

        // --- Summary ---
        PrintSummary(generatedFiles, skippedFiles, projectRoot, configPathRel);

        return 0;
    }

    private static void GenerateRuntimeConfig(
        JsonNode setupNode,
        string configFile,
        string configPathRel,
        List<string> generated,
        List<string> skipped)
    {
        if (File.Exists(configFile))
        {
            skipped.Add(configPathRel);
            Console.WriteLine($"[skip] {configPathRel} already exists.");
            return;
        }

        // Copy all fields from setup.json except setup-only metadata keys
        var runtimeNode = new JsonObject();
        var metaKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "projectRoot", "projectConfigPath"
        };

        foreach (var kv in setupNode.AsObject())
        {
            if (metaKeys.Contains(kv.Key)) continue;
            if (kv.Key.StartsWith("_")) continue;
            // Re-parse the serialized value to get an independent copy (DeepClone is .NET 7+)
            var serialized = kv.Value?.ToJsonString() ?? "null";
            runtimeNode[kv.Key] = JsonNode.Parse(serialized);
        }

        var configDir = Path.GetDirectoryName(configFile)!;
        Directory.CreateDirectory(configDir);

        var json = runtimeNode.ToJsonString(WriteOpts);
        File.WriteAllText(configFile, json);
        generated.Add(configPathRel);
        Console.WriteLine($"[gen]  {configPathRel}");
    }

    private static void CreateExcelDirectories(
        string projectRoot,
        string excelPathRel,
        string enumPathRel,
        List<string> generated)
    {
        var excelDir = Path.GetFullPath(excelPathRel, projectRoot);
        var enumDir  = Path.IsPathRooted(enumPathRel)
            ? enumPathRel
            : Path.Combine(excelDir, enumPathRel);

        EnsureDir(excelDir, generated, projectRoot);
        EnsureDir(enumDir, generated, projectRoot);
    }

    private static void EnsureDir(string absPath, List<string> generated, string projectRoot)
    {
        if (Directory.Exists(absPath)) return;
        Directory.CreateDirectory(absPath);
        var gitkeep = Path.Combine(absPath, ".gitkeep");
        File.WriteAllText(gitkeep, "");
        var relDir = Path.GetRelativePath(projectRoot, absPath).Replace('\\', '/');
        generated.Add(relDir + "/");
        Console.WriteLine($"[gen]  {relDir}/");
    }

    private static void GenerateBuildScripts(
        string projectRoot,
        string toolRelPath,
        string configPathRel,
        List<string> generated,
        List<string> skipped)
    {
        var csprojName = "GameDataConfigTool.csproj";
        var toolCsproj = $"{toolRelPath}/{csprojName}";

        // build_config.bat (Windows)
        var batPath = Path.Combine(projectRoot, "build_config.bat");
        if (File.Exists(batPath))
        {
            skipped.Add("build_config.bat");
            Console.WriteLine("[skip] build_config.bat already exists.");
        }
        else
        {
            var bat =
                "@echo off\r\n" +
                "chcp 65001 >nul\r\n" +
                "cd /d \"%~dp0\"\r\n" +
                $"dotnet run --project {toolCsproj} --verbosity quiet -- --project-root . --config {configPathRel} %*\r\n" +
                "pause\r\n";
            File.WriteAllText(batPath, bat);
            generated.Add("build_config.bat");
            Console.WriteLine("[gen]  build_config.bat");
        }

        // build_config.sh (macOS / Linux)
        var shPath = Path.Combine(projectRoot, "build_config.sh");
        if (File.Exists(shPath))
        {
            skipped.Add("build_config.sh");
            Console.WriteLine("[skip] build_config.sh already exists.");
        }
        else
        {
            var sh =
                "#!/bin/bash\n" +
                "cd \"$(dirname \"$0\")\"\n" +
                $"dotnet run --project {toolCsproj} --verbosity quiet -- --project-root . --config {configPathRel} \"$@\"\n";
            File.WriteAllText(shPath, sh);
            generated.Add("build_config.sh");
            Console.WriteLine("[gen]  build_config.sh");
        }
    }

    private static void PrintSummary(
        List<string> generated,
        List<string> skipped,
        string projectRoot,
        string configPathRel)
    {
        Console.WriteLine();
        Console.WriteLine($"Setup complete. {generated.Count} file(s) generated, {skipped.Count} skipped.");
        Console.WriteLine();
        Console.WriteLine("Next steps:");
        Console.WriteLine($"  1. Review and edit {configPathRel} to match your project.");
        Console.WriteLine( "  2. Add .xlsx files to the excel directory.");
        Console.WriteLine( "  3. Discard changes to the tool directory (git checkout -- .)");
        Console.WriteLine($"  4. Commit the generated files in your project.");
        Console.WriteLine( "  5. Run build_config.bat (Windows) or build_config.sh (macOS/Linux) to generate code.");
        Console.WriteLine();
        Console.WriteLine($"Project root: {projectRoot}");
    }
}
