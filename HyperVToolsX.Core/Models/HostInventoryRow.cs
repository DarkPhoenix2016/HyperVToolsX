namespace HyperVToolsX.Core.Models;

public class HostInventoryRow
{
    // Host
    public string HostName { get; set; } = string.Empty;
    public string Fqdn { get; set; } = string.Empty;
    public string ClusterName { get; set; } = string.Empty;
    public bool IsClusterNode { get; set; }
    public bool IsConnected { get; set; }

    // Hyper-V
    public string HyperVVersion { get; set; } = string.Empty;
    public int LogicalProcessorCount { get; set; }
    public int VirtualMachineCount { get; set; }

    // Memory
    public long TotalMemoryBytes { get; set; }
    public long UsedMemoryBytes { get; set; }

    // Operating System
    public string OperatingSystem { get; set; } = string.Empty;
    public string OSVersion { get; set; } = string.Empty;
    public string OSBuildNumber { get; set; } = string.Empty;
    public string OSArchitecture { get; set; } = string.Empty;
    public DateTime? LastBootUpTime { get; set; }

    // OS Memory
    public long TotalVisibleMemorySizeKb { get; set; }
    public long FreePhysicalMemoryKb { get; set; }

    // Hyper-V Storage
    public string VirtualHardDiskPath { get; set; } = string.Empty;
    public string VirtualMachinePath { get; set; } = string.Empty;
    public string ParentSnapshotPath { get; set; } = string.Empty;

    // VM Migration
    public int MaximumStorageMigrations { get; set; }
    public int MaximumVirtualMachineMigrations { get; set; }
    public bool VirtualMachineMigrationEnabled { get; set; }
    public string VirtualMachineMigrationAuthenticationType { get; set; } = string.Empty;
    public string VirtualMachineMigrationPerformanceOption { get; set; } = string.Empty;
    public bool UseAnyNetworkForMigration { get; set; }

    // Hyper-V Settings
    public bool EnableEnhancedSessionMode { get; set; }

    // Status
    public bool IsDeleted { get; set; }
}