using System.Text;

namespace GameDataTool.Core;

/// <summary>Name conversion utilities for code generation.</summary>
public static class Naming
{
    public static string ToPascalCase(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;

        var sb = new StringBuilder(input.Length);
        var capitalizeNext = true;

        foreach (var ch in input)
        {
            if (ch is '_' or '-' or ' ')
            {
                capitalizeNext = true;
                continue;
            }

            if (capitalizeNext)
            {
                sb.Append(char.ToUpperInvariant(ch));
                capitalizeNext = false;
            }
            else
            {
                sb.Append(ch);
            }
        }

        return sb.ToString();
    }

    public static string ToCamelCase(string input)
    {
        var pascal = ToPascalCase(input);
        return pascal.Length == 0 ? pascal : char.ToLowerInvariant(pascal[0]) + pascal[1..];
    }
}
