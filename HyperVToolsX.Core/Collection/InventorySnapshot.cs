using HyperVToolsX.Core.Models;
using HyperVToolsX.Core.Models.Details;

namespace HyperVToolsX.Core.Collection;

public class InventorySnapshot
{
    public List<HyperVTarget> Targets { get; set; } = [];
    public List<HyperVHost> Hosts { get; set; } = [];
    public List<HyperVVirtualMachine> VirtualMachines { get; set; } = [];
    public List<VmNetworkAdapter> NetworkAdapters { get; set; } = [];
    public List<VmProcessorInfo> Processors { get; set; } = [];
    public List<VmMemoryInfo> Memories { get; set; } = [];
    public List<VmDiskInfo> Disks { get; set; } = [];
    public List<VhdInfo> Vhds { get; set; } = [];
    public List<VmNetworkVlanInfo> NetworkVlans { get; set; } = [];
    public List<VmCheckpointInfo> Checkpoints { get; set; } = [];
    public List<VmIntegrationServiceInfo> IntegrationServices { get; set; } = [];
    public List<VmStorageInfo> VmStorage { get; set; } = [];
    public List<VmReplicationInfo> Replication { get; set; } = [];
    public List<VmDvdInfo> Dvds { get; set; } = [];
    public List<HostStorageInfo> HostStorage { get; set; } = [];
    public List<OperatingSystemInfo> OperatingSystems { get; set; } = [];
    public List<ClusterInfo> Clusters { get; set; } = [];
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public int TargetCount => Targets.Count;
    public int HostCount => Hosts.Count;
    public int VirtualMachineCount => VirtualMachines.Count;
    public int ProcessorCount => Processors.Count;
    public int MemoryCount => Memories.Count;
    public int DiskCount => Disks.Count;
    public int NetworkAdapterCount => NetworkAdapters.Count;
    public int CheckpointCount => Checkpoints.Count;
    public int IntegrationServiceCount => IntegrationServices.Count;
    public int DvdCount => Dvds.Count;
}