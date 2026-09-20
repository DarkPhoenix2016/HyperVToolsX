using HyperVToolsX.Core.Logging;
using HyperVToolsX.Core.Models;
using HyperVToolsX.Infrastructure.PowerShellEngine;
using HyperVToolsX.Infrastructure.Resolution;

namespace HyperVToolsX.Infrastructure.Remoting;

/// <summary>
/// Runs a script on a target through Windows PowerShell 5.1, either locally or
/// via Invoke-Command, applying the shared <see cref="RemoteConnectionOptions"/>.
/// Used by both target validation and inventory collection so they connect
/// identically.
/// </summary>
public class RemoteScriptRunner
{
    private readonly PowerShellExecutor _powerShell;
    private readonly ConnectionNameResolver _resolver;
    private readonly TrustedHostsManager _trustedHosts;
    private readonly ILiveLog _log;

    /// <summary>
    /// Most powershell.exe processes this runner keeps alive at once, across every host and cluster node
    /// (each is ~60-100 MB). Runs beyond it wait their turn; the wait does not count against the run's deadline.
    /// </summary>
    public const int MaxConcurrentRuns = 8;

    private readonly SemaphoreSlim _processGate = new(MaxConcurrentRuns, MaxConcurrentRuns);

    // Keeps stray WARNING/progress text out of the JSON on stdout.
    private const string OutputPreamble =
        "$WarningPreference = 'SilentlyContinue'\n$ProgressPreference = 'SilentlyContinue'\n";

    public RemoteScriptRunner(
        PowerShellExecutor powerShell,
        RemoteConnectionOptions? options = null,
        ConnectionNameResolver? resolver = null,
        TrustedHostsManager? trustedHosts = null,
        ILiveLog? log = null)
    {
        _powerShell = powerShell ?? throw new ArgumentNullException(nameof(powerShell));
        Options = options ?? new RemoteConnectionOptions();
        _resolver = resolver ?? new ConnectionNameResolver();
        _log = log ?? NullLiveLog.Instance;
        _trustedHosts = trustedHosts ?? new TrustedHostsManager(powerShell, _log);
    }

    public RemoteConnectionOptions Options { get; }

    public async Task<string> RunAsync(
        string computerName,
        string script,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(computerName))
        {
            throw new ArgumentException("Computer name is required.", nameof(computerName));
        }

        await _processGate.WaitAsync(cancellationToken);

