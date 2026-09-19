using HyperVToolsX.Core.Collection;
using HyperVToolsX.Core.Models;

namespace HyperVToolsX.Infrastructure.Collection;

public static class RemoteInventoryMapper
{
    public static HyperVTarget MapToTarget(
        HyperVTarget target,
        RemoteInventoryPackage package)
    {
        if (target == null)
        {
            throw new ArgumentNullException(nameof(target));
        }

        if (package == null)
        {
            throw new ArgumentNullException(nameof(package));
        }

        target.Hosts.Clear();
        target.VirtualMachines.Clear();
        target.Processors.Clear();
        target.Memories.Clear();
        target.NetworkAdapters.Clear();
        target.NetworkVlans.Clear();
        target.Checkpoints.Clear();
        target.IntegrationServices.Clear();
        target.VmStorage.Clear();
        target.Disks.Clear();
        target.Vhds.Clear();
        target.Replication.Clear();
        target.Dvds.Clear();
        target.HostStorage.Clear();
        target.OperatingSystems.Clear();
        target.Clusters.Clear();

        target.Hosts.AddRange(package.Hosts);
        target.VirtualMachines.AddRange(package.VirtualMachines);
        target.Processors.AddRange(package.Processors);
        target.Memories.AddRange(package.Memories);
        target.NetworkAdapters.AddRange(package.NetworkAdapters);
        target.NetworkVlans.AddRange(package.NetworkVlans);
        target.Checkpoints.AddRange(package.Checkpoints);
        target.IntegrationServices.AddRange(package.IntegrationServices);
        target.VmStorage.AddRange(package.VmStorage);
        target.Disks.AddRange(package.Disks);
        target.Vhds.AddRange(package.Vhds);
        target.Replication.AddRange(package.Replication);
        target.Dvds.AddRange(package.Dvds);
        target.HostStorage.AddRange(package.HostStorage);
        target.OperatingSystems.AddRange(package.OperatingSystems);
        target.Clusters.AddRange(package.Clusters);

        return target;
    }
}