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

            var profileOverride = ParseArg(args, "--profile");
            var configPath      = ParseArg(args, "--config");
            var projectRoot     = ParseArg(args, "--project-root");

            if (projectRoot != null)
                ToolConfig.SetProjectRoot(projectRoot);

            Log.Init(false);
            var cfg = ToolConfig.Load(profileOverride, configPath);

            var resolvedProjectRoot = ToolConfig.GetProjectRoot();
            var excelFullPath = cfg.ResolveExcelPath();
            var enumFullPath  = cfg.ResolveEnumPath(excelFullPath);
            var jsonPath      = cfg.ResolveOutputPath(cfg.OutputPaths.Json);
            var binaryPath    = cfg.ResolveOutputPath(cfg.OutputPaths.Binary);
            var codePath      = cfg.ResolveOutputPath(cfg.OutputPaths.Code);

            Log.Info($"Project root: {resolvedProjectRoot}");
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
  --profile <name>       Override active profile (e.g. --profile unity)
  --config <path>        Load config from an external file (bypasses profile resolution)
  --project-root <path>  Set project root; all paths in config are resolved relative to it
  --setup                Run setup: generate project-side files from config/setup.json
  --help / -h            Show this help

── Standalone usage (default) ──────────────────────────────────────────────────
  Place this tool directory inside your project root:

    MyProject/
      GameDataConfig/           <- this tool (run dotnet run here)
        excels/*.xlsx           <- data tables
        config/profile.json     <- { ""active"": ""cocos"" }
        config/cocos.json       <- Cocos pipeline (TS + JSON)
        config/unity.json       <- Unity pipeline (C# + binary)
        templates/

  outputPaths in config are relative to the project root (parent of this tool dir).
  excelPath is relative to this tool dir.

── Submodule usage ──────────────────────────────────────────────────────────────
  1. Add as submodule:
       git submodule add <url> tools/ConfigTool

  2. Edit config/setup.json inside the tool to match your project layout.

  3. Run setup:
       setup.bat       (Windows)
       ./setup.sh      (macOS / Linux)

     This generates in the project root:
       - config/configtool.json    (runtime config)
       - config/excels/            (excel directory)
       - build_config.bat / .sh    (build scripts)

  4. Discard tool changes: git checkout -- tools/ConfigTool/

  5. Commit the generated files, then use build_config.bat to generate code.

  All paths in configtool.json are relative to the project root.");
    }
}