        try
        {
            // Hard deadline for the whole call (DNS, TrustedHosts, connect, run). WinRM's own
            // timeouts don't cover a host that connects and then stops responding.
            var deadline = DeadlineFor(Options);

            using var deadlineSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadlineSource.CancelAfter(deadline);

            try
            {
                return await RunCoreAsync(computerName, script, deadlineSource.Token);
            }
            catch (OperationCanceledException)
                when (!cancellationToken.IsCancellationRequested && deadlineSource.IsCancellationRequested)
            {
                _log.Error("Remoting", $"Timed out after {deadline.TotalSeconds:N0}s; the PowerShell process was stopped", computerName);

                throw new TimeoutException(
                    $"'{computerName}' did not finish within {deadline.TotalSeconds:N0} seconds and was stopped.");
            }
        }
        finally
        {
            _processGate.Release();
        }
    }

    /// <summary>Open timeout + operation timeout + slack for process start-up and output transfer.</summary>
    public static TimeSpan DeadlineFor(RemoteConnectionOptions options)
    {
        var operation = options.OperationTimeoutSeconds > 0
            ? options.OperationTimeoutSeconds
            : RemoteConnectionOptions.DefaultOperationTimeoutSeconds;

        return TimeSpan.FromSeconds(Math.Max(options.TimeoutSeconds, 0) + operation + 30);
    }

    private async Task<string> RunCoreAsync(
        string computerName,
        string script,
        CancellationToken cancellationToken)
    {
        script = OutputPreamble + script;

        if (IsLocalTarget(computerName))
        {
            _log.Step("Remoting", "Local target: running script directly in Windows PowerShell 5.1 (no WinRM)", computerName);

            return await _powerShell.ExecuteWindowsPowerShellAsync(
                script,
                cancellationToken,
                logTarget: computerName);
        }

        // Snapshot so a UI edit mid-run can't produce a half-updated command.
        var options = Snapshot(Options);

        _log.Info(
            "Remoting",
            $"Connection settings: credentials={(options.UseCurrentCredentials ? "current Windows user" : "explicit user '" + options.Username + "'")}, " +
            $"auth={options.Authentication}, ssl={options.UseSsl}, port={(options.Port > 0 ? options.Port.ToString() : "default")}, " +
            $"open timeout={options.TimeoutSeconds}s, skipCA={options.SkipCaCertificateCheck}, skipCN={options.SkipCnCheck}",
            computerName);

        ValidateSecurity(options);

        var connectionName = await ResolveConnectionNameAsync(
            computerName.Trim(),
            options,
            cancellationToken);

        _log.Step("Remoting", $"Invoke-Command -ComputerName {connectionName} (endpoint Microsoft.PowerShell)", computerName);

        var command = BuildInvokeCommand(connectionName, script, options);

        return await _powerShell.ExecuteWindowsPowerShellAsync(
            command,
            cancellationToken,
            logTarget: computerName,
            secret: options.UseCurrentCredentials ? null : options.Password);
    }

    private async Task<string> ResolveConnectionNameAsync(
        string computerName,
        RemoteConnectionOptions options,
        CancellationToken cancellationToken)
    {
        var isIp = RemoteConnectionOptions.RequiresExplicitCredentials(computerName);

        if (isIp)
        {
            _log.Step("Resolver", $"'{computerName}' is an IP; reverse-DNS lookup then forward-verify", computerName);
        }

        var resolved = await _resolver.ResolveAsync(computerName, cancellationToken);

        if (resolved.IsVerifiedHostname)
        {
            if (isIp)
            {
                _log.Info("Resolver", $"Verified hostname {resolved.Value} for {computerName}", computerName);
            }

            return resolved.Value;
        }

        _log.Warn("Resolver", $"No verifiable hostname for {computerName}", computerName);

        // Bare IP with no verifiable hostname.
        if (options.UseCurrentCredentials)
        {
            throw new InvalidOperationException(
                $"'{computerName}' could not be resolved to a hostname (no reverse DNS record " +
                "that forward-resolves to the same address). WinRM cannot use Windows " +
                "(Default/Negotiate) authentication against a bare IP address. Use the " +
                "host's name instead, or turn off \"Use current Windows credentials\" in " +
                "Connection Settings and supply a username and password.");
        }

        if (!options.UseSsl)
        {
            if (!options.AllowTrustedHostsChange)
            {
                throw new InvalidOperationException(
                    $"'{computerName}' is a bare IP with no verifiable hostname, so WinRM would need it in this " +
                    "machine's TrustedHosts list (a persistent, machine-wide change that is not made without " +
                    "your consent). Use the host's name, enable HTTPS (SSL), or allow the TrustedHosts change " +
                    "in Connection Settings (CLI: /trusthosts).");
            }

            // HTTPS validates the certificate instead of TrustedHosts.
            await _trustedHosts.EnsureTrustedAsync(resolved.Value, cancellationToken);
        }

        return resolved.Value;
    }

    /// <summary>Rejects settings that would send credentials unprotected, and flags weakened TLS checks.</summary>
    private void ValidateSecurity(RemoteConnectionOptions options)
    {
        if (!options.UseSsl
            && !options.UseCurrentCredentials
            && options.Authentication.Equals("Basic", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Basic authentication over HTTP would send the password unencrypted. " +
                "Enable HTTPS (SSL) or use Negotiate/Kerberos.");
        }

        if (options.SkipCaCertificateCheck || options.SkipCnCheck)
        {
            _log.Warn(
                "Remoting",
                "TLS certificate validation is weakened (skip CA/CN check): the remote host's identity is not fully verified");
        }
    }

    private static string BuildInvokeCommand(
        string connectionName,
        string script,
        RemoteConnectionOptions options)
    {
        var authentication = RemoteConnectionOptions.SupportedAuthentications
            .FirstOrDefault(a => a.Equals(options.Authentication, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException(
                $"Unsupported authentication method '{options.Authentication}'.");

        // The script is embedded in a single-quoted here-string, which a line starting with '@ would end early.
        if (script.Contains("\n'@", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Script contains a here-string terminator (a line starting with '@).");
        }

        var extra = new System.Text.StringBuilder();

        if (!options.UseCurrentCredentials)
        {
            if (string.IsNullOrWhiteSpace(options.Username))
            {
                throw new InvalidOperationException(
                    "A username is required when not using the current Windows credentials.");
            }

            // $hvtxSecret is the password as a SecureString, delivered over the child's private stdin
            // pipe by PowerShellExecutor. It is never part of the script text.
            extra.AppendLine($"$invokeParams['Credential'] = New-Object System.Management.Automation.PSCredential('{Escape(options.Username.Trim())}', $hvtxSecret)");
        }

        if (!authentication.Equals("Default", StringComparison.OrdinalIgnoreCase))
        {
            extra.AppendLine($"$invokeParams['Authentication'] = '{authentication}'");
        }

        if (options.UseSsl)
        {
            extra.AppendLine("$invokeParams['UseSSL'] = $true");
        }

        if (options.Port > 0)
        {
            extra.AppendLine($"$invokeParams['Port'] = {options.Port}");
        }

        var sessionOptions = new List<string>();

        if (options.TimeoutSeconds > 0)
        {
            sessionOptions.Add($"-OpenTimeout {options.TimeoutSeconds * 1000}");
        }

        if (options.SkipCaCertificateCheck)
        {
            sessionOptions.Add("-SkipCACheck");
        }

        if (options.SkipCnCheck)
        {
            sessionOptions.Add("-SkipCNCheck");
        }

        sessionOptions.Add($"-OperationTimeout {(options.OperationTimeoutSeconds > 0 ? options.OperationTimeoutSeconds : RemoteConnectionOptions.DefaultOperationTimeoutSeconds) * 1000}");

        if (sessionOptions.Count > 0)
        {
            extra.AppendLine($"$invokeParams['SessionOption'] = New-PSSessionOption {string.Join(' ', sessionOptions)}");
        }

        return $$"""
            $ErrorActionPreference = 'Stop'
            $remoteScript = @'
            {{script}}
            '@
            $invokeParams = @{
                ComputerName      = '{{Escape(connectionName)}}'
                ConfigurationName = 'Microsoft.PowerShell'
                ScriptBlock       = [scriptblock]::Create($remoteScript)
            }
            {{extra}}
            Invoke-Command @invokeParams
            """;
    }

    private static string Escape(string value) =>
        value.Replace("'", "''", StringComparison.Ordinal);

    private static bool IsLocalTarget(string computerName) =>
        computerName.Equals("localhost", StringComparison.OrdinalIgnoreCase)
        || computerName.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase)
        || computerName.Equals("::1", StringComparison.OrdinalIgnoreCase)
        || computerName.Equals(Environment.MachineName, StringComparison.OrdinalIgnoreCase);

    private static RemoteConnectionOptions Snapshot(RemoteConnectionOptions source) =>
        new()
        {
            UseCurrentCredentials = source.UseCurrentCredentials,
            Username = source.Username,
            Password = source.Password,
            Authentication = source.Authentication,
            UseSsl = source.UseSsl,
            Port = source.Port,
            TimeoutSeconds = source.TimeoutSeconds,
            OperationTimeoutSeconds = source.OperationTimeoutSeconds,
            SkipCaCertificateCheck = source.SkipCaCertificateCheck,
            SkipCnCheck = source.SkipCnCheck,
            AllowTrustedHostsChange = source.AllowTrustedHostsChange
        };
}
