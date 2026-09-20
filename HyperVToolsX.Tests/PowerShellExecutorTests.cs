using Xunit;
using HyperVToolsX.Infrastructure.PowerShellEngine;

namespace HyperVToolsX.Tests;

public class PowerShellExecutorTests
{
    [Fact]
    public async Task Script_is_delivered_over_stdin_as_utf8_and_leaves_no_temp_file()
    {
        var before = TempScriptCount();

        var output = await new PowerShellExecutor().ExecuteWindowsPowerShellAsync(
            "[Console]::OutputEncoding = [System.Text.Encoding]::UTF8\nWrite-Output ('caf' + [char]0xE9 + ' ' + $env:HVTX_TEST)",
            environmentVariables: new Dictionary<string, string> { ["HVTX_TEST"] = "ok" });

        Assert.Equal("café ok", output);
        Assert.Equal(before, TempScriptCount());
    }

    [Theory]
    [InlineData("s3crét 'quoted' \"x\"")]
    [InlineData("")]
    public async Task Secret_arrives_as_SecureString_without_env_or_script_text(string secret)
    {
        // Prints the plaintext back so the round trip can be asserted; real scripts only build a PSCredential.
        var output = await new PowerShellExecutor().ExecuteWindowsPowerShellAsync(
            "[Console]::OutputEncoding = [System.Text.Encoding]::UTF8\n" +
            "(New-Object System.Management.Automation.PSCredential('u', $hvtxSecret)).GetNetworkCredential().Password + '|' + ($env:HVTX_REMOTE_PWD -eq $null)",
            secret: secret);

        Assert.Equal(secret + "|True", output);
    }

    [Fact]
    public async Task Terminating_error_surfaces_as_exception()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new PowerShellExecutor().ExecuteWindowsPowerShellAsync("throw 'boom'"));
    }

    private static int TempScriptCount()
    {
        var dir = Path.Combine(Path.GetTempPath(), "HyperVToolsX", "PowerShell");
        return Directory.Exists(dir) ? Directory.GetFiles(dir).Length : 0;
    }
}
