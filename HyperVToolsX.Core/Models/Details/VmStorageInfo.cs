namespace HyperVToolsX.Core.Models.Details;

public class VmStorageInfo
{
    public string VmName { get; set; } = string.Empty;

    public string HostName { get; set; } = string.Empty;

    public string VMId { get; set; } = string.Empty;

    public string ParentCheckpointId { get; set; } = string.Empty;

    public string ParentCheckpointName { get; set; } = string.Empty;

    public string CheckpointFileLocation { get; set; } =
        string.Empty;

    public string ConfigurationLocation { get; set; } =
        string.Empty;

    public string GuestStatePath { get; set; } =
        string.Empty;

    public bool SmartPagingFileInUse { get; set; }

    public string SmartPagingFilePath { get; set; } =
        string.Empty;

    public string SnapshotFileLocation { get; set; } =
        string.Empty;

    public string Path { get; set; } = string.Empty;

    public long SizeOfSystemFiles { get; set; }

    public string ParentSnapshotId { get; set; } =
        string.Empty;

    public string ParentSnapshotName { get; set; } =
        string.Empty;

    public string AutomaticStartAction { get; set; } =
        string.Empty;

    public int AutomaticStartDelay { get; set; }

    public string AutomaticStopAction { get; set; } =
        string.Empty;

    public string AutomaticCriticalErrorAction { get; set; } =
        string.Empty;

    public int AutomaticCriticalErrorActionTimeout { get; set; }

    public bool AutomaticCheckpointsEnabled { get; set; }

    public string State { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string CheckpointType { get; set; } =
        string.Empty;

    public bool ResourceMeteringEnabled { get; set; }

    public string EnhancedSessionTransportType { get; set; } =
        string.Empty;

    public string GuestStateIsolationType { get; set; } =
        string.Empty;

    public string VirtualMachineType { get; set; } =
        string.Empty;

    public string VirtualMachineSubType { get; set; } =
        string.Empty;

    public string Version { get; set; } = string.Empty;

    public bool GuestControlledCacheTypes { get; set; }

    public long LowMemoryMappedIoSpace { get; set; }

    public long HighMemoryMappedIoSpace { get; set; }

    public long HighMemoryMappedIoBaseAddress { get; set; }

    public string LockOnDisconnect { get; set; } =
        string.Empty;

    public DateTime? CreationTime { get; set; }

    public bool IsDeleted { get; set; }
}