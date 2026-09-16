namespace HyperVToolsX.Core.Models.Details;

public class VmIntegrationServiceInfo
{
    public string VmName { get; set; } = string.Empty;

    public string HostName { get; set; } = string.Empty;

    public string VMId { get; set; } = string.Empty;

    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public bool Enabled { get; set; }

    public List<string> OperationalStatus { get; set; } = [];

    public string OperationalStatusDisplay =>
        string.Join(", ", OperationalStatus);

    public string PrimaryOperationalStatus { get; set; } = string.Empty;

    public string PrimaryStatusDescription { get; set; } = string.Empty;

    public string SecondaryOperationalStatus { get; set; } = string.Empty;

    public string SecondaryStatusDescription { get; set; } = string.Empty;

    public List<string> StatusDescription { get; set; } = [];

    public string StatusDescriptionDisplay =>
        string.Join(", ", StatusDescription);



    public string VMCheckpointId { get; set; } = string.Empty;

    public string VMCheckpointName { get; set; } = string.Empty;

    public string VMSnapshotId { get; set; } = string.Empty;

    public string VMSnapshotName { get; set; } = string.Empty;

    public bool IsClustered { get; set; }

    public bool IsDeleted { get; set; }
}