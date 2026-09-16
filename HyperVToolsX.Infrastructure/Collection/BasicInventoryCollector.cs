using HyperVToolsX.Core.Collection;
using HyperVToolsX.Core.Interfaces;
using HyperVToolsX.Core.Models;

namespace HyperVToolsX.Infrastructure.Collection;
public class BasicInventoryCollector : IInventoryCollector
{
    private readonly IHyperVProvider _hyperVProvider;
    public BasicInventoryCollector(IHyperVProvider hyperVProvider)
    {
        _hyperVProvider = hyperVProvider;
    }

    public async Task<HyperVTarget> CollectAsync( HyperVTarget target, CollectionRequest request, IProgress<CollectionProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        target.Hosts.Clear();
        target.VirtualMachines.Clear();
        target.NetworkAdapters.Clear();
        target.Processors.Clear();
        target.Memories.Clear();
        target.Disks.Clear();
        target.NetworkVlans.Clear();
        target.Checkpoints.Clear();
        target.IntegrationServices.Clear();

        var host = await _hyperVProvider.GetHostAsync( target.Name, cancellationToken);
        host.ClusterName = target.Validation.ClusterName ?? string.Empty;
        host.IsClusterNode = target.Validation.IsCluster;
        host.IsConnected = true;

        var virtualMachines = await _hyperVProvider.GetVirtualMachinesAsync( target.Name, cancellationToken);

        foreach (var vm in virtualMachines)
        {
            if (string.IsNullOrWhiteSpace(vm.HostName))
            {
                vm.HostName = host.Name;
            }
            vm.ClusterName = target.Validation.ClusterName ?? string.Empty;
        }

        host.VirtualMachineCount = virtualMachines.Count;

        target.Hosts.Add(host);
        target.VirtualMachines.AddRange(virtualMachines);

        if (request.CollectCpu)
        {
            var processors = await _hyperVProvider.GetVmProcessorsAsync( target.Name,cancellationToken);

            foreach (var processor in processors)
            {
                if (string.IsNullOrWhiteSpace(processor.HostName))
                {
                    processor.HostName = host.Name;
                }
            }
            target.Processors.AddRange(processors);
        }
        if (request.CollectMemory)
        {
            var memories =await _hyperVProvider.GetVmMemoryAsync( target.Name,cancellationToken);

            foreach (var memory in memories)
            {
                if (string.IsNullOrWhiteSpace(memory.HostName))
                {
                    memory.HostName = host.Name;
                }
            }

            target.Memories.AddRange(memories);
        }
        if (request.CollectNetwork)
        {
            var networkAdapters = await _hyperVProvider.GetNetworkAdaptersAsync( target.Name, cancellationToken);
            foreach (var adapter in networkAdapters)
            {
                adapter.HostName = host.Name;
            }

            target.NetworkAdapters.AddRange(networkAdapters);

            var networkVlans =await _hyperVProvider.GetVmNetworkVlansAsync(target.Name,cancellationToken);
            target.NetworkVlans.AddRange(networkVlans);
        }
        if (request.CollectStorage)
        {
            var disks = await _hyperVProvider.GetVmDisksAsync( target.Name, cancellationToken);

            foreach (var disk in disks)
            {
                if (string.IsNullOrWhiteSpace(disk.HostName))
                {
                    disk.HostName = host.Name;
                }
            }
            target.Disks.AddRange(disks);
        }
        if (request.CollectCheckpoints)
        {
            var checkpoints =
                await _hyperVProvider.GetVmCheckpointsAsync(
                    target.Name,
                    cancellationToken);

            target.Checkpoints.AddRange(checkpoints);
        }
        if (request.CollectIntegrationServices)
        {
            var integrationServices =
                await _hyperVProvider.GetVmIntegrationServicesAsync(
                    target.Name,
                    cancellationToken);

            foreach (var service in integrationServices)
            {
                if (string.IsNullOrWhiteSpace(service.HostName))
                {
                    service.HostName = host.Name;
                }
            }

            target.IntegrationServices.AddRange(integrationServices);
        }


        progress?.Report(
            new CollectionProgress
            {
                TotalTargets = 1,
                CompletedTargets = 1,
                TotalHosts = 1,
                TotalVirtualMachines = virtualMachines.Count,
                CurrentTarget = target.Name,
                CurrentStage = "Basic collection completed"
            });

        return target;
    }
}