using GameDataTool.Core;
using GameDataTool.Generation;
using GameDataTool.Parsing;
using GameDataTool.Validation;

namespace GameDataTool;

static class Program
{
    static int Main(string[] args)
    {
        if (args.Contains("--help") || args.Contains("-h")) { ShowHelp(); return 0; }

        if (args.Contains("--setup"))
        {
            try { return ProjectInitializer.Run(); }
            catch (Exception ex)
            {
                Console.WriteLine($"Setup error: {ex.Message}");
                return 1;
            }
        }

        try
        {
            Console.WriteLine("\n=== Game Data Tool ===\n");

            var configPath  = ParseArg(args, "--config");
            var projectRoot = ParseArg(args, "--project-root");

            if (projectRoot == null || configPath == null)
            {
                Console.WriteLine("Error: --project-root and --config are required.");
                Console.WriteLine("Use the generated build_config script, or run with --help for details.");
                return 1;
            }

            ToolConfig.SetProjectRoot(projectRoot);

            Log.Init(false);
            var cfg = ToolConfig.Load(configPath);

            var excelFullPath = cfg.ResolveExcelPath();
            var enumFullPath  = cfg.ResolveEnumPath(excelFullPath);
            var jsonPath      = cfg.ResolveOutputPath(cfg.OutputPaths.Json);
            var binaryPath    = cfg.ResolveOutputPath(cfg.OutputPaths.Binary);
            var codePath      = cfg.ResolveOutputPath(cfg.OutputPaths.Code);

            Log.Info($"Project root: {ToolConfig.GetProjectRoot()}");
            Log.Info($"Excel path  : {excelFullPath}");

            if (cfg.CleanBeforeGenerate) CleanOutputs(cfg, jsonPath, binaryPath, codePath);

            // ─── Parse ──────────────────────────────────────

            Log.Info("Parsing...");
            var parser = new ExcelParser();
            var data   = parser.Parse(excelFullPath, enumFullPath);

            if (data.Tables.Count == 0 && data.Enums.Count == 0)
            {
                Console.WriteLine("No tables or enums found. Add .xlsx files to the excel directory and run again.");
                return 1;
            }

            // ─── Validate ───────────────────────────────────

            Log.Info("Validating...");
            var errors = new DataValidator().Validate(data, cfg.Validation);
            if (errors.Count > 0)
            {
                Console.WriteLine($"\nValidation failed ({errors.Count} errors):");
                foreach (var e in errors) Console.WriteLine($"  - {e}");
                return 1;
            }

            Log.Info("Validation passed.\n");

            // ─── Generate ───────────────────────────────────

            var templateDir = FindTemplateDir();
            var te = new TemplateEngine(templateDir);
            var t0 = DateTime.UtcNow;

            if (cfg.Generators.EnableJson)
            {
                Directory.CreateDirectory(jsonPath);
                JsonGenerator.Generate(data, jsonPath);
            }

            if (cfg.Generators.EnableBinary)
            {
                Directory.CreateDirectory(binaryPath);
                BinaryGenerator.Generate(data, binaryPath);
            }

            if (cfg.Generators.EnableCode)
            {
                Directory.CreateDirectory(codePath);
                var lang = cfg.CodeGeneration.Language.ToLowerInvariant();
                switch (lang)
                {
                    case "csharp" or "cs":
                        new CSharpCodeGenerator(te, cfg.CodeGeneration.Namespace)
                            .Generate(data, codePath, cfg.CodeGeneration.GenerateEnum);
                        break;
                    case "typescript" or "ts":
                        new TypeScriptCodeGenerator(te)
                            .Generate(data, codePath, cfg.CodeGeneration.GenerateEnum);
                        break;
                    default:
                        Log.Warn($"Unknown language '{lang}', skipping code generation.");
                        break;
                }
            }

            var ms = (DateTime.UtcNow - t0).TotalMilliseconds;
            Console.WriteLine($"\nDone in {ms:F0} ms.");
            if (cfg.Generators.EnableJson)   Console.WriteLine($"  JSON:   {jsonPath}");
            if (cfg.Generators.EnableBinary) Console.WriteLine($"  Binary: {binaryPath}");
            if (cfg.Generators.EnableCode)   Console.WriteLine($"  Code:   {codePath}");
            Console.WriteLine();
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            if (ex.InnerException != null) Console.WriteLine($"  {ex.InnerException.Message}");
            return 1;
        }
        finally
        {
            Log.Dispose();
        }
    }

    private static string FindTemplateDir()
    {
        var candidates = new[]
        {
            Path.Combine(Directory.GetCurrentDirectory(), "templates"),
            Path.Combine(AppContext.BaseDirectory, "templates"),
        };

        foreach (var c in candidates)
            if (Directory.Exists(c)) return c;

        throw new DirectoryNotFoundException(
            "templates/ directory not found. Run from repo root or ensure templates/ is next to the executable.");
    }

    private static void CleanOutputs(ToolConfig cfg, string jsonPath, string binaryPath, string codePath)
    {
        if (cfg.Generators.EnableJson)   CleanDir(jsonPath);
        if (cfg.Generators.EnableBinary) CleanDir(binaryPath);
        if (cfg.Generators.EnableCode)   CleanDir(codePath);
    }

    private static void CleanDir(string path)
    {
        if (!Directory.Exists(path)) return;
        foreach (var d in Directory.GetDirectories(path))
        {
            if (Path.GetFileName(d).Equals("ext", StringComparison.OrdinalIgnoreCase)) continue;
            Directory.Delete(d, true);
        }
        foreach (var f in Directory.GetFiles(path)) File.Delete(f);
    }

    private static string? ParseArg(string[] args, string key)
    {
        for (var i = 0; i < args.Length - 1; i++)
            if (args[i].Equals(key, StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        return null;
    }

    private static void ShowHelp()
    {
        Console.WriteLine(@"Usage:
  dotnet run [-- <options>]

Options:
  --project-root <path>  Set project root; all config paths are resolved relative to it
  --config <path>        Path to the runtime config file (e.g. config/configtool.json)
  --setup                Run setup: generate project-side files from config/setup.json
  --help / -h            Show this help

── Setup (one-time) ────────────────────────────────────────────────────────────
  1. Add as submodule:
       git submodule add <url> tools/ConfigTool

  2. Copy a preset or edit config/setup.json:
       cp config/presets/unity.setup.json config/setup.json   # Unity
       cp config/presets/cocos.setup.json config/setup.json   # Cocos Creator

  3. Adjust projectRoot in setup.json, then run:
       setup.bat       (Windows)
       ./setup.sh      (macOS / Linux)

  4. Discard tool changes: git checkout -- tools/ConfigTool/

  5. Commit the generated files, then use build_config.bat to generate code.

── Generated build script usage ────────────────────────────────────────────────
  build_config.bat / .sh calls this tool with the correct arguments:
    dotnet run -- --project-root . --config config/configtool.json

  All paths in configtool.json are relative to the project root.");
    }
}
