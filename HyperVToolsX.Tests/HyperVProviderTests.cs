using HyperVToolsX.Infrastructure.HyperV;
using HyperVToolsX.Infrastructure.PowerShellEngine;
using Xunit;

namespace HyperVToolsX.Tests;

public class HyperVProviderTests
{
    [Fact]
    public async Task GetLocalHost_ShouldReturnHyperVHost()
    {
        var executor = new PowerShellExecutor();
        var provider = new HyperVProvider(executor);

        var host = await provider.GetHostAsync("localhost");

        Assert.NotNull(host);
        Assert.False(string.IsNullOrWhiteSpace(host.Name));
        Assert.True(host.LogicalProcessorCount > 0);
        Assert.True(host.TotalMemoryBytes > 0);
    }
    [Fact]
    public async Task GetLocalVirtualMachines_ShouldReturnCollection()
    {
        var executor = new PowerShellExecutor();
        var provider = new HyperVProvider(executor);

        var virtualMachines =
            await provider.GetVirtualMachinesAsync("localhost");

        Assert.NotNull(virtualMachines);
    }
}