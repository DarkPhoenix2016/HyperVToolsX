using HyperVToolsX.Core.Enums;

namespace HyperVToolsX.Core.Models;

public class TargetValidationResult
{
    public string TargetName { get; set; } = string.Empty;

    public TargetValidationStatus Status { get; set; }
        = TargetValidationStatus.Pending;

    public bool NameResolved { get; set; }

    public string? ResolvedAddress { get; set; }

    public bool PingSucceeded { get; set; }

    public bool HyperVConnectionSucceeded { get; set; }

    public bool IsCluster { get; set; }

    public string? ClusterName { get; set; }

    public string? ErrorMessage { get; set; }

    public DateTime StartedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public TimeSpan? Duration =>
        CompletedAt.HasValue
            ? CompletedAt.Value - StartedAt
            : null;

    /// <summary>
    /// True when this target's Hyper-V connection was verified within <paramref name="maxAge"/>, so
    /// collection can skip repeating the DNS/ping/WinRM probe (a whole extra PowerShell process per host).
    /// </summary>
    public bool IsFresh(TimeSpan maxAge) =>
        HyperVConnectionSucceeded
        && CompletedAt.HasValue
        && DateTime.Now - CompletedAt.Value <= maxAge;

    public bool CanCollect =>
        Status == TargetValidationStatus.Ready ||
        Status == TargetValidationStatus.ClusterReady;
}