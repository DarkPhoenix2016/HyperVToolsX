namespace HyperVToolsX.Core.Models.Details;

public class VmCheckpointInfo
{
    public string VmName { get; set; } = string.Empty;

    public string HostName { get; set; } = string.Empty;

    public string VMId { get; set; } = string.Empty;

    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string ParentCheckpointId { get; set; } = string.Empty;

    public string ParentCheckpointName { get; set; } = string.Empty;

    public string ParentSnapshotId { get; set; } = string.Empty;

    public string ParentSnapshotName { get; set; } = string.Empty;

    public string CheckpointType { get; set; } = string.Empty;

    public string SnapshotType { get; set; } = string.Empty;

    public bool IsAutomaticCheckpoint { get; set; }

    public string State { get; set; } = string.Empty;

    public DateTime? CreationTime { get; set; }

    public string Path { get; set; } = string.Empty;

    public long SizeOfSystemFiles { get; set; }

    public string Version { get; set; } = string.Empty;

    public bool DynamicMemoryEnabled { get; set; }

    public long MemoryMaximum { get; set; }

    public long MemoryMinimum { get; set; }

    public long MemoryStartup { get; set; }

    public int ProcessorCount { get; set; }

    public bool BatteryPassthroughEnabled { get; set; }

    public int Generation { get; set; }

    public bool IsClustered { get; set; }

    public string Notes { get; set; } = string.Empty;

    public bool GuestControlledCacheTypes { get; set; }

    public long LowMemoryMappedIoSpace { get; set; }

    public long HighMemoryMappedIoSpace { get; set; }

    public long HighMemoryMappedIoBaseAddress { get; set; }

    public string LockOnDisconnect { get; set; } = string.Empty;

    public bool IsDeleted { get; set; }

    public List<string> ComPorts { get; set; } = [];

    public List<string> DVDDrives { get; set; } = [];

    public List<string> FibreChannelHostBusAdapters { get; set; } = [];

    public List<string> VMIntegrationServices { get; set; } = [];

    public List<string> HardDrives { get; set; } = [];

    public List<string> NetworkAdapters { get; set; } = [];
}