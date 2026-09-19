using HyperVToolsX.Core.Enums;
using HyperVToolsX.Core.Models.Details;

namespace HyperVToolsX.Core.Models;

public class HyperVTarget
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public TargetType Type { get; set; }
    public ConnectionStatus Status { get; set; } = ConnectionStatus.Disconnected;
    public TargetValidationResult Validation { get; set; } = new();
    public DateTime? LastSuccessfulScan { get; set; }

    /// <summary>
    /// For a cluster target: the data of each node on its own (the lists below hold all nodes combined).
    /// Empty for anything else.
    /// </summary>
    public List<HyperVTarget> NodeTargets { get; set; } = [];

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
}