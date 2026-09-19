using Xunit;
using ClosedXML.Excel;
using HyperVToolsX.Core.Collection;
using HyperVToolsX.Core.Models;
using HyperVToolsX.Export;

namespace HyperVToolsX.Tests;

public class ExcelInventoryExporterTests
{
    [Fact]
    public async Task Export_UsesRequestedSizeUnit()
    {
        var snapshot = new InventorySnapshot
        {
            VirtualMachines = [new HyperVVirtualMachine { Name = "vm01", MemoryAssigned = 2L * 1024 * 1024 * 1024 }]
        };

        var path = Path.Combine(Path.GetTempPath(), $"hvtx-{Guid.NewGuid():N}.xlsx");

        try
        {
            await new ExcelInventoryExporter().ExportAsync(snapshot, path, Core.Enums.SizeUnit.MB);

            using var wb = new XLWorkbook(path);
            var info = wb.Worksheet("vInfo");

            Assert.Contains(info.Row(1).CellsUsed(), c => c.GetString() == "Memory Assigned (MB)");
            Assert.Contains(info.Row(2).CellsUsed(), c => c.DataType == XLDataType.Number && c.GetDouble() == 2048d);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Export_WritesSheetsWithHeadersAndRows()
    {
        var snapshot = new InventorySnapshot
        {
            VirtualMachines =
            [
                new HyperVVirtualMachine
                {
                    Name = "vm01",
                    HostName = "host1",
                    MemoryAssigned = 4L * 1024 * 1024 * 1024,
                    Uptime = TimeSpan.FromHours(30),
                    Groups = ["a", "b"],
                    Notes = "=SUM(1,1)"
                }
            ]
        };

        var path = Path.Combine(Path.GetTempPath(), $"hvtx-{Guid.NewGuid():N}.xlsx");

        try
        {
            var sheets = await new ExcelInventoryExporter().ExportAsync(snapshot, path);

            Assert.True(sheets >= 14);

            using var wb = new XLWorkbook(path);
            var info = wb.Worksheet("vInfo");

            Assert.Equal("Name", info.Cell(1, 1).GetString());
            Assert.Equal("vm01", info.Cell(2, 1).GetString());
            Assert.Contains(info.Row(1).CellsUsed(), c => c.GetString() == "VM Id");
            Assert.Contains(info.Row(2).CellsUsed(), c => c.GetString() == "a; b");
            Assert.Contains(info.Row(2).CellsUsed(), c => c.GetString() == "=SUM(1,1)" && !c.HasFormula);
            Assert.Contains(info.Row(1).CellsUsed(), c => c.GetString() == "Memory Assigned (GB)");
            Assert.Contains(info.Row(2).CellsUsed(), c => c.DataType == XLDataType.Number && c.GetDouble() == 4d);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Export_PutsTemplatesFirstAndAppliesColumns()
    {
        var snapshot = new InventorySnapshot
        {
            VirtualMachines =
            [
                new HyperVVirtualMachine { Name = "vm01", MemoryAssigned = 3L * 1024 * 1024 * 1024, Groups = ["g"] }
            ]
        };

        var template = new Core.Templates.CustomTabTemplate
        {
            Name = "Capacity",
            Source = "vInfo",
            Columns =
            [
                new Core.Templates.TemplateColumn { Field = "MemoryAssigned", Header = "RAM" },
                new Core.Templates.TemplateColumn { Field = "Name", Header = "VM" }
            ]
        };

        var path = Path.Combine(Path.GetTempPath(), $"hvtx-{Guid.NewGuid():N}.xlsx");

        try
        {
            var sheets = await new ExcelInventoryExporter().ExportAsync(snapshot, path, templates: [template]);

            using var wb = new XLWorkbook(path);

            Assert.Equal("Capacity", wb.Worksheet(1).Name);
            Assert.Equal("vInfo", wb.Worksheet(2).Name);
            Assert.Equal(wb.Worksheets.Count, sheets);

            var sheet = wb.Worksheet(1);
            Assert.Equal("RAM (GB)", sheet.Cell(1, 1).GetString());
            Assert.Equal("VM", sheet.Cell(1, 2).GetString());
            Assert.Equal(3d, sheet.Cell(2, 1).GetDouble());
            Assert.Equal("vm01", sheet.Cell(2, 2).GetString());
            Assert.True(sheet.Cell(1, 3).IsEmpty());
        }
        finally
        {
            File.Delete(path);
        }
    }
}
