using ClosedXML.Excel;
using HyperVToolsX.Core.Collection;
using HyperVToolsX.Core.Models;
using HyperVToolsX.Core.Models.Details;
using HyperVToolsX.Core.Templates;
using HyperVToolsX.Export;
using HyperVToolsX.Infrastructure.Templates;
using Xunit;

namespace HyperVToolsX.Tests;

public class CustomTabBuilderTests
{
    private static readonly Guid Vm1Id = Guid.NewGuid();

    private static InventorySnapshot Snapshot() => new()
    {
        VirtualMachines =
        [
            new HyperVVirtualMachine { Name = "vm1", HostName = "hostA", VMId = Vm1Id, ClusterName = "cl1" },
            new HyperVVirtualMachine { Name = "vm2", HostName = "hostA", VMId = Guid.NewGuid() }
        ],
        NetworkAdapters =
        [
            new VmNetworkAdapter { VmName = "vm1", HostName = "hostA", Name = "nic0", MacAddress = "AA" },
            new VmNetworkAdapter { VmName = "vm1", HostName = "hostA", Name = "nic1", MacAddress = "BB" },
            // Same VM name on a different host must not leak into hostA's vm1.
            new VmNetworkAdapter { VmName = "vm1", HostName = "hostB", Name = "other", MacAddress = "ZZ" }
        ],
        Memories =
        [
            new VmMemoryInfo { VmName = "vm1", HostName = "hostA", Startup = 4L * 1024 * 1024 * 1024 }
        ],
        Disks = [new VmDiskInfo { VmName = "vm1", HostName = "hostA", Path = @"C:\a.vhdx" }],
        Vhds =
        [
            new VhdInfo { HostName = "hostA", Path = @"C:\a.vhdx", FileSize = 2L * 1024 * 1024 * 1024 },
            new VhdInfo { HostName = "hostA", Path = @"C:\unrelated.vhdx", FileSize = 1 }
        ],
        Hosts = [new HyperVHost { Name = "hostA", HyperVVersion = "10.0" }]
    };

    private static CustomTabTemplate Template(params (string Source, string Field)[] columns) => new()
    {
        Name = "Combined",
        Source = "vInfo",
        Columns = columns.Select(c => new TemplateColumn { Source = c.Source, Field = c.Field, Header = c.Field }).ToList()
    };

    [Fact]
    public void Build_ProducesOneRowPerVm_AndCollapsesMultipleMatches()
    {
        var data = CustomTabBuilder.Build(
            Snapshot(),
            Template(("vInfo", "Name"), ("vNetwork", "MacAddress"), ("vMemory", "Startup"), ("vHost", "HyperVVersion")));

        Assert.Equal(2, data.Rows.Count);

        var vm1 = data.Rows[0].Values;
        Assert.Equal("vm1", vm1[0]);

        var macs = Assert.IsType<CollapsedValue>(vm1[1]);
        Assert.Equal(["AA", "BB"], macs.Items);
        Assert.Equal("AA; BB", CustomTabBuilder.JoinText(vm1[1], x => x?.ToString() ?? ""));

        Assert.Equal(4L * 1024 * 1024 * 1024, vm1[2]);
        Assert.Equal("10.0", vm1[3]);

        var vm2 = data.Rows[1].Values;
        Assert.Equal("vm2", vm2[0]);
        Assert.Null(vm2[1]);
        Assert.Null(vm2[2]);
    }

    [Fact]
    public void Build_JoinsVhdThroughVmDisks()
    {
        var data = CustomTabBuilder.Build(Snapshot(), Template(("vInfo", "Name"), ("vVHD", "FileSize")));

        Assert.Equal(2L * 1024 * 1024 * 1024, data.Rows[0].Values[1]);
        Assert.Null(data.Rows[1].Values[1]);
    }

    [Fact]
    public void Build_SortKeysAreSingleComparableValues()
    {
        var data = CustomTabBuilder.Build(Snapshot(), Template(("vNetwork", "MacAddress"), ("vNetwork", "IPv4Addresses")));

        Assert.Equal("AA", data.Rows[0].SortKeys[0]);
        Assert.IsType<string>(data.Rows[0].SortKeys[1]);
    }

    [Fact]
    public void ResolveColumns_DropsSourcesNotAllowedForRowSource()
    {
        var template = new CustomTabTemplate
        {
            Name = "H",
            Source = "vHost",
            Columns =
            [
                new TemplateColumn { Source = "vHost", Field = "HostName" },
                new TemplateColumn { Source = "vNetwork", Field = "MacAddress" }
            ]
        };

        var column = Assert.Single(CustomTabBuilder.ResolveColumns(template));
        Assert.Equal("HostName", column.Field.Name);
    }

    [Fact]
    public void Store_MigratesLegacySingleSourceFileToVmRows()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"hvtx-tpl-{Guid.NewGuid():N}");
        Directory.CreateDirectory(folder);

        try
        {
            File.WriteAllText(Path.Combine(folder, "Nics.xml"),
                "<CustomTab Name=\"Nics\" Source=\"vNetwork\"><DataGrid><Column Field=\"MacAddress\" Header=\"MAC\"/></DataGrid></CustomTab>");

            var loaded = Assert.Single(new TemplateStore(folder).LoadAll());

            Assert.Equal("vInfo", loaded.Source);
            Assert.Equal("vNetwork", loaded.Columns[0].Source);
            Assert.Equal("MAC", loaded.Columns[0].Header);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public async Task Export_WritesCombinedColumnsWithCollapsedAndConvertedValues()
    {
        var path = Path.Combine(Path.GetTempPath(), $"hvtx-{Guid.NewGuid():N}.xlsx");

        try
        {
            await new ExcelInventoryExporter().ExportAsync(
                Snapshot(),
                path,
                templates: [Template(("vInfo", "Name"), ("vNetwork", "MacAddress"), ("vMemory", "Startup"))]);

            using var wb = new XLWorkbook(path);
            var sheet = wb.Worksheet(1);

            Assert.Equal("Combined", sheet.Name);
            Assert.Equal("Startup (GB)", sheet.Cell(1, 3).GetString());
            Assert.Equal("AA; BB", sheet.Cell(2, 2).GetString());
            Assert.Equal(4d, sheet.Cell(2, 3).GetDouble());
            Assert.True(sheet.Cell(3, 2).IsEmpty());
        }
        finally
        {
            File.Delete(path);
        }
    }
}
