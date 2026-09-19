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
    public const string PasswordEnvironmentVariable = "HVTX_REMOTE_PWD";

    private readonly PowerShellExecutor _powerShell;
    private readonly ConnectionNameResolver _resolver;
    private readonly TrustedHostsManager _trustedHosts;
    private readonly ILiveLog _log;

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

        var connectionName = await ResolveConnectionNameAsync(
            computerName.Trim(),
            options,
            cancellationToken);

        _log.Step("Remoting", $"Invoke-Command -ComputerName {connectionName} (endpoint Microsoft.PowerShell)", computerName);

        var command = BuildInvokeCommand(connectionName, script, options);

        Dictionary<string, string>? environment = null;

        if (!options.UseCurrentCredentials)
        {
            environment = new Dictionary<string, string>
            {
                [PasswordEnvironmentVariable] = options.Password
            };
        }

        return await _powerShell.ExecuteWindowsPowerShellAsync(
            command,
            cancellationToken,
            environment,
            computerName);
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
            // HTTPS validates the certificate instead of TrustedHosts.
            await _trustedHosts.EnsureTrustedAsync(resolved.Value, cancellationToken);
        }

        return resolved.Value;
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

        var extra = new System.Text.StringBuilder();

        if (!options.UseCurrentCredentials)
        {
            if (string.IsNullOrWhiteSpace(options.Username))
            {
                throw new InvalidOperationException(
                    "A username is required when not using the current Windows credentials.");
            }

            // Password is read from the environment and removed immediately;
            // it is never part of the script text.
            extra.AppendLine($"$securePwd = ConvertTo-SecureString $env:{PasswordEnvironmentVariable} -AsPlainText -Force");
            extra.AppendLine($"Remove-Item Env:\\{PasswordEnvironmentVariable} -ErrorAction SilentlyContinue");
            extra.AppendLine($"$invokeParams['Credential'] = New-Object System.Management.Automation.PSCredential('{Escape(options.Username.Trim())}', $securePwd)");
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
            SkipCaCertificateCheck = source.SkipCaCertificateCheck,
            SkipCnCheck = source.SkipCnCheck
        };
}
