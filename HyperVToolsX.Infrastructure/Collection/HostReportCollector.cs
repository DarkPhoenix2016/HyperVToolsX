using HyperVToolsX.Core.Interfaces;
using HyperVToolsX.Core.Models;
using HyperVToolsX.Core.Models.Reports;

namespace HyperVToolsX.Infrastructure.Collection;

public class HostReportCollector : IHostReportCollector
{
    private readonly IHyperVProvider _hyperVProvider;

    public HostReportCollector(IHyperVProvider hyperVProvider)
    {
        _hyperVProvider = hyperVProvider;
    }

    public async Task<HostReport> CollectAsync(
        string computerName,
        string clusterName = "",
        CancellationToken cancellationToken = default)
    {
        var report = new HostReport
        {
            HostName = computerName,
            ComputerName = computerName,
            ClusterName = clusterName,
            IsClusterNode = !string.IsNullOrWhiteSpace(clusterName),
            CollectedAt = DateTime.Now
        };

        cancellationToken.ThrowIfCancellationRequested();

        var host = await _hyperVProvider.GetHostAsync(
            computerName,
            cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();

        var virtualMachines = await _hyperVProvider.GetVirtualMachinesAsync(
            computerName,
            cancellationToken);

        foreach (var vm in virtualMachines)
        {
            if (string.IsNullOrWhiteSpace(vm.HostName))
            {
                vm.HostName = host.Name;
            }

            vm.ClusterName = clusterName;
        }

        host.ClusterName = clusterName;
        host.IsClusterNode = !string.IsNullOrWhiteSpace(clusterName);
        host.IsConnected = true;
        host.VirtualMachineCount = virtualMachines.Count;

        report.VirtualMachines.AddRange(virtualMachines);

        cancellationToken.ThrowIfCancellationRequested();

        var memories = await _hyperVProvider.GetVmMemoryAsync(
            computerName,
            cancellationToken);

        foreach (var memory in memories)
        {
            if (string.IsNullOrWhiteSpace(memory.HostName))
            {
                memory.HostName = host.Name;
            }
        }

        report.Memories.AddRange(memories);

        return report;
    }
}