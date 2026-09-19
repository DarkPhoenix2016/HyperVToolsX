using HyperVToolsX.Core.Models;

namespace HyperVToolsX.Core.Collection;

/// <summary>
/// Joins hosts with their storage/OS details into the flat rows shown on the
/// vHost tab. Shared by the UI, custom tabs and the Excel export.
/// </summary>
public static class HostInventoryBuilder
{
    public static List<HostInventoryRow> Build(InventorySnapshot snapshot)
    {
        var rows = new List<HostInventoryRow>(snapshot.Hosts.Count);

        foreach (var host in snapshot.Hosts)
        {
            var storage = snapshot.HostStorage.FirstOrDefault(x =>
                string.Equals(x.HostName, host.Name, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(x.ComputerName, host.Name, StringComparison.OrdinalIgnoreCase));

            var operatingSystem = snapshot.OperatingSystems.FirstOrDefault(x =>
                string.Equals(x.ComputerName, host.Name, StringComparison.OrdinalIgnoreCase));

            rows.Add(new HostInventoryRow
            {
                HostName = host.Name,
                Fqdn = host.Fqdn,
                ClusterName = host.ClusterName,
                IsClusterNode = host.IsClusterNode,
                IsClusterOwner = host.IsClusterOwner,
                IsConnected = host.IsConnected,

                HyperVVersion = host.HyperVVersion,
                LogicalProcessorCount = host.LogicalProcessorCount,
                VirtualMachineCount = host.VirtualMachineCount,

                TotalMemoryBytes = host.TotalMemoryBytes,
                UsedMemoryBytes = host.UsedMemoryBytes,

                OperatingSystem = operatingSystem?.Caption ?? host.OperatingSystem,
                OSVersion = operatingSystem?.Version ?? string.Empty,
                OSBuildNumber = operatingSystem?.BuildNumber ?? string.Empty,
                OSArchitecture = operatingSystem?.OSArchitecture ?? string.Empty,
                LastBootUpTime = operatingSystem?.LastBootUpTime,

                TotalVisibleMemorySizeKb = operatingSystem?.TotalVisibleMemorySizeKb ?? 0,
                FreePhysicalMemoryKb = operatingSystem?.FreePhysicalMemoryKb ?? 0,

                VirtualHardDiskPath = storage?.VirtualHardDiskPath ?? string.Empty,
                VirtualMachinePath = storage?.VirtualMachinePath ?? string.Empty,
                ParentSnapshotPath = storage?.ParentSnapshotPath ?? string.Empty,

                MaximumStorageMigrations = storage?.MaximumStorageMigrations ?? 0,
                MaximumVirtualMachineMigrations = storage?.MaximumVirtualMachineMigrations ?? 0,
                VirtualMachineMigrationEnabled = storage?.VirtualMachineMigrationEnabled ?? false,
                VirtualMachineMigrationAuthenticationType =
                    storage?.VirtualMachineMigrationAuthenticationType ?? string.Empty,
                VirtualMachineMigrationPerformanceOption =
                    storage?.VirtualMachineMigrationPerformanceOption ?? string.Empty,
                UseAnyNetworkForMigration = storage?.UseAnyNetworkForMigration ?? false,

                EnableEnhancedSessionMode = storage?.EnableEnhancedSessionMode ?? false,
                IsDeleted = storage?.IsDeleted ?? false
            });
        }

        return rows;
    }
}
