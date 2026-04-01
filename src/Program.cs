using GameDataTool.Core.Configuration;
using GameDataTool.Core.Logging;
using GameDataTool.Parsers;
using GameDataTool.Generators;

namespace GameDataTool;

static class Program
{
    static async Task Main(string[] args)
    {
        try
        {
            Console.WriteLine();
            Console.WriteLine("=== Game Data Tool ===");
            Console.WriteLine("Excel → validated binary / JSON / C# for Unity");
            Console.WriteLine();

            if (args.Contains("--help") || args.Contains("-h"))
            {
                ShowHelp();
                return;
            }

            if (!Directory.Exists("excels") || !Directory.GetFiles("excels", "*.xlsx").Any())
            {
                Console.WriteLine("No Excel files found. Add .xlsx files under excels/ and run again.");
                return;
            }

            var config = await ConfigurationManager.LoadAsync();
            Logger.Initialize(config.Logging.Level, config.Logging.OutputToFile);

            var cwd = Directory.GetCurrentDirectory();
            var jsonOutputPath = Path.GetFullPath(config.OutputPaths.Json, cwd);
            var binaryOutputPath = Path.GetFullPath(config.OutputPaths.Binary, cwd);
            var codeOutputPath = Path.GetFullPath(config.OutputPaths.Code, cwd);

            EnsureOutputDirectories(config, jsonOutputPath, binaryOutputPath, codeOutputPath);

            if (config.CleanOutputsBeforeGenerate)
            {
                if (config.Generators.EnableJson)
                    TryCleanOutputDirectory(jsonOutputPath, "JSON output");
                if (config.Generators.EnableBinary)
                    TryCleanOutputDirectory(binaryOutputPath, "binary output");
                if (config.Generators.EnableCode)
                    TryCleanOutputDirectory(codeOutputPath, "code output");
            }

            Logger.Info("Processing Excel data...");

            var excelParser = new ExcelParser();
            var data = await excelParser.ParseAsync(config.ExcelPath, config.EnumPath);

            if (data.Tables.Count == 0 && data.Enums.Count == 0)
            {
                Console.WriteLine("No data tables or enum files were produced. Check excels/ and enumPath in settings.");
                return;
            }

            var validator = new DataValidator();
            var validationResult = await validator.ValidateAsync(data, config.Validation);

            if (!validationResult.IsValid)
            {
                Console.WriteLine($"Validation failed ({validationResult.Errors.Count} issue(s)):");
                foreach (var error in validationResult.Errors)
                    Console.WriteLine($"  - {error}");
                Logger.Error("Build stopped: validation errors.");
                Environment.Exit(1);
            }

            Logger.Info("Validation passed.");
            Console.WriteLine();

            var generator = new OutputGenerator();
            var startTime = DateTime.UtcNow;

            Console.WriteLine("Generating outputs...");
            if (config.Generators.EnableJson)
                await generator.GenerateJsonAsync(data, jsonOutputPath);
            if (config.Generators.EnableBinary)
                await generator.GenerateBinaryAsync(data, binaryOutputPath);
            if (config.Generators.EnableCode)
                await generator.GenerateCodeAsync(data, codeOutputPath, config.CodeGeneration);

            var duration = DateTime.UtcNow - startTime;

            Console.WriteLine();
            Console.WriteLine($"Done in {duration.TotalMilliseconds:F0} ms.");
            if (config.Generators.EnableJson)
                Console.WriteLine($"  JSON:    {jsonOutputPath}");
            if (config.Generators.EnableBinary)
                Console.WriteLine($"  Binary:  {binaryOutputPath}");
            if (config.Generators.EnableCode)
                Console.WriteLine($"  Code:    {codeOutputPath}");
            Console.WriteLine();

            Logger.Info($"Generation finished in {duration.TotalMilliseconds:F0} ms.");
        }
        catch (FileNotFoundException ex)
        {
            Console.WriteLine($"File not found: {ex.Message}");
            Logger.Error(ex.Message);
            Environment.Exit(1);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            Logger.Error(ex.Message);
            if (ex.InnerException != null)
            {
                Console.WriteLine($"  {ex.InnerException.Message}");
                Logger.Error(ex.InnerException.Message);
            }
            Environment.Exit(1);
        }
    }

    private static void EnsureOutputDirectories(
        ToolConfiguration config,
        string jsonPath,
        string binaryPath,
        string codePath)
    {
        var paths = new List<string>();
        if (config.Generators.EnableJson)
            paths.Add(jsonPath);
        if (config.Generators.EnableBinary)
            paths.Add(binaryPath);
        if (config.Generators.EnableCode)
            paths.Add(codePath);

        foreach (var path in paths)
        {
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
        }
    }

    /// <summary>Deletes files and subfolders except <c>ext</c> (hand-written partials).</summary>
    private static void TryCleanOutputDirectory(string path, string label)
    {
        if (!Directory.Exists(path))
            return;

        try
        {
            foreach (var dir in Directory.GetDirectories(path))
            {
                if (string.Equals(Path.GetFileName(dir), "ext", StringComparison.OrdinalIgnoreCase))
                    continue;
                Directory.Delete(dir, recursive: true);
            }

            foreach (var file in Directory.GetFiles(path))
                File.Delete(file);

            Console.WriteLine($"Cleaned {label}: {path}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: could not clean {label} ({path}): {ex.Message}");
        }
    }

    private static void ShowHelp()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  dotnet run");
        Console.WriteLine("  dotnet run -- --help");
        Console.WriteLine();
        Console.WriteLine("Layout:");
        Console.WriteLine("  config/settings.json  — paths, toggles, namespace");
        Console.WriteLine("  excels/*.xlsx         — one workbook per table (first sheet only)");
        Console.WriteLine("  excels/<enumPath>/    — enum workbooks");
        Console.WriteLine();
        Console.WriteLine("Run from the tool root so relative paths in settings resolve correctly.");
    }
}
