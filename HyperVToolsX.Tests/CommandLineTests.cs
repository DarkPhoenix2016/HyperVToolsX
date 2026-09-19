using HyperVToolsX.Core.Cli;
using HyperVToolsX.Core.Collection;
using HyperVToolsX.Core.Enums;
using HyperVToolsX.Core.Models;
using HyperVToolsX.Core.Models.Details;
using HyperVToolsX.Core.Templates;
using HyperVToolsX.Export;
using Xunit;

namespace HyperVToolsX.Tests;

public class CommandLineTests
{
    [Fact]
    public void Parse_ReadsAllSwitches()
    {
        var result = CommandLineParser.Parse(
        [
            "/host:HV01,HV02", "/HOST:hv01", @"/export:C:\Reports", "/type:xlsx",
            @"/user:corp\admin", "/password:p:w=d", "/auth:kerberos", "/ssl", "/port:5986",
            "/skipca", "/skipcn", "/timeout:45", "/silent", "/unit:tb"
        ]);

        Assert.True(result.IsValid);
        var o = result.Options!;

        Assert.Equal(["HV01", "HV02"], o.Hosts);
        Assert.Equal(@"C:\Reports", o.ExportPath);
        Assert.True(o.ExportIsFolder);
        Assert.False(o.ExportsCsv);
        Assert.Equal(@"corp\admin", o.Username);
        Assert.Equal("p:w=d", o.Password);
        Assert.Equal("Kerberos", o.Authentication);
        Assert.True(o.UseSsl);
        Assert.Equal(5986, o.Port);
        Assert.True(o.SkipCaCertificateCheck);
        Assert.True(o.SkipCnCheck);
        Assert.Equal(45, o.TimeoutSeconds);
        Assert.True(o.Silent);
        Assert.Equal(SizeUnit.TB, o.SizeUnit);

        var connection = o.ToConnectionOptions();
        Assert.False(connection.UseCurrentCredentials);
        Assert.Equal("Kerberos", connection.Authentication);
    }

    [Fact]
    public void Parse_Defaults_UseCurrentCredentialsAndGb()
    {
        var o = CommandLineParser.Parse(["/host:HV01", "/export:report.csv"]).Options!;

        Assert.True(o.ExportsCsv);
        Assert.Equal(30, o.TimeoutSeconds);
        Assert.Equal(0, o.Port);
        Assert.Equal(SizeUnit.GB, o.SizeUnit);
        Assert.Equal("Default", o.Authentication);
        Assert.True(o.ToConnectionOptions().UseCurrentCredentials);
    }

    [Theory]
    [InlineData("/?")]
    [InlineData("-?")]
    [InlineData("/help")]
    public void Parse_HelpWinsOverMissingParameters(string arg)
    {
        var result = CommandLineParser.Parse([arg]);

        Assert.True(result.ShowHelp);
        Assert.Empty(result.Errors);
    }

