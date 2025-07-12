namespace GameDataTool.Core.Logging;

public static class Logger
{
    private static LoggingConfiguration? _config;
    private static StreamWriter? _logWriter;

    public static void Initialize(string level, bool outputToFile)
    {
        _config = new LoggingConfiguration
        {
            Level = ParseLogLevel(level),
            OutputToFile = outputToFile,
            LogFilePath = "logs/tool.log"
        };

        if (_config.OutputToFile)
        {
            var logDir = Path.GetDirectoryName(_config.LogFilePath);
            if (!string.IsNullOrEmpty(logDir) && !Directory.Exists(logDir))
            {
                Directory.CreateDirectory(logDir);
            }
            _logWriter = new StreamWriter(_config.LogFilePath, true);
        }
    }

    public static void Info(string message)
    {
        WriteLog(LogLevel.Info, message);
    }

    public static void Warning(string message)
    {
        WriteLog(LogLevel.Warning, message);
    }

    public static void Error(string message)
    {
        WriteLog(LogLevel.Error, message);
    }

    public static void Debug(string message)
    {
        WriteLog(LogLevel.Debug, message);
    }

    private static void WriteLog(LogLevel level, string message)
    {
        if (_config == null || level < _config.Level)
            return;

        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        var logMessage = $"[{timestamp}] [{level}] {message}";

        Console.WriteLine(logMessage);

        if (_config.OutputToFile && _logWriter != null)
        {
            _logWriter.WriteLine(logMessage);
            _logWriter.Flush();
        }
    }

    private static LogLevel ParseLogLevel(string level)
    {
        return level.ToLower() switch
        {
            "debug" => LogLevel.Debug,
            "info" => LogLevel.Info,
            "warning" => LogLevel.Warning,
            "error" => LogLevel.Error,
            _ => LogLevel.Info
        };
    }

    public static void Dispose()
    {
        _logWriter?.Dispose();
    }
}

public enum LogLevel
{
    Debug = 0,
    Info = 1,
    Warning = 2,
    Error = 3
}

public class LoggingConfiguration
{
    public LogLevel Level { get; set; } = LogLevel.Info;
    public bool OutputToFile { get; set; } = true;
    public string LogFilePath { get; set; } = "logs/tool.log";
} 