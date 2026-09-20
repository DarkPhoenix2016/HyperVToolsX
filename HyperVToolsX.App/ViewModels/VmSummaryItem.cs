using HyperVToolsX.App.Converters;
using HyperVToolsX.Core.Collection;

namespace HyperVToolsX.App.ViewModels;

/// <summary>One row of the vSummary tab: the headline facts about a VM.</summary>
public sealed class VmSummaryItem
{
    public string Name { get; init; } = string.Empty;
    public string VMId { get; init; } = string.Empty;
    public string HostName { get; init; } = string.Empty;
    public string ClusterName { get; init; } = string.Empty;
    public string State { get; init; } = string.Empty;
    public int Generation { get; init; }
    public string IPv4Addresses { get; init; } = string.Empty;
    public int CpuUsage { get; init; }
    public int ProcessorCount { get; init; }
    public string MemoryStatus { get; init; } = string.Empty;
    public long MemoryAssigned { get; init; }
    public long MemoryDemand { get; init; }
    public long MemoryStartup { get; init; }
    public string Notes { get; init; } = string.Empty;
    public string NicNames { get; init; } = string.Empty;
    public string SwitchNames { get; init; } = string.Empty;
    public string MacAddresses { get; init; } = string.Empty;
    public string VlanModes { get; init; } = string.Empty;
    public string VlanLists { get; init; } = string.Empty;
    public string StoragePaths { get; init; } = string.Empty;

    public int CheckpointCount { get; init; }
    public TimeSpan? Uptime { get; init; }
    public DateTime? BootTime { get; init; }

    /// <summary>Currently assigned memory as a percentage of the startup memory (over 100% when dynamic memory has grown).</summary>
    public double MemoryUsagePercent =>
        MemoryStartup <= 0 ? 0 : Math.Round(MemoryAssigned * 100d / MemoryStartup, 1);

    public string MemoryUsageText => $"{MemoryUsagePercent:0.#}%";

    public string CpuUsageText => $"{CpuUsage}%";

    public string UptimeText => Uptime is { } up && up > TimeSpan.Zero
        ? $"{(int)up.TotalDays}d {up.Hours:00}h {up.Minutes:00}m"
        : "0d 00h 00m";

    public string BootTimeText => BootTime is { } boot ? boot.ToString("yyyy-MM-dd HH:mm:ss") : "-";

    public static List<VmSummaryItem> Build(InventorySnapshot snapshot)
    {
        var details = VmSummaryDetailsBuilder.Build(snapshot);

        var checkpoints = snapshot.Checkpoints
            .GroupBy(c => VmSummaryDetailsBuilder.Key(c.HostName, c.VmName))
            .ToDictionary(g => g.Key, g => g.Count());

        return snapshot.VirtualMachines
            .Select(vm =>
            {
                var d = details.GetValueOrDefault(VmSummaryDetailsBuilder.Key(vm.HostName, vm.Name));

                return new VmSummaryItem
                {
                    Name = vm.Name,
                    VMId = vm.VMId.ToString(),
                    HostName = vm.HostName,
                    ClusterName = vm.ClusterName,
                    State = vm.State,
                    Generation = vm.Generation,
                    IPv4Addresses = d?.IpAddresses ?? string.Empty,
                    CpuUsage = vm.CPUUsage,
                    ProcessorCount = vm.ProcessorCount,
                    MemoryStatus = vm.MemoryStatus,
                    MemoryAssigned = vm.MemoryAssigned,
                    MemoryDemand = vm.MemoryDemand,
                    MemoryStartup = vm.MemoryStartup,
                    CheckpointCount = checkpoints.GetValueOrDefault(VmSummaryDetailsBuilder.Key(vm.HostName, vm.Name)),
                    Uptime = vm.Uptime,
                    BootTime = vm.BootTime,
                    Notes = d?.Notes ?? string.Empty,
                    NicNames = d?.NicNames ?? string.Empty,
                    SwitchNames = d?.SwitchNames ?? string.Empty,
                    MacAddresses = d?.MacAddresses ?? string.Empty,
                    VlanModes = d?.VlanModes ?? string.Empty,
                    VlanLists = d?.VlanLists ?? string.Empty,
                    StoragePaths = d?.StoragePaths ?? string.Empty
                };
            })
            .ToList();
    }
}
