using HyperVToolsX.Core.Models;

namespace HyperVToolsX.Core.Collection;

public class CollectionResult
{
    public List<HyperVTarget> Targets { get; set; } = [];

    public int TotalTargets { get; set; }

    public int SuccessfulTargets { get; set; }

    public int FailedTargets { get; set; }

    public int TotalHosts { get; set; }

    public int TotalVirtualMachines { get; set; }

    public DateTime StartedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public TimeSpan? Duration =>
        CompletedAt.HasValue
            ? CompletedAt.Value - StartedAt
            : null;

    public bool IsCancelled { get; set; }
}