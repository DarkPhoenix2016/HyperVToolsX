using System.Net;
using HyperVToolsX.Core.Logging;
using HyperVToolsX.Infrastructure.PowerShellEngine;

namespace HyperVToolsX.Infrastructure.Remoting;

/// <summary>
/// Adds an IP to WSMan:\localhost\Client\TrustedHosts (the workgroup-target
/// case, where no hostname could be resolved). Only ever adds; existing
/// entries, including "*", are left untouched. Requires elevation, which the
/// app manifest guarantees.
/// </summary>
public class TrustedHostsManager
{
    private readonly PowerShellExecutor _powerShell;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly HashSet<string> _confirmed =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly ILiveLog _log;

    public TrustedHostsManager(PowerShellExecutor powerShell, ILiveLog? log = null)
    {
        _powerShell = powerShell ?? throw new ArgumentNullException(nameof(powerShell));
        _log = log ?? NullLiveLog.Instance;
    }

    public async Task EnsureTrustedAsync(
        string ipAddress,
        CancellationToken cancellationToken = default)
    {
        // Only a parsed IP is ever embedded in the script below.
        if (!IPAddress.TryParse(ipAddress?.Trim(), out var ip))
        {
            throw new ArgumentException(
                $"'{ipAddress}' is not a valid IP address.",
                nameof(ipAddress));
        }

        var entry = ip.ToString();

        await _gate.WaitAsync(cancellationToken);

        try
        {
            if (_confirmed.Contains(entry))
            {
                return;
            }

            _log.Step("TrustedHosts", $"Checking WinRM TrustedHosts for {entry}", entry);

            var script = $$"""
                $ErrorActionPreference = 'Stop'
                $path = 'WSMan:\localhost\Client\TrustedHosts'
                $current = [string](Get-Item $path).Value
                $items = @($current -split ',' | ForEach-Object { $_.Trim() } | Where-Object { $_ })
                if ($items -contains '*' -or $items -contains '{{entry}}') {
                    'PRESENT'
                }
                else {
                    Set-Item -Path $path -Value ((@($items) + '{{entry}}') -join ',') -Force
                    'ADDED'
                }
                """;

            try
            {
                var outcome = await _powerShell.ExecuteWindowsPowerShellAsync(
                    script,
                    cancellationToken,
                    logTarget: entry);

                _log.Info(
                    "TrustedHosts",
                    outcome.Contains("ADDED", StringComparison.Ordinal)
                        ? $"Added {entry} to WSMan TrustedHosts"
                        : $"{entry} already trusted (no change)",
                    entry);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Could not add {entry} to WinRM TrustedHosts: {ex.Message} " +
                    "(the app must run elevated and the WinRM service must be running).",
                    ex);
            }

            _confirmed.Add(entry);
        }
        finally
        {
            _gate.Release();
        }
    }
}
