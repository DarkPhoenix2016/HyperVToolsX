using HyperVToolsX.Core.Templates;
using HyperVToolsX.Infrastructure.Templates;
using Xunit;

namespace HyperVToolsX.Tests;

public class TemplateStoreTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"hvtx-tpl-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    private static CustomTabTemplate Sample(string name = "Capacity") => new()
    {
        Name = name,
        Source = "vInfo",
        Columns =
        [
            new TemplateColumn { Field = "Name", Header = "VM" },
            new TemplateColumn { Field = "MemoryAssigned", Header = "Memory" }
        ]
    };

    [Fact]
    public void LoadAll_CreatesMissingFolder()
    {
        var store = new TemplateStore(_folder);

        Assert.Empty(store.LoadAll());
        Assert.True(Directory.Exists(_folder));
    }

    [Fact]
    public void SaveThenLoad_RoundTripsColumnsInOrder()
    {
        var store = new TemplateStore(_folder);
        store.Save(Sample());

        var loaded = Assert.Single(new TemplateStore(_folder).LoadAll());

        Assert.Equal("Capacity", loaded.Name);
        Assert.Equal("vInfo", loaded.Source);
        Assert.Equal(["Name", "MemoryAssigned"], loaded.Columns.Select(c => c.Field));
        Assert.Equal(["VM", "Memory"], loaded.Columns.Select(c => c.Header));
        Assert.True(File.Exists(Path.Combine(_folder, "Capacity.xml")));
    }

    [Fact]
    public void Save_WithNewName_RemovesOldFile()
    {
        var store = new TemplateStore(_folder);
        store.Save(Sample("Old"));
        store.Save(Sample("New"), previousName: "Old");

        Assert.False(File.Exists(Path.Combine(_folder, "Old.xml")));
        Assert.True(File.Exists(Path.Combine(_folder, "New.xml")));
    }

    [Fact]
    public void LoadAll_SkipsInvalidFilesAndUnknownFields()
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(Path.Combine(_folder, "broken.xml"), "<CustomTab");
        File.WriteAllText(Path.Combine(_folder, "badsource.xml"),
            "<CustomTab Name=\"X\" Source=\"nope\"><DataGrid><Column Field=\"Name\"/></DataGrid></CustomTab>");
        File.WriteAllText(Path.Combine(_folder, "partial.xml"),
            "<CustomTab Name=\"Partial\" Source=\"vInfo\"><DataGrid><Column Field=\"Name\"/><Column Field=\"Gone\"/></DataGrid></CustomTab>");

        var loaded = Assert.Single(new TemplateStore(_folder).LoadAll());

        Assert.Equal("Partial", loaded.Name);
        Assert.Equal(["Name"], loaded.Columns.Select(c => c.Field));
        Assert.Equal("Name", loaded.Columns[0].Header);
    }

    [Theory]
    [InlineData("")]
    [InlineData("a/b")]
    [InlineData("vInfo")]
    [InlineData("History")]
    [InlineData("this name is much longer than thirty-one characters")]
    public void NameValidator_RejectsBadNames(string name)
    {
        Assert.NotNull(TemplateNameValidator.Validate(name, []));
    }

    [Fact]
    public void NameValidator_RejectsDuplicatesCaseInsensitively()
    {
        Assert.NotNull(TemplateNameValidator.Validate("capacity", ["Capacity"]));
        Assert.Null(TemplateNameValidator.Validate("Capacity 2", ["Capacity"]));
    }
}
