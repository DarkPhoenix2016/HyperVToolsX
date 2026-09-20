namespace HyperVToolsX.Core.Models;

public class HyperVVirtualMachine
{
    public string Name { get; set; } = string.Empty;
    public Guid VMId { get; set; }
    public string HostName { get; set; } = string.Empty;
    public string ClusterName { get; set; } = string.Empty;

    public string CheckpointFileLocation { get; set; } = string.Empty;
    public string ConfigurationLocation { get; set; } = string.Empty;
    public string GuestStatePath { get; set; } = string.Empty;
    public string SmartPagingFilePath { get; set; } = string.Empty;
    public bool SmartPagingFileInUse { get; set; }
    public string SnapshotFileLocation { get; set; } = string.Empty;

    public string AutomaticStartAction { get; set; } = string.Empty;
    public int AutomaticStartDelay { get; set; }
    public string AutomaticStopAction { get; set; } = string.Empty;
    public string AutomaticCriticalErrorAction { get; set; } = string.Empty;
    public int AutomaticCriticalErrorActionTimeout { get; set; }
    public bool AutomaticCheckpointsEnabled { get; set; }

    public int CPUUsage { get; set; }

    public long MemoryAssigned { get; set; }
    public long MemoryDemand { get; set; }
    public string MemoryStatus { get; set; } = string.Empty;

    public bool NumaAligned { get; set; }
    public int NumaNodesCount { get; set; }
    public int NumaSocketCount { get; set; }

    public string Heartbeat { get; set; } = string.Empty;
    public string IntegrationServicesState { get; set; } = string.Empty;
    public string IntegrationServicesVersion { get; set; } = string.Empty;

    public TimeSpan? Uptime { get; set; }

    public List<string> OperationalStatus { get; set; } = [];
    public List<string> StatusDescriptions { get; set; } = [];

    public string PrimaryOperationalStatus { get; set; } = string.Empty;
    public string SecondaryOperationalStatus { get; set; } = string.Empty;
    public string PrimaryStatusDescription { get; set; } = string.Empty;
    public string SecondaryStatusDescription { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;

    public string ReplicationHealth { get; set; } = string.Empty;
    public string ReplicationMode { get; set; } = string.Empty;
    public string ReplicationState { get; set; } = string.Empty;

    public bool ResourceMeteringEnabled { get; set; }

    public string CheckpointType { get; set; } = string.Empty;
    public string EnhancedSessionTransportType { get; set; } = string.Empty;

    public List<string> Groups { get; set; } = [];

    public string Version { get; set; } = string.Empty;
    public string VirtualMachineType { get; set; } = string.Empty;
    public string VirtualMachineSubType { get; set; } = string.Empty;
    public string GuestStateIsolationType { get; set; } = string.Empty;

    public string Notes { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;

    /// <summary>Numeric value of the Hyper-V VMState enum (2 = Running, 3 = Off, ...).</summary>
    public int StateId { get; set; }

    public bool DynamicMemoryEnabled { get; set; }
    public long MemoryMaximum { get; set; }
    public long MemoryMinimum { get; set; }
    public long MemoryStartup { get; set; }

    public int ProcessorCount { get; set; }

    public bool BatteryPassthroughEnabled { get; set; }
    public int Generation { get; set; }
    public bool IsClustered { get; set; }

    public DateTime? BootTime { get; set; }
}