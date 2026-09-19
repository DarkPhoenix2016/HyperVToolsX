namespace HyperVToolsX.Core.Models.Details;

public class VmDvdInfo
{
    public string VmName { get; set; } = string.Empty;

    public string HostName { get; set; } = string.Empty;

    public string VMId { get; set; } = string.Empty;

    public string Id { get; set; } = string.Empty;

    public string Path { get; set; } = string.Empty;

    public string DvdMediaType { get; set; } = string.Empty;

    public int ControllerLocation { get; set; }

    public int ControllerNumber { get; set; }

    public string ControllerType { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string PoolName { get; set; } = string.Empty;

    public string VMCheckpointId { get; set; } = string.Empty;

    public string VMCheckpointName { get; set; } = string.Empty;

    public string VMSnapshotId { get; set; } = string.Empty;

    public string VMSnapshotName { get; set; } = string.Empty;

    public bool IsDeleted { get; set; }
}