    [Theory]
    [InlineData("/export:a.xlsx")]                                   // no host
    [InlineData("/host:HV01")]                                       // no export
    [InlineData("/host:HV01", "/export:a.txt")]                      // bad extension
    [InlineData("/host:HV01", "/export:a.xlsx", "/user:u")]          // user without password
    [InlineData("/host:HV01", "/export:a.xlsx", "/password:p")]      // password without user
    [InlineData("/host:HV01", "/export:a.xlsx", "/auth:magic")]
    [InlineData("/host:HV01", "/export:a.xlsx", "/port:70000")]
    [InlineData("/host:HV01", "/export:a.xlsx", "/timeout:0")]
    [InlineData("/host:HV01", "/export:a.xlsx", "/unit:9")]
    [InlineData("/host:HV01", "/export:a.xlsx", "/bogus")]
    [InlineData("/host:HV01", "/export:a.xlsx", "stray")]
    public void Parse_RejectsInvalidArguments(params string[] args)
    {
        var result = CommandLineParser.Parse(args);

        Assert.False(result.IsValid);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public void Parse_AcceptsDashPrefixesAndEqualsSeparator()
    {
        var o = CommandLineParser.Parse(["--host=HV01", "-export:r.xlsx", "-ssl"]).Options!;

        Assert.Equal(["HV01"], o.Hosts);
        Assert.True(o.UseSsl);
    }

    // ---------------------------------------------------------
    // Export folder + type, host file

    [Fact]
    public void Parse_ExportFolder_RequiresType()
    {
        var missing = CommandLineParser.Parse(["/host:HV01", @"/export:C:\Reports"]);
        Assert.False(missing.IsValid);
        Assert.Contains(missing.Errors, e => e.Contains("/type"));

        var bad = CommandLineParser.Parse(["/host:HV01", @"/export:C:\Reports", "/type:pdf"]);
        Assert.False(bad.IsValid);
    }

    [Theory]
    [InlineData("xlsx", ExportFormat.Xlsx, ".xlsx")]
    [InlineData("CSV", ExportFormat.Csv, ".csv")]
    public void ExportFile_IsHostnamePlusTimestampInFolder(string type, ExportFormat format, string extension)
    {
        var o = CommandLineParser.Parse(["/host:HV01", @"/export:C:\Reports", $"/type:{type}"]).Options!;

        Assert.True(o.ExportIsFolder);
        Assert.Equal(format, o.Format);
        Assert.Equal(
            Path.Combine(@"C:\Reports", $"HV01_20260920-031500{extension}"),
            o.ResolveExportFile("HV01", new DateTime(2026, 9, 20, 3, 15, 0)));
    }

    [Fact]
    public void ExportFile_EachHostGetsItsOwnFile_AndNamesAreSanitized()
    {
        var o = CommandLineParser.Parse(["/host:hv/01,HV02,HV03", @"/export:out", "/type:xlsx"]).Options!;
        var when = new DateTime(2026, 9, 20, 3, 15, 0);

        var files = o.Hosts.Select(h => o.ResolveExportFile(h, when)).ToList();

        Assert.Equal(
            [
                Path.Combine("out", "hv_01_20260920-031500.xlsx"),
                Path.Combine("out", "HV02_20260920-031500.xlsx"),
                Path.Combine("out", "HV03_20260920-031500.xlsx")
            ],
            files);
    }

    [Fact]
    public void Parse_ExplicitFilePath_StillWorks_AndTypeMustAgree()
    {
        var ok = CommandLineParser.Parse(["/host:HV01", @"/export:D:\out.csv"]).Options!;

        Assert.False(ok.ExportIsFolder);
        Assert.Equal(ExportFormat.Csv, ok.Format);
        Assert.Equal(@"D:\out.csv", ok.ResolveExportFile("HV01", DateTime.Now));

        Assert.False(CommandLineParser.Parse(["/host:HV01", @"/export:D:\out.csv", "/type:xlsx"]).IsValid);
    }

    [Fact]
    public void Parse_ExplicitFilePath_WithSeveralHosts_IsRejected()
    {
        var result = CommandLineParser.Parse(["/host:HV01,HV02", @"/export:D:\out.xlsx"]);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("own file"));

        Assert.True(CommandLineParser.Parse(["/host:HV01,HV02", @"/export:D:\Out", "/type:xlsx"]).IsValid);
    }

    [Fact]
    public void ClusterNodeFiles_GoInAFolderNamedAfterTheCluster()
    {
        var o = CommandLineParser.Parse(["/host:CL1", @"/export:C:\Reports", "/type:xlsx"]).Options!;
        var when = new DateTime(2026, 9, 20, 3, 15, 0);

        Assert.Equal(
            Path.Combine(@"C:\Reports", "CL1", "NODE1_20260920-031500.xlsx"),
            o.ResolveClusterNodeFile("CL1", "NODE1", when));

        // Names that can't be file names are sanitized, and CSV keeps its extension.
        var csv = CommandLineParser.Parse(["/host:CL1", @"/export:out", "/type:csv"]).Options!;
        Assert.Equal(
            Path.Combine("out", "cl_1", "n_1_20260920-031500.csv"),
            csv.ResolveClusterNodeFile("cl/1", "n:1", when));
    }

