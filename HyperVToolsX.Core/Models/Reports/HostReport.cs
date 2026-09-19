using HyperVToolsX.Core.Models.Details;

namespace HyperVToolsX.Core.Models.Reports;

public class HostReport
{
    // Host identity
    public string HostName { get; set; } = string.Empty;

    public string ComputerName { get; set; } = string.Empty;

    public string ClusterName { get; set; } = string.Empty;

    public bool IsClusterNode { get; set; }

    public DateTime CollectedAt { get; set; }

    // Host-level information
    public HostStorageInfo? HostStorage { get; set; }

    public OperatingSystemInfo? OperatingSystem { get; set; }

    public ClusterInfo? Cluster { get; set; }

    // VM-level information
    public List<HyperVVirtualMachine> VirtualMachines { get; set; } = [];

    public List<VmProcessorInfo> Processors { get; set; } = [];

    public List<VmMemoryInfo> Memories { get; set; } = [];

    public List<VmDiskInfo> Disks { get; set; } = [];

    public List<VhdInfo> Vhds { get; set; } = [];

    public List<VmNetworkAdapter> NetworkAdapters { get; set; } = [];

    public List<VmNetworkVlanInfo> NetworkVlans { get; set; } = [];

    public List<VmCheckpointInfo> Checkpoints { get; set; } = [];

    public List<VmIntegrationServiceInfo> IntegrationServices { get; set; } = [];

    public List<VmStorageInfo> VmStorage { get; set; } = [];

    public List<VmReplicationInfo> Replication { get; set; } = [];

    public List<VmDvdInfo> Dvds { get; set; } = [];
}