using GameDataTool.Core;
using GameDataTool.Core.Configuration;
using GameDataTool.Core.Logging;
using GameDataTool.Parsers;
using GameDataTool.Generators;

namespace GameDataTool;

class Program
{
    static async Task Main(string[] args)
    {
        try
        {
            Console.WriteLine("=== Game Data Tool ===");
            Console.WriteLine("A standalone game data configuration tool");
            Console.WriteLine();

            // Show help
            if (args.Contains("--help") || args.Contains("-h"))
            {
                ShowHelp();
                return;
            }

            // Check for Excel files
            if (!Directory.Exists("excels") || !Directory.GetFiles("excels", "*.xlsx").Any())
            {
                Console.WriteLine("❌ No Excel files detected. Please put valid .xlsx files in the excels/ directory and try again.");
                return;
            }

            // Create output directories
            Console.WriteLine("Creating output directories...");
            var outputDirs = new[] { "output", "output/json", "output/binary", "output/code" };
            foreach (var dir in outputDirs)
            {
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
            }

            // Load config
            Console.WriteLine("Loading configuration...");
            var config = await ConfigurationManager.LoadAsync();
            Logger.Initialize(config.Logging.Level, config.Logging.OutputToFile);

            Logger.Info("Processing Excel data...");
            Console.WriteLine("Processing Excel data...");

            // Parse Excel
            Console.WriteLine("Parsing Excel files...");
            var excelParser = new ExcelParser();
            var data = await excelParser.ParseAsync(config.ExcelPath, config.EnumPath);

            if (data.Tables.Count == 0 && data.Enums.Count == 0)
            {
                Console.WriteLine("⚠️  Warning: No Excel data tables or enum types found.");
                Console.WriteLine("Please make sure valid .xlsx files are present in the excels/ directory.");
                return;
            }

            Console.WriteLine($"✅ Parsing complete: {data.Tables.Count} data tables, {data.Enums.Count} enum types");

            // Data validation
            Console.WriteLine("Validating data...");
            var validator = new DataValidator();
            var validationResult = await validator.ValidateAsync(data, config.Validation);
            
            if (!validationResult.IsValid)
            {
                Console.WriteLine($"⚠️  Data validation failed: {validationResult.Errors.Count} error(s)");
                foreach (var error in validationResult.Errors)
                {
                    Console.WriteLine($"  - {error}");
                }
                Console.WriteLine("⚠️  Continuing with generation despite validation errors...");
                Logger.Warning("Data validation failed, but continuing with generation");
            }
            else
            {
                Console.WriteLine("✅ Data validation passed");
                Logger.Info("Data validation passed");
            }

            // Generate output
            var generator = new OutputGenerator();
            var startTime = DateTime.Now;

            if (config.Generators.EnableJson)
            {
                Console.WriteLine("Generating JSON files...");
                await generator.GenerateJsonAsync(data, config.OutputPaths.Json);
                Console.WriteLine("✅ JSON files generated");
            }

            if (config.Generators.EnableBinary)
            {
                Console.WriteLine("Generating binary files...");
                await generator.GenerateBinaryAsync(data, config.OutputPaths.Binary);
                Console.WriteLine("✅ Binary files generated");
            }

            if (config.Generators.EnableCode)
            {
                Console.WriteLine("Generating code files...");
                await generator.GenerateCodeAsync(data, config.OutputPaths.Code, config.CodeGeneration);
                Console.WriteLine("✅ Code files generated");
            }

            var endTime = DateTime.Now;
            var duration = endTime - startTime;

            Console.WriteLine();
            Console.WriteLine("🎉 All files generated successfully!");
            Console.WriteLine($"⏱️  Total time: {duration.TotalMilliseconds:F0}ms");
            Console.WriteLine();
            Console.WriteLine("📁 Output directories:");
            Console.WriteLine($"  📄 JSON: {config.OutputPaths.Json}");
            Console.WriteLine($"  🔢 Binary: {config.OutputPaths.Binary}");
            Console.WriteLine($"  💻 Code: {config.OutputPaths.Code}");
            
            Logger.Info($"All files generated successfully! Total time: {duration.TotalMilliseconds:F0}ms");
        }
        catch (FileNotFoundException ex)
        {
            Console.WriteLine($"❌ File not found: {ex.Message}");
            Logger.Error($"File not found: {ex.Message}");
            Environment.Exit(1);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ An error occurred: {ex.Message}");
            Logger.Error($"An error occurred: {ex.Message}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"   Details: {ex.InnerException.Message}");
                Logger.Error($"Details: {ex.InnerException.Message}");
            }
            Environment.Exit(1);
        }
    }

    private static void ShowHelp()
    {
        Console.WriteLine("Game Data Tool - Game Data Configuration Utility");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  dotnet run                    # Run normally");
        Console.WriteLine("  dotnet run --help            # Show help");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --help, -h         Show this help message");
        Console.WriteLine();
        Console.WriteLine("Config file: config/settings.json");
        Console.WriteLine("Excel files: excels/ directory");
        Console.WriteLine();
        Console.WriteLine("Environment Detection:");
        Console.WriteLine("  - If in Unity project: Outputs to Assets/Scripts/ConfigData/");
        Console.WriteLine("  - If standalone: Outputs to local output/ directory");
    }
} 