    [Fact]
    public void Parse_HostFile_IsCombinedWithHostAndDeduplicated()
    {
        var file = Path.Combine(Path.GetTempPath(), $"hvtx-hosts-{Guid.NewGuid():N}.txt");
        File.WriteAllLines(file, ["# my hosts", "HV01", "", "  HV02  ", "HV03, HV04;HV05", "hv01"]);

        try
        {
            var o = CommandLineParser.Parse(["/host:HV09", $"/hostfile:{file}", "/export:out", "/type:csv"]).Options!;

            Assert.Equal(["HV09", "HV01", "HV02", "HV03", "HV04", "HV05"], o.Hosts);

            var fileOnly = CommandLineParser.Parse([$"/hostfile:{file}", "/export:out", "/type:csv"]);
            Assert.True(fileOnly.IsValid);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public void Parse_HostFile_MissingOrEmpty_IsAnError()
    {
        var missing = CommandLineParser.Parse([@"/hostfile:C:\definitely\not\here.txt", "/export:out", "/type:csv"]);
        Assert.False(missing.IsValid);
        Assert.Contains(missing.Errors, e => e.Contains("Could not read the host file"));

        var empty = Path.Combine(Path.GetTempPath(), $"hvtx-hosts-{Guid.NewGuid():N}.txt");
        File.WriteAllLines(empty, ["# nothing here", ""]);

        try
        {
            var result = CommandLineParser.Parse([$"/hostfile:{empty}", "/export:out", "/type:csv"]);
            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, e => e.Contains("no host names"));
        }
        finally
        {
            File.Delete(empty);
        }
    }

    // ---------------------------------------------------------

    [Fact]
    public async Task CsvExporter_WritesOneFilePerNonEmptyTable_WithQuotingAndUnits()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"hvtx-csv-{Guid.NewGuid():N}");
        var target = Path.Combine(folder, "report.csv");

        var snapshot = new InventorySnapshot
        {
            VirtualMachines =
            [
                new HyperVVirtualMachine
                {
                    Name = "=cmd",
                    Notes = "has, comma and \"quote\"",
                    HostName = "h1",
                    MemoryAssigned = 2L * 1024 * 1024 * 1024
                }
            ],
            NetworkAdapters =
            [
                new VmNetworkAdapter { VmName = "=cmd", HostName = "h1", MacAddress = "AA" },
                new VmNetworkAdapter { VmName = "=cmd", HostName = "h1", MacAddress = "BB" }
            ]
        };

        var template = new CustomTabTemplate
        {
            Name = "My Tab",
            Source = "vInfo",
            Columns =
            [
                new TemplateColumn { Source = "vInfo", Field = "MemoryAssigned", Header = "RAM" },
                new TemplateColumn { Source = "vNetwork", Field = "MacAddress", Header = "MACs" }
            ]
        };

        try
        {
            var count = await new CsvInventoryExporter().ExportAsync(snapshot, target, SizeUnit.MB, [template]);

            var files = Directory.GetFiles(folder).Select(Path.GetFileName).Order().ToList();

            Assert.Equal(["report-My Tab.csv", "report-vInfo.csv", "report-vNetwork.csv"], files);
            Assert.Equal(3, count);

            var custom = File.ReadAllLines(Path.Combine(folder, "report-My Tab.csv"));
            Assert.Equal("RAM (MB),MACs", custom[0]);
            Assert.Equal(
                new byte[] { 0xEF, 0xBB, 0xBF },
                File.ReadAllBytes(Path.Combine(folder, "report-My Tab.csv")).Take(3));
            Assert.Equal("2048,AA; BB", custom[1]);

            var info = File.ReadAllText(Path.Combine(folder, "report-vInfo.csv"));
            Assert.Contains("'=cmd", info);
            Assert.Contains("\"has, comma and \"\"quote\"\"\"", info);
        }
        finally
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }
    }

    [Fact]
    public void Humanize_KeepsIPv4Together()
    {
        Assert.Equal("IPv4 Addresses", InventoryCatalog.Humanize("IPv4Addresses"));
        Assert.Equal("VM Id", InventoryCatalog.Humanize("VMId"));
    }
}
