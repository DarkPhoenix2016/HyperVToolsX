using HyperVToolsX.Core.Collection;
using HyperVToolsX.Core.Models;
using HyperVToolsX.Core.Models.Details;
using Xunit;

namespace HyperVToolsX.Tests;

public class VmSummaryDetailsTests
{
    private static InventorySnapshot Snapshot() => new()
    {
        VirtualMachines =
        [
            new HyperVVirtualMachine { Name = "vm1", HostName = "HV01", State = "Running", Notes = "web\r\nfront" },
            new HyperVVirtualMachine { Name = "vm2", HostName = "HV01", State = "OffCritical" }
        ],
        NetworkAdapters =
        [
            new VmNetworkAdapter { VmName = "vm1", HostName = "HV01", Name = "Net A", SwitchName = "vSw1", MacAddress = "00-11", IPv4Addresses = ["10.0.0.5", "10.0.0.6"] },
            new VmNetworkAdapter { VmName = "vm1", HostName = "HV01", Name = "Net B", SwitchName = "vSw2", MacAddress = "00-22", IPv4Addresses = ["10.0.1.5", "10.0.0.5"] }
        ],
        NetworkVlans =
        [
            new VmNetworkVlanInfo { VmName = "vm1", AdapterName = "Net A", OperationMode = "Access", AccessVlanId = 20 },
            new VmNetworkVlanInfo { VmName = "vm1", AdapterName = "Net B", OperationMode = "Trunk", AllowedVlanIdListString = "10,30-40", NativeVlanId = 1 }
        ],
        Disks =
        [
            new VmDiskInfo { VmName = "vm1", HostName = "HV01", Path = @"C:\vm1\a.vhdx" },
            new VmDiskInfo { VmName = "vm1", HostName = "HV01", Path = @"D:\vm1\b.vhdx" },
            new VmDiskInfo { VmName = "vm1", HostName = "HV01", Path = @"C:\vm1\a.vhdx" },
            new VmDiskInfo { VmName = "vm2", HostName = "HV02", Path = @"X:\other.vhdx" }
        ]
    };

    [Fact]
    public void A_vm_with_several_adapters_and_disks_is_one_row_with_joined_cells()
    {
        var d = VmSummaryDetailsBuilder.Build(Snapshot())[VmSummaryDetailsBuilder.Key("HV01", "vm1")];

        Assert.Equal("Net A; Net B", d.NicNames);
        Assert.Equal("vSw1; vSw2", d.SwitchNames);
        Assert.Equal("00-11; 00-22", d.MacAddresses);
        Assert.Equal("10.0.0.5; 10.0.0.6; 10.0.1.5", d.IpAddresses);   // distinct
        Assert.Equal("Access; Trunk", d.VlanModes);
        Assert.Equal("20; 10,30-40 (native 1)", d.VlanLists);
        Assert.Equal(@"C:\vm1\a.vhdx; D:\vm1\b.vhdx", d.StoragePaths); // distinct
        Assert.Equal("web front", d.Notes);
    }

    [Fact]
    public void Vm_without_adapters_or_disks_has_empty_cells_and_critical_state_is_noted()
    {
        var d = VmSummaryDetailsBuilder.Build(Snapshot())[VmSummaryDetailsBuilder.Key("HV01", "vm2")];

        Assert.Equal(string.Empty, d.NicNames);
        Assert.Equal(string.Empty, d.StoragePaths);
        Assert.Equal("VM configuration storage unavailable (OffCritical)", d.Notes);
    }

    [Fact]
    public void Adapter_without_vlan_info_keeps_the_entries_aligned()
    {
        var snapshot = Snapshot();
        snapshot.NetworkVlans.RemoveAt(0);

        var d = VmSummaryDetailsBuilder.Build(snapshot)[VmSummaryDetailsBuilder.Key("HV01", "vm1")];

        Assert.Equal("-; Trunk", d.VlanModes);
        Assert.Equal("-; 10,30-40 (native 1)", d.VlanLists);
    }
}
