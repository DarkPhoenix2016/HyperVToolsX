using System.Management.Automation.Language;
using System.Reflection;
using HyperVToolsX.Infrastructure.Collection;
using Xunit;

namespace HyperVToolsX.Tests;

public class CollectionScriptTests
{
    private static string Script() =>
        (string)typeof(RemoteInventoryCollector)
            .GetMethod("BuildCollectionScript", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, null)!;

    [Fact]
    public void Collection_script_parses_without_errors()
    {
        Parser.ParseInput(Script(), out _, out var errors);

        Assert.Empty(errors);
    }

    [Fact]
    public void Every_section_after_the_host_is_isolated_so_one_failure_keeps_the_rest()
    {
        var script = Script();

        var sections = new[]
        {
            "HOST STORAGE", "OPERATING SYSTEM", "VIRTUAL MACHINES", "PROCESSOR", "MEMORY", "NETWORK ADAPTERS", "VLAN",
            "CHECKPOINTS", "INTEGRATION SERVICES", "VM STORAGE", "VM DISKS", "VHD", "REPLICATION", "DVD", "CLUSTER"
        };

        foreach (var section in sections)
        {
            Assert.Contains($"$result.Warnings += ('{section}: '", script);
        }
    }

    [Fact]
    public void Script_cannot_break_out_of_the_remote_here_string()
    {
        Assert.DoesNotContain("\n'@", Script().Replace("\r\n", "\n"));
    }

    /// <summary>Like the provider tests, needs the Hyper-V module on this machine (and elevation).</summary>
    [Fact]
    public async Task Local_collection_runs_end_to_end()
    {
        var executor = new HyperVToolsX.Infrastructure.PowerShellEngine.PowerShellExecutor();
        var runner = new HyperVToolsX.Infrastructure.Remoting.RemoteScriptRunner(executor);
        var collector = new RemoteInventoryCollector(runner);

        var target = new HyperVToolsX.Core.Models.HyperVTarget { Name = "localhost" };

        var result = await collector.CollectAsync(target, new HyperVToolsX.Core.Collection.CollectionRequest());

        Assert.NotEmpty(result.Hosts);
        Assert.All(result.VirtualMachines, vm => Assert.True(vm.StateId > 0, $"{vm.Name}: {vm.State} has no StateId"));
    }
}
