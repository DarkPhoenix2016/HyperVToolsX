namespace HyperVToolsX.Core.Models.Details;

public class HostStorageInfo
{
    public string HostName { get; set; } = string.Empty;

    public string ComputerName { get; set; } = string.Empty;

    public string VirtualHardDiskPath { get; set; } =
        string.Empty;

    public string VirtualMachinePath { get; set; } =
        string.Empty;

    public string ParentSnapshotPath { get; set; } =
        string.Empty;

    public long MemoryCapacity { get; set; }

    public int LogicalProcessorCount { get; set; }

    public int MaximumStorageMigrations { get; set; }

    public int MaximumVirtualMachineMigrations { get; set; }

    public bool VirtualMachineMigrationEnabled { get; set; }

    public string VirtualMachineMigrationAuthenticationType { get; set; } =
        string.Empty;

    public string VirtualMachineMigrationPerformanceOption { get; set; } =
        string.Empty;

    public bool UseAnyNetworkForMigration { get; set; }

    public bool EnableEnhancedSessionMode { get; set; }

    public bool IsDeleted { get; set; }
}