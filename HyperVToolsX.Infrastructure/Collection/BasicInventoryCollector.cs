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

    public async Task<HyperVTarget> CollectAsync(
        HyperVTarget target,
        CollectionRequest request,
        IProgress<CollectionProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        target.Hosts.Clear();
        target.HostStorage.Clear();
        target.OperatingSystems.Clear();


        target.VirtualMachines.Clear();
        target.NetworkAdapters.Clear();
        target.Processors.Clear();
        target.Memories.Clear();
        target.NetworkVlans.Clear();
        target.Checkpoints.Clear();
        target.IntegrationServices.Clear();
        target.VmStorage.Clear();
        target.Disks.Clear();
        target.Vhds.Clear();
        target.Replication.Clear();
        target.Vhds.Clear();


        target.Clusters.Clear();

        cancellationToken.ThrowIfCancellationRequested();

        var host = await _hyperVProvider.GetHostAsync(
            target.Name,
            cancellationToken);

        host.ClusterName =
            target.Validation.ClusterName ?? string.Empty;

        host.IsClusterNode =
            target.Validation.IsCluster;

        host.IsConnected = true;
        
        target.Hosts.Add(host);

        // =========================================================
        // HOST DETAILS
        // =========================================================

        var hostStorage =
            await _hyperVProvider.GetHostStorageAsync(
                host.Name,
                cancellationToken);

        target.HostStorage.Add(hostStorage);

        var operatingSystem =
            await _hyperVProvider.GetOperatingSystemAsync(
                host.Name,
                cancellationToken);

        target.OperatingSystems.Add(operatingSystem);



        var virtualMachines =
            await _hyperVProvider.GetVirtualMachinesAsync(
                target.Name,
                cancellationToken);

        foreach (var vm in virtualMachines)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(vm.HostName))
            {
                vm.HostName = host.Name;
            }

            vm.ClusterName =
                target.Validation.ClusterName ?? string.Empty;
        }

        host.VirtualMachineCount = virtualMachines.Count;

        target.VirtualMachines.AddRange(virtualMachines);

        // CPU
        if (request.CollectCpu)
        {
            var processors =
                await _hyperVProvider.GetVmProcessorsAsync(
                    target.Name,
                    cancellationToken);

            foreach (var processor in processors)
            {
                if (string.IsNullOrWhiteSpace(processor.HostName))
                {
                    processor.HostName = host.Name;
                }
            }

            target.Processors.AddRange(processors);
        }

        // Memory
        if (request.CollectMemory)
        {
            var memories =
                await _hyperVProvider.GetVmMemoryAsync(
                    target.Name,
                    cancellationToken);

            foreach (var memory in memories)
            {
                if (string.IsNullOrWhiteSpace(memory.HostName))
                {
                    memory.HostName = host.Name;
                }
            }

            target.Memories.AddRange(memories);
        }

        // Network
        if (request.CollectNetwork)
        {
            var networkAdapters =
                await _hyperVProvider.GetNetworkAdaptersAsync(
                    target.Name,
                    cancellationToken);

            foreach (var adapter in networkAdapters)
            {
                adapter.HostName = host.Name;
            }

            target.NetworkAdapters.AddRange(
                networkAdapters);

            var networkVlans =
                await _hyperVProvider.GetVmNetworkVlansAsync(
                    target.Name,
                    cancellationToken);

            target.NetworkVlans.AddRange(
                networkVlans);
        }

        // Checkpoints
        if (request.CollectCheckpoints)
        {
            var checkpoints =
                await _hyperVProvider.GetVmCheckpointsAsync(
                    target.Name,
                    cancellationToken);

            target.Checkpoints.AddRange(
                checkpoints);
        }

        // Integration Services
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

            target.IntegrationServices.AddRange(
                integrationServices);
        }

        // Storage
        if (request.CollectStorage)
        {
            var vmStorage =
                await _hyperVProvider.GetVmStorageAsync(
                    target.Name,
                    cancellationToken);

            var disks =
                await _hyperVProvider.GetVmDisksAsync(
                    target.Name,
                    cancellationToken);

            var vhds =
                await _hyperVProvider.GetVhdsAsync(
                    target.Name,
                    cancellationToken);

            target.VmStorage.AddRange(
                vmStorage);

            target.Disks.AddRange(
                disks);

            target.Vhds.AddRange(
                vhds);
        }

        // DVD
        var dvds =
    await _hyperVProvider.GetVmDvdsAsync(
        target.Name,
        cancellationToken);

        foreach (var dvd in dvds)
        {
            if (string.IsNullOrWhiteSpace(dvd.HostName))
            {
                dvd.HostName = host.Name;
            }
        }

        target.Dvds.AddRange(dvds);



        // Replication
        var replication =
            await _hyperVProvider.GetVmReplicationAsync(
                target.Name,
                cancellationToken);

        target.Replication.AddRange(
            replication);




        // Clusters
        if (target.Validation.IsCluster)
        {
            var clusters =
                await _hyperVProvider.GetClustersAsync(
                    target.Name,
                    cancellationToken);

            target.Clusters.AddRange(clusters);

                 }
        progress?.Report(
            new CollectionProgress
            {
                TotalTargets = 1,
                CompletedTargets = 1,
                TotalHosts = 1,
                TotalVirtualMachines =
                    virtualMachines.Count,
                CurrentTarget = target.Name,
                CurrentStage =
                    "Data collection completed"
            });

        return target;
    }
}