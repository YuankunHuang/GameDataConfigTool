using System.Reflection;

namespace GameDataTool.Generation;

/// <summary>
/// Loads embedded .template files and performs placeholder replacement.
/// Templates are embedded as resources from the templates/ directory.
/// </summary>
public sealed class TemplateEngine
{
    private readonly string _templateDir;

    public TemplateEngine(string templateDir)
    {
        _templateDir = templateDir;
    }

    public string Load(string language, string templateName)
    {
        var path = Path.Combine(_templateDir, language, templateName);
        if (!File.Exists(path))
            throw new FileNotFoundException($"Template not found: {path}");
        return File.ReadAllText(path);
    }

    public static string Replace(string template, params (string key, string value)[] replacements)
    {
        var result = template;
        foreach (var (key, value) in replacements)
            result = result.Replace($"{{{{{key}}}}}", value);
        return result;
    }
}
