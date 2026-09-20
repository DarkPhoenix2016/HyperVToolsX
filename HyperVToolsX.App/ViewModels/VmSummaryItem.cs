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
    public string IPv4Addresses { get; init; } = string.Empty;
    public int CpuUsage { get; init; }
    public int ProcessorCount { get; init; }
    public string MemoryStatus { get; init; } = string.Empty;
    public long MemoryAssigned { get; init; }
    public long MemoryDemand { get; init; }
    public TimeSpan? Uptime { get; init; }
    public DateTime? BootTime { get; init; }

    /// <summary>Formatted with the size unit chosen in Preferences, so it is re-read on refresh.</summary>
    public string MemoryUsageText =>
        $"{ByteSizeConverter.FormatBytes(MemoryDemand)} / {ByteSizeConverter.FormatBytes(MemoryAssigned)}";

    public double MemoryUsagePercent =>
        MemoryAssigned <= 0 ? 0 : Math.Min(100, MemoryDemand * 100d / MemoryAssigned);

    public string CpuUsageText => $"{CpuUsage}%";

    public string UptimeText => Uptime is { } up && up > TimeSpan.Zero
        ? $"{(int)up.TotalDays}d {up.Hours:00}h {up.Minutes:00}m"
        : "0d 00h 00m";

    public string BootTimeText => BootTime is { } boot ? boot.ToString("yyyy-MM-dd HH:mm:ss") : "-";

    public static List<VmSummaryItem> Build(InventorySnapshot snapshot)
    {
        var ipsByVm = snapshot.NetworkAdapters
            .GroupBy(a => VmKey(a.HostName, a.VmName))
            .ToDictionary(
                g => g.Key,
                g => string.Join(", ", g
                    .SelectMany(a => a.IPv4Addresses)
                    .Where(ip => !string.IsNullOrWhiteSpace(ip))
                    .Distinct(StringComparer.OrdinalIgnoreCase)));

        return snapshot.VirtualMachines
            .Select(vm => new VmSummaryItem
            {
                Name = vm.Name,
                VMId = vm.VMId.ToString(),
                HostName = vm.HostName,
                ClusterName = vm.ClusterName,
                State = vm.State,
                IPv4Addresses = ipsByVm.GetValueOrDefault(VmKey(vm.HostName, vm.Name), string.Empty),
                CpuUsage = vm.CPUUsage,
                ProcessorCount = vm.ProcessorCount,
                MemoryStatus = vm.MemoryStatus,
                MemoryAssigned = vm.MemoryAssigned,
                MemoryDemand = vm.MemoryDemand,
                Uptime = vm.Uptime,
                BootTime = vm.BootTime
            })
            .ToList();
    }

    private static string VmKey(string host, string vm) => $"{host}|{vm}".ToLowerInvariant();
}
