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
            Console.WriteLine();
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
                Console.WriteLine("No Excel files detected. Please put valid .xlsx files in the excels/ directory and try again.");
                return;
            }

            // Read configuration
            var config = await ConfigurationManager.LoadAsync();
            Logger.Initialize(config.Logging.Level, config.Logging.OutputToFile);

            // Generate absolute paths, support ../Assets/... style relative paths
            string jsonOutputPath = Path.GetFullPath(config.OutputPaths.Json, Directory.GetCurrentDirectory());
            string binaryOutputPath = Path.GetFullPath(config.OutputPaths.Binary, Directory.GetCurrentDirectory());
            string codeOutputPath = Path.GetFullPath(config.OutputPaths.Code, Directory.GetCurrentDirectory());

            // Create output directories
            Console.WriteLine("Creating output directories...");
            var outputDirs = new List<string> { jsonOutputPath, binaryOutputPath, codeOutputPath };
            foreach (var dir in outputDirs)
            {
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
            }

            // Clean binary output directory (keep only ext subdirectory)
            if (Directory.Exists(binaryOutputPath))
            {
                try
                {
                    foreach (var dir in Directory.GetDirectories(binaryOutputPath))
                    {
                        if (Path.GetFileName(dir).ToLower() != "ext")
                            Directory.Delete(dir, true);
                    }
                    foreach (var file in Directory.GetFiles(binaryOutputPath))
                    {
                        File.Delete(file);
                    }
                    Console.WriteLine($"Cleaned output directory (except ext): {binaryOutputPath}\n");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to clean output directory: {ex.Message}");
                }
            }

            // Clean Unity output directories (if exist)
            string unityRoot = null;
            try
            {
                // Check if running in Unity environment
                unityRoot = Directory.GetParent(Directory.GetCurrentDirectory()).FullName;
                var unityCode = Path.Combine(unityRoot, "Assets/Scripts/ConfigData/code");
                if (Directory.Exists(unityCode))
                {
                    foreach (var dir in Directory.GetDirectories(unityCode))
                    {
                        if (Path.GetFileName(dir).ToLower() != "ext")
                            Directory.Delete(dir, true);
                    }
                    foreach (var file in Directory.GetFiles(unityCode))
                    {
                        File.Delete(file);
                    }
                    Console.WriteLine($"Cleaned Unity code output (except ext): {unityCode}");
                }
            }
            catch { /* Ignore Unity path exception */ }


            Logger.Info("Processing Excel data...");

            // Parse Excel
            var excelParser = new ExcelParser();
            var data = await excelParser.ParseAsync(config.ExcelPath, config.EnumPath);

            if (data.Tables.Count == 0 && data.Enums.Count == 0)
            {
                Console.WriteLine("Warning: No Excel data tables or enum types found.");
                Console.WriteLine("Please make sure valid .xlsx files are present in the excels/ directory.");
                return;
            }

            // Data validation
            var validator = new DataValidator();
            var validationResult = await validator.ValidateAsync(data, config.Validation);
            
            if (!validationResult.IsValid)
            {
                Console.WriteLine($"Data validation failed: {validationResult.Errors.Count} error(s)");
                foreach (var error in validationResult.Errors)
                {
                    Console.WriteLine($"  - {error}");
                }
                Logger.Error("Build terminated due to data validation errors.");
                Environment.Exit(1);
            }
            else
            {
                Logger.Info("Data validation passed");
                Console.WriteLine();
            }

            // Generate output
            var generator = new OutputGenerator();
            var startTime = DateTime.Now;

            Console.WriteLine($"Start generating output files...");
            Console.WriteLine();

            Console.WriteLine($"JSON generation enabled: {config.Generators.EnableJson}");
            if (config.Generators.EnableJson)
            {
                await generator.GenerateJsonAsync(data, jsonOutputPath);
            }

            if (config.Generators.EnableBinary)
            {
                await generator.GenerateBinaryAsync(data, binaryOutputPath);
            }

            if (config.Generators.EnableCode)
            {
                await generator.GenerateCodeAsync(data, codeOutputPath, config.CodeGeneration);
            }

            var endTime = DateTime.Now;
            var duration = endTime - startTime;

            Console.WriteLine();
            Console.WriteLine("All files generated successfully!");
            Console.WriteLine($"Total time: {duration.TotalMilliseconds:F0}ms");
            Console.WriteLine();
            Console.WriteLine("Output directories:");
            if (config.Generators.EnableJson)
            {
                Console.WriteLine($"  JSON: {jsonOutputPath}");
            }
            if (config.Generators.EnableBinary)
            {
                Console.WriteLine($"  Binary: {binaryOutputPath}");
            }
            if (config.Generators.EnableCode)
            {
                Console.WriteLine($"  Code: {codeOutputPath}");
            }
            Console.WriteLine();
            
            Logger.Info($"All files generated successfully! Total time: {duration.TotalMilliseconds:F0}ms");
        }
        catch (FileNotFoundException ex)
        {
            Console.WriteLine($"File not found: {ex.Message}");
            Logger.Error($"File not found: {ex.Message}");
            Environment.Exit(1);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred: {ex.Message}");
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