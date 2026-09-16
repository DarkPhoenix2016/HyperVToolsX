namespace HyperVToolsX.Core.Collection;

public class CollectionProgress
{
    public int TotalTargets { get; set; }

    public int CompletedTargets { get; set; }

    public int FailedTargets { get; set; }

    public int TotalHosts { get; set; }

    public int TotalVirtualMachines { get; set; }

    public string CurrentTarget { get; set; } = string.Empty;

    public string CurrentStage { get; set; } = string.Empty;

    public double Percentage =>
        TotalTargets == 0
            ? 0
            : (double)CompletedTargets / TotalTargets * 100;
}