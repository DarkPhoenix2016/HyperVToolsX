namespace HyperVToolsX.Core.Logging;

public enum LiveLogLevel
{
    Info,
    Step,
    Warning,
    Error
}

public sealed record LiveLogEntry(
    DateTime Timestamp,
    LiveLogLevel Level,
    string Source,
    string? Target,
    string Message);

/// <summary>
/// Sink for the "Live Log" tab. Implementations must be thread-safe and must
/// never throw. Never pass secrets (passwords, tokens) as a message.
/// </summary>
public interface ILiveLog
{
    void Write(
        LiveLogLevel level,
        string source,
        string message,
        string? target = null);
}

public sealed class NullLiveLog : ILiveLog
{
    public static readonly NullLiveLog Instance = new();

    public void Write(LiveLogLevel level, string source, string message, string? target = null)
    {
    }
}

public sealed class LiveLog : ILiveLog
{
    public event EventHandler<LiveLogEntry>? EntryWritten;

    public void Write(
        LiveLogLevel level,
        string source,
        string message,
        string? target = null)
    {
        try
        {
            EntryWritten?.Invoke(
                this,
                new LiveLogEntry(DateTime.Now, level, source, target, message));
        }
        catch
        {
            // Logging must never break the operation being logged.
        }
    }
}

public static class LiveLogExtensions
{
    public static void Info(this ILiveLog log, string source, string message, string? target = null) =>
        log.Write(LiveLogLevel.Info, source, message, target);

    public static void Step(this ILiveLog log, string source, string message, string? target = null) =>
        log.Write(LiveLogLevel.Step, source, message, target);

    public static void Warn(this ILiveLog log, string source, string message, string? target = null) =>
        log.Write(LiveLogLevel.Warning, source, message, target);

    public static void Error(this ILiveLog log, string source, string message, string? target = null) =>
        log.Write(LiveLogLevel.Error, source, message, target);
}
