using System.Text;
using System.Text.RegularExpressions;

namespace HyperVToolsX.Core.Logging;

/// <summary>
/// Appends log entries to a daily file (<c>hypervtoolsx-yyyyMMdd.log</c>), so a problem can be
/// diagnosed after the window is closed. Thread-safe, never throws, redacts anything that looks
/// like a password, caps each day's file size, and deletes files past the retention period.
/// </summary>
public sealed partial class FileLogWriter
{
    public const int DefaultRetentionDays = 14;
    public const long DefaultMaxFileBytes = 10 * 1024 * 1024;

    private readonly object _lock = new();
    private readonly long _maxFileBytes;
    private bool _capNoted;

    public FileLogWriter(string? directory = null, int retentionDays = DefaultRetentionDays, long maxFileBytes = DefaultMaxFileBytes)
    {
        Directory = directory ?? DefaultDirectory;
        _maxFileBytes = maxFileBytes;

        Prune(retentionDays);
    }

    public string Directory { get; }

    /// <summary>Per-user, so log contents (host names, errors) aren't readable by other accounts.</summary>
    public static string DefaultDirectory =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "HyperVToolsX",
            "logs");

    public string CurrentFile => Path.Combine(Directory, $"hypervtoolsx-{DateTime.Now:yyyyMMdd}.log");

    public void Write(LiveLogEntry entry) =>
        Write(entry.Timestamp, entry.Level.ToString().ToUpperInvariant(), entry.Source, entry.Target, entry.Message);

    public void Write(DateTime timestamp, string level, string source, string? target, string message)
    {
        try
        {
            var line = new StringBuilder()
                .Append(timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff"))
                .Append(" [").Append(level).Append("] ")
                .Append(source)
                .Append(string.IsNullOrEmpty(target) ? string.Empty : " (" + target + ")")
                .Append(": ")
                .Append(Redact(message).ReplaceLineEndings(" | "))
                .AppendLine()
                .ToString();

            lock (_lock)
            {
                System.IO.Directory.CreateDirectory(Directory);

                var file = CurrentFile;

                if (File.Exists(file) && new FileInfo(file).Length > _maxFileBytes)
                {
                    if (!_capNoted)
                    {
                        _capNoted = true;
                        File.AppendAllText(file, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [WARNING] Log: daily size limit reached; further entries are dropped{Environment.NewLine}");
                    }

                    return;
                }

                File.AppendAllText(file, line);
            }
        }
        catch (Exception)
        {
            // Logging must never break the operation being logged.
        }
    }

    public static string Redact(string? message) =>
        string.IsNullOrEmpty(message) ? string.Empty : SecretPattern().Replace(message, "$1=***");

    private void Prune(int retentionDays)
    {
        try
        {
            if (!System.IO.Directory.Exists(Directory))
            {
                return;
            }

            var cutoff = DateTime.Now.AddDays(-Math.Max(retentionDays, 1));

            foreach (var file in System.IO.Directory.EnumerateFiles(Directory, "hypervtoolsx-*.log"))
            {
                if (File.GetLastWriteTime(file) < cutoff)
                {
                    File.Delete(file);
                }
            }
        }
        catch (Exception)
        {
            // Best effort.
        }
    }

    [GeneratedRegex(@"(?i)\b(password|passwd|pwd|secret|token)\b\s*[=:]\s*(?:""[^""]*""|'[^']*'|\S+)")]
    private static partial Regex SecretPattern();
}
