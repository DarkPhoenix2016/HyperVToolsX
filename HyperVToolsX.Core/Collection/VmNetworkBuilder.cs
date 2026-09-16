using HyperVToolsX.Core.Models;

namespace HyperVToolsX.Core.Collection;

public static class VmNetworkBuilder
{
    public static IReadOnlyList<VmNetworkRow> Build(
        InventorySnapshot snapshot)
    {
        return snapshot.NetworkAdapters
            .Select(adapter =>
            {
                var vm =
                    snapshot.VirtualMachines
                        .FirstOrDefault(x =>
                            x.Name.Equals(
                                adapter.VmName,
                                StringComparison.OrdinalIgnoreCase) &&
                            x.HostName.Equals(
                                adapter.HostName,
                                StringComparison.OrdinalIgnoreCase));

                return new VmNetworkRow
                {
                    VmName = adapter.VmName,

                    HostName = adapter.HostName,

                    ClusterName =
                        vm?.ClusterName
                        ?? string.Empty,

                    State =
                        vm?.State
                        ?? string.Empty,

                    AdapterName = adapter.Name,

                    SwitchName = adapter.SwitchName,

                    MacAddress = adapter.MacAddress,

                    IpAddress =
                        string.Join(
                            ", ",
                            adapter.IpAddresses),

                    VlanMode = adapter.VlanMode,

                    VlanList = adapter.VlanList,

                    Status = adapter.Status
                };
            })
            .ToList();
    }
}