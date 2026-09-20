using HyperVToolsX.Core.Models;
using HyperVToolsX.Core.Models.Details;

namespace HyperVToolsX.Core.Collection;

/// <summary>
/// Per-VM values for the vSummary tab that come from other inventory tables (NICs, VLANs, disks).
/// One VM is one row, so a VM with several adapters or disks gets every value in a single cell,
/// separated by <see cref="Separator"/>. Per-adapter columns (name, switch, MAC, VLAN mode, VLAN list)
/// list the adapters in the same order, so the n-th entry of each belongs to the same adapter.
/// </summary>
public sealed record VmSummaryDetails(
    string NicNames,
    string SwitchNames,
    string IpAddresses,
    string MacAddresses,
    string VlanModes,
    string VlanLists,
    string StoragePaths,
    string Notes);

public static class VmSummaryDetailsBuilder
{
    public const string Separator = "; ";

    /// <summary>Shown for an adapter that has no value in a per-adapter column, to keep the entries aligned.</summary>
    public const string Missing = "-";

    public static string Key(string host, string vm) => $"{host}|{vm}".ToLowerInvariant();

    public static Dictionary<string, VmSummaryDetails> Build(InventorySnapshot snapshot)
    {
        var adaptersByVm = snapshot.NetworkAdapters.ToLookup(a => Key(a.HostName, a.VmName));
        var disksByVm = snapshot.Disks.ToLookup(d => Key(d.HostName, d.VmName));
        var vlans = snapshot.NetworkVlans.ToLookup(v => VlanKey(v.VmName, v.AdapterName));

        var result = new Dictionary<string, VmSummaryDetails>();

        foreach (var vm in snapshot.VirtualMachines)
        {
            var key = Key(vm.HostName, vm.Name);

            if (result.ContainsKey(key))
            {
                continue;
            }

            var adapters = adaptersByVm[key].ToList();

            var vlanByAdapter = adapters
                .Select(a => vlans[VlanKey(a.VmName, a.Name)].FirstOrDefault())
                .ToList();

            result[key] = new VmSummaryDetails(
                NicNames: PerAdapter(adapters.Select(a => a.Name)),
                SwitchNames: PerAdapter(adapters.Select(a => a.SwitchName)),
                IpAddresses: Join(adapters.SelectMany(a => a.IPv4Addresses)),
                MacAddresses: PerAdapter(adapters.Select(a => a.MacAddress)),
                VlanModes: PerAdapter(vlanByAdapter.Select(v => v?.OperationMode)),
                VlanLists: PerAdapter(vlanByAdapter.Select(v => v is null ? null : FormatVlanList(v))),
                StoragePaths: Join(disksByVm[key].Select(d => d.Path)),
                Notes: FormatNotes(vm));
        }

        return result;
    }

    /// <summary>The VLAN IDs an adapter uses: the access ID, a trunk's allowed list, or the isolation IDs.</summary>
    public static string FormatVlanList(VmNetworkVlanInfo vlan)
    {
        var mode = vlan.OperationMode?.Trim() ?? string.Empty;

        if (mode.Equals("Access", StringComparison.OrdinalIgnoreCase))
        {
            return vlan.AccessVlanId > 0 ? vlan.AccessVlanId.ToString() : string.Empty;
        }

        if (mode.Equals("Trunk", StringComparison.OrdinalIgnoreCase))
        {
            var allowed = !string.IsNullOrWhiteSpace(vlan.AllowedVlanIdListString)
                ? vlan.AllowedVlanIdListString.Trim()
                : string.Join(",", vlan.AllowedVlanIdList);

            return vlan.NativeVlanId > 0 ? $"{allowed} (native {vlan.NativeVlanId})" : allowed;
        }

        if (mode.Equals("Isolation", StringComparison.OrdinalIgnoreCase))
        {
            return $"{vlan.PrimaryVlanId}/{(string.IsNullOrWhiteSpace(vlan.SecondaryVlanIdListString) ? vlan.SecondaryVlanId.ToString() : vlan.SecondaryVlanIdListString)}";
        }

        return string.Empty;
    }

    /// <summary>The VM's own notes, plus a marker for the "Critical" states where its storage is unreachable.</summary>
    public static string FormatNotes(HyperVVirtualMachine vm)
    {
        var parts = new List<string>();

        if (vm.State?.EndsWith("Critical", StringComparison.OrdinalIgnoreCase) == true)
        {
            parts.Add($"VM configuration storage unavailable ({vm.State})");
        }

        if (!string.IsNullOrWhiteSpace(vm.Notes))
        {
            parts.Add(vm.Notes.ReplaceLineEndings(" ").Trim());
        }

        return string.Join(Separator, parts);
    }

    private static string VlanKey(string vm, string adapter) => $"{vm}|{adapter}".ToLowerInvariant();

    private static string Join(IEnumerable<string?> values) =>
        string.Join(
            Separator,
            values
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Select(v => v!.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase));

    /// <summary>One entry per adapter, in adapter order (no de-duplication, so entries stay aligned).</summary>
    private static string PerAdapter(IEnumerable<string?> values)
    {
        var list = values.Select(v => string.IsNullOrWhiteSpace(v) ? Missing : v.Trim()).ToList();

        return list.All(v => v == Missing) ? string.Empty : string.Join(Separator, list);
    }
}
