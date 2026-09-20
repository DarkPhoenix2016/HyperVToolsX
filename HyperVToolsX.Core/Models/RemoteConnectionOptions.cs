using System.Net;

namespace HyperVToolsX.Core.Models;

/// <summary>
/// WinRM connection settings shared by target validation and inventory
/// collection. One instance is created by the composition root and mutated by
/// the Connection Settings tab, so changes apply to the next operation.
/// </summary>
public class RemoteConnectionOptions
{
    public const string DefaultAuthentication = "Default";

    /// <summary>
    /// Authentication mechanisms accepted by Invoke-Command -Authentication.
    /// Anything else is rejected rather than interpolated into a script.
    /// </summary>
    public static readonly IReadOnlyList<string> SupportedAuthentications =
        ["Default", "Negotiate", "Kerberos", "Basic", "CredSSP"];

    public bool UseCurrentCredentials { get; set; } = true;

    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Plaintext by necessity (it ends up in a PSCredential). It is only ever
    /// handed to the child PowerShell process over its private stdin pipe and
    /// must never be logged or written into script text, files or command lines.
    /// </summary>
    public string Password { get; set; } = string.Empty;

    public string Authentication { get; set; } = DefaultAuthentication;

    public bool UseSsl { get; set; }

    /// <summary>0 = WinRM default for the transport (5985 HTTP / 5986 HTTPS).</summary>
    public int Port { get; set; }

    /// <summary>Seconds allowed to open the WinRM session (0 = no open timeout).</summary>
    public int TimeoutSeconds { get; set; } = 30;

    public const int DefaultOperationTimeoutSeconds = 300;

    /// <summary>
    /// Seconds a single remote script may run once connected. Always enforced (values below 1
    /// fall back to the default) so an unresponsive host can never stall a collection.
    /// </summary>
    public int OperationTimeoutSeconds { get; set; } = DefaultOperationTimeoutSeconds;

    public bool SkipCaCertificateCheck { get; set; }

    public bool SkipCnCheck { get; set; }

    /// <summary>
    /// Permits the app to append a bare-IP target to this machine's WinRM TrustedHosts list
    /// (a persistent, machine-wide change). Off unless the user explicitly opts in.
    /// </summary>
    public bool AllowTrustedHostsChange { get; set; }

    /// <summary>
    /// True when the target is a bare IP address. Default/Negotiate
    /// authentication cannot pass through to a bare IP (anti-NTLM-reflection),
    /// so such a target needs a resolvable hostname or explicit credentials.
    /// </summary>
    public static bool RequiresExplicitCredentials(string target) =>
        !string.IsNullOrWhiteSpace(target)
        && IPAddress.TryParse(target.Trim(), out _);
}
