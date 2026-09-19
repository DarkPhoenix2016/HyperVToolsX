namespace HyperVToolsX.Core.Models;

public class HyperVHost
{
    public string Name { get; set; } = string.Empty;

    public string Fqdn { get; set; } = string.Empty;

    public string OperatingSystem { get; set; } = string.Empty;

    public string HyperVVersion { get; set; } = string.Empty;

    public int LogicalProcessorCount { get; set; }

    public long TotalMemoryBytes { get; set; }

    public long UsedMemoryBytes { get; set; }

    public int VirtualMachineCount { get; set; }

    public string ClusterName { get; set; } = string.Empty;

    public bool IsClusterNode { get; set; }

    public bool IsConnected { get; set; }
}