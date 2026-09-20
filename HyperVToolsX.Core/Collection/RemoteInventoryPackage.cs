using HyperVToolsX.Core.Models;
using HyperVToolsX.Core.Models.Details;

namespace HyperVToolsX.Core.Collection;

public class RemoteInventoryPackage
{
    public string ComputerName { get; set; } = string.Empty;

    public List<HyperVHost> Hosts { get; set; } = [];

    public List<HyperVVirtualMachine> VirtualMachines { get; set; } = [];

    public List<VmProcessorInfo> Processors { get; set; } = [];

    public List<VmMemoryInfo> Memories { get; set; } = [];

    public List<VmNetworkAdapter> NetworkAdapters { get; set; } = [];

    public List<VmNetworkVlanInfo> NetworkVlans { get; set; } = [];

    public List<VmCheckpointInfo> Checkpoints { get; set; } = [];

    public List<VmIntegrationServiceInfo> IntegrationServices { get; set; } = [];

    public List<VmStorageInfo> VmStorage { get; set; } = [];

    public List<VmDiskInfo> Disks { get; set; } = [];

    public List<VhdInfo> Vhds { get; set; } = [];

    public List<VmReplicationInfo> Replication { get; set; } = [];

    public List<VmDvdInfo> Dvds { get; set; } = [];

    public List<HostStorageInfo> HostStorage { get; set; } = [];

    public List<OperatingSystemInfo> OperatingSystems { get; set; } = [];

    public List<ClusterInfo> Clusters { get; set; } = [];

    public bool Success { get; set; }

    public string ErrorMessage { get; set; } = string.Empty;

    /// <summary>Sections the script could not collect (the rest of the host's data is still valid).</summary>
    public List<string> Warnings { get; set; } = [];
}