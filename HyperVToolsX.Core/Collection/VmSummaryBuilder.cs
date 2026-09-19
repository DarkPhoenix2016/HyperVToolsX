using HyperVToolsX.Core.Models;

namespace HyperVToolsX.Core.Collection;

public static class VmSummaryBuilder
{
    public static IReadOnlyList<VmSummaryRow> Build(
        InventorySnapshot snapshot)
    {
        return snapshot.VirtualMachines
            .Select(vm =>
            {
                var networkAdapters =
                    snapshot.NetworkAdapters
                        .Where(adapter =>
                            adapter.VmName.Equals(
                                vm.Name,
                                StringComparison.OrdinalIgnoreCase))
                        .ToList();

                var ipAddresses =
                    networkAdapters
                        .SelectMany(adapter => adapter.IpAddresses)
                        .Where(ip => !string.IsNullOrWhiteSpace(ip))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();

                return new VmSummaryRow
                {
                    VmName = vm.Name,
                    HostName = vm.HostName,
                    ClusterName = vm.ClusterName,
                    IpAddress = string.Join(
                        ", ",
                        ipAddresses),
                    State = vm.State,
                    ProcessorCount = vm.ProcessorCount,
                    MemoryBytes = vm.StartupMemoryBytes,
                    Generation = vm.Generation,
                    Version = vm.ConfigurationVersion,
                    BootTime = vm.BootTime,
                    Uptime = FormatUptime(vm.Uptime),
                };
            })
            .ToList();
    }
    private static string FormatUptime(TimeSpan? uptime)
    {
        if (!uptime.HasValue || uptime.Value <= TimeSpan.Zero)
        {
            return "-";
        }

        var ts = uptime.Value;
        var parts = new List<string>();

        if (ts.Days > 0)
        {
            parts.Add($"{ts.Days} {(ts.Days == 1 ? "day" : "days")}");
        }

        if (ts.Hours > 0)
        {
            parts.Add($"{ts.Hours} {(ts.Hours == 1 ? "hour" : "hours")}");
        }

        if (ts.Minutes > 0 && ts.Days == 0)
        {
            parts.Add($"{ts.Minutes} {(ts.Minutes == 1 ? "min" : "mins")}");
        }

        return parts.Count > 0 ? string.Join(", ", parts) : "< 1 min";
    }
}