using System;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AzureDevOpsWorkItemExporter.Logging;

public sealed class ExecutionLogger : IDisposable
{
    private readonly string[] _cliArgs;
    private readonly JsonSerializerOptions _serializerOptions =
        new() { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

    private LogLevel _minLevel = LogLevel.Info;
    private string _logFilePath;
    private StreamWriter? _writer;

    public ExecutionLogger(string baseDirectory, string[] cliArgs)
    {
        _cliArgs = SanitizeArguments(cliArgs);
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        _logFilePath = Path.Combine(baseDirectory, $"export-{timestamp}.log");
    }

    public void RecordStart(object context)
    {
        Log(LogLevel.Info, "Started CLI process", new
        {
            CliArguments = _cliArgs,
            Context = context
        }, alwaysWrite: true);
    }

    public void RecordConfiguration(object configuration)
    {
        Log(LogLevel.Info, "Loaded configuration", new
        {
            Configuration = configuration
        }, alwaysWrite: true);
    }

    public void RecordStatus(string status, string? message = null)
    {
        Log(LogLevel.Info, $"Execution {status}", new
        {
            Status = status,
            Message = message
        }, alwaysWrite: true);
    }

    public void RecordFieldNames(IEnumerable<string> fieldNames)
    {
        Log(LogLevel.Info, "Available Azure DevOps fields", new
        {
            FieldNames = fieldNames
        }, alwaysWrite: true);
    }

    public void RecordException(Exception ex)
    {
        Log(LogLevel.Error, "Exception occurred", new
        {
            Exception = ex.GetType().FullName,
            ex.Message,
            ex.StackTrace
        }, alwaysWrite: true);
    }

    public void RecordDebug(string message, object? data = null)
    {
        Log(LogLevel.Debug, message, data);
    }

    public void RecordWarning(string message, object? data = null)
    {
        Log(LogLevel.Warn, message, data);
    }

    public void SetVerbosity(string? verbosity)
    {
        _minLevel = ParseLevel(verbosity);
    }

    public void OverrideLocation(string location)
    {
        if (string.IsNullOrWhiteSpace(location))
        {
            return;
        }

        var targetDirectory = Path.IsPathRooted(location)
            ? location
            : Path.Combine(AppContext.BaseDirectory, location);

        Directory.CreateDirectory(targetDirectory);

        var newPath = Path.Combine(targetDirectory, Path.GetFileName(_logFilePath));

        CloseWriter();
        if (File.Exists(_logFilePath))
        {
            File.Move(_logFilePath, newPath, true);
        }

        _logFilePath = newPath;
    }

    private void Log(LogLevel level, string message, object? data, bool alwaysWrite = false)
    {
        if (!alwaysWrite && level < _minLevel)
        {
            return;
        }

        EnsureWriter();

        var entry = new
        {
            timestamp = DateTime.UtcNow,
            level = level.ToString().ToUpperInvariant(),
            message,
            data
        };

        _writer!.WriteLine(JsonSerializer.Serialize(entry, _serializerOptions));
    }

    private static LogLevel ParseLevel(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return LogLevel.Info;
        }

        return value.Trim().ToUpperInvariant() switch
        {
            "DEBUG" => LogLevel.Debug,
            "INFO" => LogLevel.Info,
            "WARN" => LogLevel.Warn,
            "WARNING" => LogLevel.Warn,
            "ERROR" => LogLevel.Error,
            _ => LogLevel.Info
        };
    }

    private void EnsureWriter()
    {
        if (_writer != null)
        {
            return;
        }

        var directory = Path.GetDirectoryName(_logFilePath) ?? AppContext.BaseDirectory;
        Directory.CreateDirectory(directory);
        var stream = new FileStream(_logFilePath, FileMode.Append, FileAccess.Write, FileShare.Read);
        _writer = new StreamWriter(stream) { AutoFlush = true };
    }

    private void CloseWriter()
    {
        _writer?.Dispose();
        _writer = null;
    }

    public void Dispose()
    {
        CloseWriter();
    }

    private static string[] SanitizeArguments(string[] args)
    {
        if (args.Length == 0)
        {
            return Array.Empty<string>();
        }

        var sanitized = new string[args.Length];
        var index = 0;
        while (index < args.Length)
        {
            var token = args[index];
            if (IsPatToken(token))
            {
                sanitized[index] = token;
                if (index + 1 < args.Length)
                {
                    sanitized[index + 1] = "***";
                }
                index += 2;
                continue;
            }

            if (token.StartsWith("--pat=", StringComparison.OrdinalIgnoreCase))
            {
                sanitized[index] = "--pat=***";
                index++;
                continue;
            }

            sanitized[index] = token;
            index++;
        }

        return sanitized;
    }

    private static bool IsPatToken(string token) =>
        token.Equals("--pat", StringComparison.OrdinalIgnoreCase);

    private enum LogLevel
    {
        Debug = 1,
        Info = 2,
        Warn = 3,
        Error = 4
    }
}
