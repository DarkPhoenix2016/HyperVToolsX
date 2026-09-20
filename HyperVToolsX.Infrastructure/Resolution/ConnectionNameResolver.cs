using System.Net;
using HyperVToolsX.Core.Models;

namespace HyperVToolsX.Infrastructure.Resolution;

public sealed record ConnectionName(string Value, bool IsVerifiedHostname);

/// <summary>
/// Turns a bare IP into a verified hostname where possible. WinRM's
/// Default/Negotiate authentication cannot pass through to a bare IP, so a
/// domain-joined target typed as an IP has to become a hostname before
/// Invoke-Command sees it. A PTR record alone is not trusted: the returned
/// name must forward-resolve back to the same IP.
/// </summary>
public class ConnectionNameResolver
{
    public virtual async Task<ConnectionName> ResolveAsync(
        string target,
        CancellationToken cancellationToken = default)
    {
        var trimmed = target?.Trim() ?? string.Empty;

        if (!RemoteConnectionOptions.RequiresExplicitCredentials(trimmed))
        {
            return new ConnectionName(trimmed, IsVerifiedHostname: true);
        }

        var ip = IPAddress.Parse(trimmed);

        try
        {
            var entry = await Dns
                .GetHostEntryAsync(ip)
                .WaitAsync(cancellationToken);

            var hostname = entry.HostName?.Trim().TrimEnd('.');

            if (!string.IsNullOrEmpty(hostname)
                && !IPAddress.TryParse(hostname, out _))
            {
                var forward = await Dns
                    .GetHostAddressesAsync(hostname)
                    .WaitAsync(cancellationToken);

                if (forward.Any(address => address.Equals(ip)))
                {
                    return new ConnectionName(hostname, IsVerifiedHostname: true);
                }
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            // No PTR / forward lookup failure: fall through to the bare IP.
        }

        return new ConnectionName(trimmed, IsVerifiedHostname: false);
    }
}
