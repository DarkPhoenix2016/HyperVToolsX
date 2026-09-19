namespace HyperVToolsX.Core.Models.Details;

public class VmMemoryInfo
{
    public string VmName { get; set; } = string.Empty;

    public string HostName { get; set; } = string.Empty;

    public string ResourcePoolName { get; set; } = string.Empty;

    public int Buffer { get; set; }

    public bool DynamicMemoryEnabled { get; set; }

    public long Maximum { get; set; }

    public long MaximumPerNumaNode { get; set; }

    public long Minimum { get; set; }

    public int Priority { get; set; }

    public long Startup { get; set; }

    public bool HugePagesEnabled { get; set; }

    public string MemoryEncryptionPolicy { get; set; } =
        string.Empty;

    public bool MemoryEncryptionEnabled { get; set; }

    public string BackingType { get; set; } =
        string.Empty;

    public string Name { get; set; } = string.Empty;
}