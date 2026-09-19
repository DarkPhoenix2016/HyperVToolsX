using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.Json;
using HyperVToolsX.Core.Enums;
using HyperVToolsX.Core.Interfaces;
using HyperVToolsX.Core.Logging;
using HyperVToolsX.Core.Models;
using HyperVToolsX.Infrastructure.PowerShellEngine;
using HyperVToolsX.Infrastructure.Remoting;

namespace HyperVToolsX.Infrastructure.Validation;

public class TargetValidator : ITargetValidator
{
    private readonly RemoteScriptRunner _runner;
    private readonly ILiveLog _log;

    public TargetValidator(PowerShellExecutor powerShell)
        : this(new RemoteScriptRunner(powerShell))
    {
    }

    public TargetValidator(RemoteScriptRunner runner, ILiveLog? log = null)
    {
        _runner =
            runner
            ?? throw new ArgumentNullException(
                nameof(runner));

        _log = log ?? NullLiveLog.Instance;
    }

    public async Task<TargetValidationResult> ValidateAsync(
        HyperVTarget target,
        CancellationToken cancellationToken = default)
    {
        if (target == null)
        {
            throw new ArgumentNullException(nameof(target));
        }

        var targetName =
            target.Name?.Trim() ?? string.Empty;

        var result =
            new TargetValidationResult
            {
                TargetName = targetName,
                StartedAt = DateTime.Now,
                Status =
                    TargetValidationStatus.Pending,
                IsCluster = false,
                ClusterName = null,
                ErrorMessage = null
            };

        if (string.IsNullOrWhiteSpace(targetName))
        {
            result.Status =
                TargetValidationStatus.NameResolutionFailed;

            result.ErrorMessage =
                "Target name or address is required.";

            return Complete(result);
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            _log.Step("Validation", "Started", targetName);

            // =========================================================
            // 1. DNS / NAME RESOLUTION
            // =========================================================

            result.Status =
                TargetValidationStatus.ResolvingName;

            IPAddress[] addresses;

            try
            {
                addresses =
                    await Dns.GetHostAddressesAsync(
                        targetName,
                        cancellationToken);
            }
            catch (SocketException ex)
            {
                result.Status =
                    TargetValidationStatus.NameResolutionFailed;

                result.ErrorMessage =
                    $"Unable to resolve '{targetName}'. " +
                    $"DNS error: {ex.Message}";

                return Complete(result);
            }

            cancellationToken.ThrowIfCancellationRequested();

            if (addresses.Length == 0)
            {
                result.Status =
                    TargetValidationStatus.NameResolutionFailed;

                result.ErrorMessage =
                    $"Unable to resolve '{targetName}'.";

                return Complete(result);
            }

            result.NameResolved = true;

            _log.Info("Validation", $"DNS resolved to {string.Join(", ", addresses.Select(a => a.ToString()))}", targetName);

            // Prefer IPv4.
            var resolvedAddress =
                addresses.FirstOrDefault(
                    address =>
                        address.AddressFamily ==
                        AddressFamily.InterNetwork);

            resolvedAddress ??=
                addresses.FirstOrDefault();

            result.ResolvedAddress =
                resolvedAddress?.ToString();

            // =========================================================
            // 2. PING
            // =========================================================

            cancellationToken.ThrowIfCancellationRequested();

            result.Status =
                TargetValidationStatus.Pinging;

            using var ping = new Ping();

            PingReply pingReply;

            try
            {
                pingReply =
                    await ping.SendPingAsync(
                        targetName,
                        3000);
            }
            catch (PingException ex)
            {
                result.Status =
                    TargetValidationStatus.PingFailed;

                result.ErrorMessage =
                    $"Ping failed for '{targetName}'. " +
                    $"Error: {ex.Message}";

                return Complete(result);
            }

            cancellationToken.ThrowIfCancellationRequested();

            if (pingReply.Status != IPStatus.Success)
            {
                result.Status =
                    TargetValidationStatus.PingFailed;

                result.ErrorMessage =
                    $"Ping failed for '{targetName}'. " +
                    $"Status: {pingReply.Status}";

                return Complete(result);
            }

            result.PingSucceeded = true;

            _log.Info("Validation", $"Ping OK ({pingReply.RoundtripTime} ms)", targetName);

            // =========================================================
            // 3. HYPER-V CONNECTIVITY
            //
            // IMPORTANT:
            // Do NOT use HyperVProvider here.
            //
            // The Hyper-V cmdlets must execute inside Windows
            // PowerShell 5.1 on the local machine and then remotely
            // on the target.
            // =========================================================

            cancellationToken.ThrowIfCancellationRequested();

            result.Status =
                TargetValidationStatus.Connecting;

            RemoteValidationResult? remoteResult;

            _log.Step("Validation", "Checking Hyper-V role and failover cluster membership over WinRM", targetName);

            try
            {
                remoteResult =
                    await ValidateRemoteHyperVAsync(
                        targetName,
                        cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                result.Status =
                    TargetValidationStatus.ConnectionFailed;

                result.ErrorMessage =
                    $"Unable to connect to Hyper-V host " +
                    $"'{targetName}'. Error: {ex.Message}";

                return Complete(result);
            }

            cancellationToken.ThrowIfCancellationRequested();

            if (remoteResult == null)
            {
                result.Status =
                    TargetValidationStatus.ConnectionFailed;

                result.ErrorMessage =
                    $"Hyper-V validation returned no data " +
                    $"from '{targetName}'.";

                return Complete(result);
            }

            if (!remoteResult.Success)
            {
                result.Status =
                    TargetValidationStatus.ConnectionFailed;

                result.ErrorMessage =
                    string.IsNullOrWhiteSpace(
                        remoteResult.ErrorMessage)
                        ? $"Unable to connect to Hyper-V host '{targetName}'."
                        : remoteResult.ErrorMessage;

                return Complete(result);
            }

            result.HyperVConnectionSucceeded = true;

            // =========================================================
            // 4. TARGET IDENTIFICATION
            // =========================================================

            cancellationToken.ThrowIfCancellationRequested();

            _log.Info(
                "Validation",
                $"Hyper-V reachable. Host={remoteResult.HostName}, IsCluster={remoteResult.IsCluster}, Cluster='{remoteResult.ClusterName}'",
                targetName);

            if (remoteResult.IsCluster)
            {
                result.IsCluster = true;

                result.ClusterName =
                    remoteResult.ClusterName?.Trim();

                result.Status =
                    TargetValidationStatus.ClusterReady;

                result.ErrorMessage = null;

                // A cluster name (CNO) vs. one of its nodes: only the cluster
                // itself matches the cluster's own name.
                target.Type =
                    SameShortName(targetName, result.ClusterName)
                        ? TargetType.Cluster
                        : TargetType.ClusteredHost;

                target.Address =
                    result.ResolvedAddress
                    ?? target.Address;

                return Complete(result);
            }

            // =========================================================
            // 5. STANDALONE HOST
            // =========================================================

            result.IsCluster = false;
            result.ClusterName = null;

            result.Status =
                TargetValidationStatus.Ready;

            result.ErrorMessage = null;

            target.Type =
                TargetType.StandaloneHost;

            target.Address =
                result.ResolvedAddress
                ?? target.Address;

            return Complete(result);
        }
        catch (OperationCanceledException)
        {
            result.Status =
                TargetValidationStatus.Failed;

            result.ErrorMessage =
                "Validation cancelled.";

            return Complete(result);
        }
        catch (Exception ex)
        {
            result.Status =
                DetermineFailureStage(result);

            result.ErrorMessage =
                ex.Message;

            return Complete(result);
        }
    }

    private static bool SameShortName(string? a, string? b)
    {
        static string Short(string? value) =>
            (value ?? string.Empty).Trim().Split('.')[0];

        var left = Short(a);

        return left.Length > 0
            && left.Equals(Short(b), StringComparison.OrdinalIgnoreCase);
    }

    private async Task<RemoteValidationResult?>
        ValidateRemoteHyperVAsync(
            string computerName,
            CancellationToken cancellationToken)
    {
        var remoteScript = """
$ErrorActionPreference = 'Stop'

$result = [ordered]@{
    Success       = $false
    ComputerName  = $env:COMPUTERNAME
    HostName      = ''
    IsCluster     = $false
    ClusterName   = ''
    ErrorMessage  = ''
}

try {
    Import-Module Hyper-V -ErrorAction Stop

    $vmHost = Get-VMHost -ErrorAction Stop

    if ($null -eq $vmHost) {
        throw 'Get-VMHost returned no Hyper-V host.'
    }

    $result.HostName =
        if ($null -ne $vmHost.ComputerName) {
            [string]$vmHost.ComputerName
        }
        else {
            [string]$env:COMPUTERNAME
        }

    try {
        Import-Module FailoverClusters -ErrorAction Stop

        $cluster =
            Get-Cluster -ErrorAction Stop

        if ($null -ne $cluster) {
            $result.IsCluster = $true

            $result.ClusterName =
                if ($null -ne $cluster.Name) {
                    [string]$cluster.Name
                }
                else {
                    ''
                }
        }
    }
    catch {
        # No Failover Cluster is normal for a standalone Hyper-V host.
        $result.IsCluster = $false
        $result.ClusterName = ''
    }

    $result.Success = $true
}
catch {
    $result.Success = $false
    $result.ErrorMessage = $_.Exception.Message
}

$result |
    ConvertTo-Json -Compress -Depth 5
""";

        var json =
            await _runner.RunAsync(
                computerName,
                remoteScript,
                cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(json))
        {
            throw new InvalidOperationException(
                $"Remote validation returned no data " +
                $"from '{computerName}'.");
        }

        // Invoke-Command can return more than one serialized
        // output object. Find the JSON object that contains
        // the validation result.
        var jsonLine =
            json
                .Split(
                    new[]
                    {
                        Environment.NewLine,
                        "\r",
                        "\n"
                    },
                    StringSplitOptions.RemoveEmptyEntries)
                .LastOrDefault(
                    line =>
                        line.TrimStart()
                            .StartsWith(
                                "{",
                                StringComparison.Ordinal));

        if (string.IsNullOrWhiteSpace(jsonLine))
        {
            throw new InvalidOperationException(
                $"Unable to parse Hyper-V validation response " +
                $"from '{computerName}'. " +
                $"PowerShell output: {json}");
        }

        try
        {
            return JsonSerializer.Deserialize<RemoteValidationResult>(
                jsonLine,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                $"Invalid Hyper-V validation response " +
                $"from '{computerName}': {ex.Message}",
                ex);
        }
    }

    private static TargetValidationStatus
        DetermineFailureStage(
            TargetValidationResult result)
    {
        if (!result.NameResolved)
        {
            return TargetValidationStatus.NameResolutionFailed;
        }

        if (!result.PingSucceeded)
        {
            return TargetValidationStatus.PingFailed;
        }

        if (!result.HyperVConnectionSucceeded)
        {
            return TargetValidationStatus.ConnectionFailed;
        }

        return TargetValidationStatus.Failed;
    }

    private TargetValidationResult Complete(
        TargetValidationResult result)
    {
        result.CompletedAt = DateTime.Now;

        var summary =
            $"Finished: {result.Status}" +
            (string.IsNullOrWhiteSpace(result.ErrorMessage)
                ? string.Empty
                : $" - {result.ErrorMessage}");

        if (result.Status is TargetValidationStatus.Ready or TargetValidationStatus.ClusterReady)
        {
            _log.Info("Validation", summary, result.TargetName);
        }
        else
        {
            _log.Error("Validation", summary, result.TargetName);
        }

        return result;
    }

    private sealed class RemoteValidationResult
    {
        public bool Success { get; set; }

        public string ComputerName { get; set; } =
            string.Empty;

        public string HostName { get; set; } =
            string.Empty;

        public bool IsCluster { get; set; }

        public string ClusterName { get; set; } =
            string.Empty;

        public string ErrorMessage { get; set; } =
            string.Empty;
    }
}