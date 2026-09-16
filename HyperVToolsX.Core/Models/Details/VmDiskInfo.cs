namespace HyperVToolsX.Core.Models.Details;

public class VmDiskInfo
{
    public string VmName { get; set; } = string.Empty;

    public string HostName { get; set; } = string.Empty;

    public string Path { get; set; } = string.Empty;

    public string DiskNumber { get; set; } = string.Empty;

    public long MaximumIOPS { get; set; }

    public long MinimumIOPS { get; set; }

    public string QoSPolicyID { get; set; } = string.Empty;

    public bool SupportPersistentReservations { get; set; }

    public string WriteHardeningMethod { get; set; } =
        string.Empty;

    public int ControllerLocation { get; set; }

    public int ControllerNumber { get; set; }

    public string ControllerType { get; set; } =
        string.Empty;

    public string Name { get; set; } = string.Empty;

    public string PoolName { get; set; } = string.Empty;
}