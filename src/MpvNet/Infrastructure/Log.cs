using System.Globalization;
using System.Text;

namespace MpvNet;

public enum LogLevel
{
    Info,
    Debug,
    Error
}

public static class Log
{
    const int MaxValueLength = 500;

    static readonly FileLogWriter Writer = new FileLogWriter();
    public static bool IsEnabled => true;

    public static string LogFolder => FileLogWriter.DefaultLogFolder;

    public static void Info(string message) => Write(LogLevel.Info, message, null);
    public static void Debug(string message) => Write(LogLevel.Debug, message, null);
    public static void Error(string message) => Write(LogLevel.Error, message, null);
    public static void Error(Exception exception, string? message = null) => Write(LogLevel.Error, message, exception);

    public static string SafeValue(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return "";

        string ret = value;

        if (Uri.TryCreate(ret, UriKind.Absolute, out Uri? uri) &&
            (!string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment)))
        {
            ret = uri.GetLeftPart(UriPartial.Path);

            if (!string.IsNullOrEmpty(uri.Query))
                ret += "?***";

            if (!string.IsNullOrEmpty(uri.Fragment))
                ret += "#***";
        }

        if (ret.Length > MaxValueLength)
            ret = ret[..MaxValueLength] + "...";

        return ret;
    }

    public static string SafeValues(IEnumerable<string> values) =>
        "[" + string.Join(", ", values.Select(i => "'" + SafeValue(i) + "'")) + "]";

    static void Write(LogLevel level, string? message, Exception? exception)
    {
        if (level == LogLevel.Error)
        {
            Writer.Write(level, message, exception);
            return;
        }

#if ENABLE_FILE_LOGGING
        Writer.Write(level, message, exception);
#endif
    }
}

internal sealed class FileLogWriter
{
    const string FilePrefix = "mpvnet-";
    const string FileExtension = ".log";
    const int RetentionDays = 3;

    readonly object _lock = new();
    readonly Func<DateTime> _now;
    readonly string _folder;

    public FileLogWriter() : this(DefaultLogFolder, () => DateTime.Now)
    {
    }

    internal FileLogWriter(string folder, Func<DateTime> now)
    {
        _folder = folder;
        _now = now;
        DeleteOldLogs();
    }

    public static string DefaultLogFolder =>
        Path.Combine(AppPaths.LocalAppData, "mpv.net", "Logs");

    internal void Write(LogLevel level, string? message, Exception? exception)
    {
        try
        {
            lock (_lock)
            {
                Directory.CreateDirectory(_folder);
                string file = Path.Combine(_folder, FilePrefix + _now().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + FileExtension);
                File.AppendAllText(file, FormatLine(level, message, exception, _now()), Encoding.UTF8);
            }
        }
        catch
        {
        }
    }

    internal void DeleteOldLogs()
    {
        try
        {
            lock (_lock)
            {
                if (!Directory.Exists(_folder))
                    return;

                DateTime cutoff = _now().Date.AddDays(-RetentionDays);

                foreach (string file in Directory.EnumerateFiles(_folder, FilePrefix + "*" + FileExtension))
                {
                    try
                    {
                        string name = Path.GetFileNameWithoutExtension(file);
                        string dateText = name[FilePrefix.Length..];

                        if (DateTime.TryParseExact(dateText, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime date) &&
                            date.Date < cutoff)
                        {
                            File.Delete(file);
                        }
                    }
                    catch (Exception ex)
                    {
                        Write(LogLevel.Error, "Failed to delete old log file.", ex);
                    }
                }
            }
        }
        catch
        {
        }
    }

    internal static string FormatLine(LogLevel level, string? message, Exception? exception, DateTime timestamp)
    {
        var builder = new StringBuilder();
        builder
            .Append('[')
            .Append(timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture))
            .Append("] [")
            .Append(level.ToString().ToUpperInvariant())
            .Append("] ")
            .AppendLine(message ?? "");

        if (exception != null)
            builder.AppendLine(exception.ToString());

        return builder.ToString();
    }
}
