namespace GameDataTool.Core;

/// <summary>Minimal structured logger. Call <see cref="Dispose"/> at shutdown.</summary>
public static class Log
{
    private static StreamWriter? _file;

    public static void Init(bool toFile)
    {
        if (!toFile) return;
        const string path = "logs/tool.log";
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        _file = new StreamWriter(path, append: true) { AutoFlush = true };
    }

    public static void Info(string msg)    => Write("INF", msg);
    public static void Warn(string msg)    => Write("WRN", msg);
    public static void Error(string msg)   => Write("ERR", msg);

    public static void Dispose() { _file?.Dispose(); _file = null; }

    private static void Write(string level, string msg)
    {
        var line = $"[{DateTime.Now:HH:mm:ss}] [{level}] {msg}";
        Console.WriteLine(line);
        _file?.WriteLine(line);
    }
}
