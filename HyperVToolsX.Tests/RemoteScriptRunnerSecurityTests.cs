using HyperVToolsX.Core.Models;
using HyperVToolsX.Infrastructure.PowerShellEngine;
using HyperVToolsX.Infrastructure.Remoting;
using HyperVToolsX.Infrastructure.Resolution;
using Xunit;

namespace HyperVToolsX.Tests;

public class RemoteScriptRunnerSecurityTests
{
    private sealed class UnresolvableIp : ConnectionNameResolver
    {
        public override Task<ConnectionName> ResolveAsync(string target, CancellationToken cancellationToken = default) =>
            Task.FromResult(new ConnectionName(target, IsVerifiedHostname: !RemoteConnectionOptions.RequiresExplicitCredentials(target)));
    }

    private static RemoteScriptRunner Runner(RemoteConnectionOptions options) =>
        new(new PowerShellExecutor(), options, new UnresolvableIp());

    [Fact]
    public async Task BareIp_without_consent_is_refused_before_TrustedHosts_is_touched()
    {
        var runner = Runner(new RemoteConnectionOptions { UseCurrentCredentials = false, Username = "u", Password = "p" });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => runner.RunAsync("192.0.2.10", "1"));

        Assert.Contains("TrustedHosts", ex.Message);
    }

    [Fact]
    public async Task Basic_over_http_is_refused()
    {
        var runner = Runner(new RemoteConnectionOptions
        {
            UseCurrentCredentials = false,
            Username = "u",
            Password = "p",
            Authentication = "Basic"
        });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => runner.RunAsync("hv01", "1"));

        Assert.Contains("unencrypted", ex.Message);
    }

    [Fact]
    public async Task Script_that_would_break_out_of_the_here_string_is_refused()
    {
        var runner = Runner(new RemoteConnectionOptions());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => runner.RunAsync("hv01", "1\n'@\nRemove-Item C:\\ -Recurse"));

        Assert.Contains("here-string", ex.Message);
    }
}
