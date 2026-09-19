using HyperVToolsX.Core.Enums;
using HyperVToolsX.Core.Models;

namespace HyperVToolsX.Core.Cli;

/// <summary>Process exit codes of command-line mode, so scheduled tasks can react to them.</summary>
public static class CliExitCode
{
    /// <summary>Every target was collected and the export was written.</summary>
    public const int Success = 0;

    /// <summary>Nothing was exported: no target could be collected, the run was cancelled, or the export failed.</summary>
    public const int Failure = 1;

    /// <summary>The arguments were invalid.</summary>
    public const int UsageError = 2;

    /// <summary>The export was written, but at least one target failed.</summary>
    public const int PartialSuccess = 3;
}

public enum ExportFormat
{
    Xlsx,
    Csv
}

public sealed class CommandLineOptions
{
    public IReadOnlyList<string> Hosts { get; init; } = [];

    /// <summary>What was given to /export: a folder, or a complete .xlsx/.csv file path.</summary>
    public string ExportPath { get; init; } = string.Empty;

    /// <summary>True when <see cref="ExportPath"/> is a folder and the file name is generated.</summary>
    public bool ExportIsFolder { get; init; }

    public ExportFormat Format { get; init; } = ExportFormat.Xlsx;

    public bool ExportsCsv => Format == ExportFormat.Csv;

    /// <summary>
    /// The file for one host. In a folder it is "&lt;host&gt;_&lt;yyyyMMdd-HHmmss&gt;.xlsx|csv"; with an explicit
    /// file path (single host only) it is that path. CSV output adds "-&lt;tab&gt;" to the name, one file per tab.
    /// </summary>
    public string ResolveExportFile(string host, DateTime now)
    {
        if (!ExportIsFolder)
        {
            return ExportPath;
        }

        var extension = Format == ExportFormat.Csv ? ".csv" : ".xlsx";

        return Path.Combine(ExportPath, $"{Sanitize(host)}_{now:yyyyMMdd-HHmmss}{extension}");
    }

    /// <summary>The folder the output goes to: <see cref="ExportPath"/> itself, or the folder of an explicit file path.</summary>
    public string ExportFolder =>
        ExportIsFolder ? ExportPath : Path.GetDirectoryName(Path.GetFullPath(ExportPath)) ?? ".";

    /// <summary>
    /// The file for one node of a cluster: "&lt;folder&gt;\&lt;cluster&gt;\&lt;node&gt;_&lt;yyyyMMdd-HHmmss&gt;.xlsx|csv".
    /// </summary>
    public string ResolveClusterNodeFile(string cluster, string node, DateTime now)
    {
        var extension = Format == ExportFormat.Csv ? ".csv" : ".xlsx";

        return Path.Combine(ExportFolder, Sanitize(cluster), $"{Sanitize(node)}_{now:yyyyMMdd-HHmmss}{extension}");
    }

    private static string Sanitize(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();

        return string.Concat(name.Select(c => Array.IndexOf(invalid, c) >= 0 ? '_' : c));
    }

    /// <summary>Empty means "run as the current Windows user".</summary>
    public string Username { get; init; } = string.Empty;

    public string Password { get; init; } = string.Empty;

    public string Authentication { get; init; } = RemoteConnectionOptions.DefaultAuthentication;

    public bool UseSsl { get; init; }

    /// <summary>0 = the WinRM default for the transport.</summary>
    public int Port { get; init; }

    public int TimeoutSeconds { get; init; } = 30;

    public bool SkipCaCertificateCheck { get; init; }

    public bool SkipCnCheck { get; init; }

    public bool Silent { get; init; }

    public SizeUnit SizeUnit { get; init; } = SizeUnit.GB;

    public RemoteConnectionOptions ToConnectionOptions() => new()
    {
        UseCurrentCredentials = string.IsNullOrEmpty(Username),
        Username = Username,
        Password = Password,
        Authentication = Authentication,
        UseSsl = UseSsl,
        Port = Port,
        TimeoutSeconds = TimeoutSeconds,
        SkipCaCertificateCheck = SkipCaCertificateCheck,
        SkipCnCheck = SkipCnCheck
    };
}

public sealed record CommandLineParseResult(
    CommandLineOptions? Options,
    IReadOnlyList<string> Errors,
    bool ShowHelp)
{
    public bool IsValid => Options is not null && Errors.Count == 0;
}

/// <summary>
/// Parses the RVTools-style switches, e.g.
/// <c>/host:HV01 /export:C:\Reports /type:xlsx /ssl</c>.
/// Switches start with <c>/</c>, <c>-</c> or <c>--</c>, are case-insensitive and take their value after
/// <c>:</c> or <c>=</c>.
/// </summary>
public static class CommandLineParser
{
    public static CommandLineParseResult Parse(IEnumerable<string> args)
    {
        var errors = new List<string>();
        var hosts = new List<string>();

        string? export = null;
        string? type = null;
        string? hostFile = null;
        string? user = null;
        string? password = null;
        string? auth = null;
        string? unit = null;
        var ssl = false;
        var skipCa = false;
        var skipCn = false;
        var silent = false;
        var help = false;
        int? port = null;
        int? timeout = null;

        foreach (var arg in args)
        {
            if (!TrySplit(arg, out var name, out var value))
            {
                errors.Add($"Unexpected argument '{arg}'. Switches must start with '/'.");
                continue;
            }

            switch (name)
            {
                case "?" or "h" or "help":
                    help = true;
                    break;

                case "host":
                    hosts.AddRange((value ?? string.Empty).Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
                    break;

                case "export":
                    export = value;
                    break;

                case "type":
                    type = value;
                    break;

                case "hostfile":
                    hostFile = value;
                    break;

                case "user":
                    user = value;
                    break;

                case "password":
                    password = value;
                    break;

                case "auth":
                    auth = value;
                    break;

                case "unit":
                    unit = value;
                    break;

                case "ssl":
                    ssl = true;
                    break;

                case "skipca":
                    skipCa = true;
                    break;

                case "skipcn":
                    skipCn = true;
                    break;

                case "silent":
                    silent = true;
                    break;

                case "port":
                    port = ParseInt(name, value, 0, 65535, errors);
                    break;

                case "timeout":
                    timeout = ParseInt(name, value, 1, int.MaxValue, errors);
                    break;

                default:
                    errors.Add($"Unknown option '{arg}'.");
                    break;
            }
        }

        if (help)
        {
            return new CommandLineParseResult(null, [], ShowHelp: true);
        }

        if (!string.IsNullOrWhiteSpace(hostFile))
        {
            hosts.AddRange(ReadHostFile(hostFile.Trim(), errors));
        }
        else if (hostFile is not null)
        {
            errors.Add("/hostfile:<path> needs a file path.");
        }

        if (hosts.Count == 0 && errors.Count == 0)
        {
            errors.Add("/host:<name> or /hostfile:<path> is required.");
        }

        ExportFormat? format = null;

        if (!string.IsNullOrEmpty(type))
        {
            format = type.Trim().ToLowerInvariant() switch
            {
                "xlsx" or "excel" => ExportFormat.Xlsx,
                "csv" => ExportFormat.Csv,
                _ => null
            };

            if (format is null)
            {
                errors.Add("/type must be xlsx or csv.");
            }
        }

        var exportIsFolder = false;

        if (string.IsNullOrWhiteSpace(export))
        {
            errors.Add("/export:<path> is required.");
        }
        else
        {
            var extension = Path.GetExtension(export.Trim());

            if (string.IsNullOrEmpty(extension))
            {
                // A folder: the file name is generated, so the format has to be stated.
                exportIsFolder = true;

                if (string.IsNullOrEmpty(type))
                {
                    errors.Add("/type:<xlsx|csv> is required when /export is a folder.");
                }
            }
            else
            {
                var fromFile = extension.Equals(".xlsx", StringComparison.OrdinalIgnoreCase) ? ExportFormat.Xlsx
                    : extension.Equals(".csv", StringComparison.OrdinalIgnoreCase) ? ExportFormat.Csv
                    : (ExportFormat?)null;

                if (fromFile is null)
                {
                    errors.Add("/export must be a folder, or a file ending in .xlsx or .csv.");
                }
                else if (hosts.Distinct(StringComparer.OrdinalIgnoreCase).Count() > 1)
                {
                    errors.Add("Every host gets its own file, so with several hosts /export must be a folder (not a single file).");
                }
                else if (format is not null && format != fromFile)
                {
                    errors.Add($"/type:{type} doesn't match the file extension '{extension}'.");
                }
                else
                {
                    format = fromFile;
                }
            }
        }

        if (!string.IsNullOrEmpty(user) && string.IsNullOrEmpty(password))
        {
            errors.Add("/user requires /password.");
        }

        if (!string.IsNullOrEmpty(password) && string.IsNullOrEmpty(user))
        {
            errors.Add("/password requires /user.");
        }

        var authentication = RemoteConnectionOptions.DefaultAuthentication;

        if (!string.IsNullOrEmpty(auth))
        {
            var match = RemoteConnectionOptions.SupportedAuthentications
                .FirstOrDefault(a => string.Equals(a, auth, StringComparison.OrdinalIgnoreCase));

            if (match is null)
            {
                errors.Add($"/auth must be one of: {string.Join(", ", RemoteConnectionOptions.SupportedAuthentications)}.");
            }
            else
            {
                authentication = match;
            }
        }

        var sizeUnit = SizeUnit.GB;

        if (!string.IsNullOrEmpty(unit)
            && (!Enum.TryParse(unit, ignoreCase: true, out sizeUnit) || !Enum.IsDefined(sizeUnit)))
        {
            errors.Add($"/unit must be one of: {string.Join(", ", Enum.GetNames<SizeUnit>())}.");
        }

        if (errors.Count > 0)
        {
            return new CommandLineParseResult(null, errors, ShowHelp: false);
        }

        return new CommandLineParseResult(
            new CommandLineOptions
            {
                Hosts = hosts.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                ExportPath = export!.Trim(),
                ExportIsFolder = exportIsFolder,
                Format = format ?? ExportFormat.Xlsx,
                Username = user ?? string.Empty,
                Password = password ?? string.Empty,
                Authentication = authentication,
                UseSsl = ssl,
                Port = port ?? 0,
                TimeoutSeconds = timeout ?? 30,
                SkipCaCertificateCheck = skipCa,
                SkipCnCheck = skipCn,
                Silent = silent,
                SizeUnit = sizeUnit
            },
            [],
            ShowHelp: false);
    }

    public static string Usage(string version) => $"""
        HyperVToolsX v{version} - Command Line Usage
        {new string('=', 24 + version.Length + 9)}

        USAGE:
          HyperVToolsX.exe /host:<hostname> /export:<folder> /type:<xlsx|csv> [options]
          HyperVToolsX.exe /hostfile:<hosts.txt> /export:<folder> /type:<xlsx|csv> [options]

        REQUIRED PARAMETERS:
          /host:<name>      Hyper-V host or cluster name (several: /host:HV01,HV02)
          /hostfile:<path>  Text file with host names, one per line (blank lines and
                            lines starting with # are ignored). Can be combined with /host.
                            One of /host or /hostfile is required.
          /export:<folder>  Folder for the output. Every host gets its own file,
                            named <hostname>_<yyyyMMdd-HHmmss>. A cluster name gets a
                            folder <cluster>\ with one file per node
          /type:<format>    xlsx or csv. csv writes one file per tab:
                            <hostname>_<yyyyMMdd-HHmmss>-<tab>.csv

                AUTHENTICATION OPTIONS:
          /user:<username>  Username (domain\user format)
          /password:<pwd>   Password (use with /user)
          /auth:<method>    Authentication: Default, Negotiate,
                            Kerberos, Basic, CredSSP

        CONNECTION OPTIONS:
          /ssl              Use HTTPS (port 5986)
          /port:<number>    Custom port (default: 5985/5986)
          /skipca           Skip CA certificate check
          /skipcn           Skip CN hostname check
          /timeout:<secs>   Connection timeout (default: 30)

        OTHER OPTIONS:
          /unit:<unit>      Size unit for exported sizes: Bytes, KB, MB, GB, TB, PB
                            (default: GB)
          /silent           Suppress output (for scheduled tasks)
          /?                Show this help

        Custom tabs saved in the Templates folder next to the exe are exported
        as the first sheets (or files).

        EXIT CODES:
          0  Success                     2  Invalid arguments
          1  Failure (nothing exported)  3  Exported, but some hosts failed

        EXAMPLES:
          HyperVToolsX.exe /host:HV01 /export:C:\Reports /type:xlsx
              -> C:\Reports\HV01_20260920-031500.xlsx
          HyperVToolsX.exe /hostfile:C:\hosts.txt /export:D:\Out /type:xlsx /ssl
              -> one file per host in D:\Out
          HyperVToolsX.exe /host:HV01.domain.com /export:D:\Out /type:csv /auth:Kerberos

        With a single host, a full file path also works: /export:D:\out.xlsx
        (the type follows the extension).
        """;

    /// <summary>One host per line (commas and semicolons also separate); blank lines and lines starting with # are ignored.</summary>
    public static IReadOnlyList<string> ParseHostList(IEnumerable<string> lines) =>
        lines
            .Select(l => l.Trim())
            .Where(l => l.Length > 0 && l[0] != '#')
            .SelectMany(l => l.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .ToList();

    private static IReadOnlyList<string> ReadHostFile(string path, List<string> errors)
    {
        try
        {
            var hosts = ParseHostList(File.ReadAllLines(path));

            if (hosts.Count == 0)
            {
                errors.Add($"The host file '{path}' contains no host names.");
            }

            return hosts;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
        {
            errors.Add($"Could not read the host file '{path}': {ex.Message}");
            return [];
        }
    }

    private static int? ParseInt(string name, string? value, int min, int max, List<string> errors)
    {
        if (int.TryParse(value, out var number) && number >= min && number <= max)
        {
            return number;
        }

        errors.Add($"/{name} must be a number between {min} and {(max == int.MaxValue ? "2147483647" : max.ToString())}.");
        return null;
    }

    private static bool TrySplit(string arg, out string name, out string? value)
    {
        name = string.Empty;
        value = null;

        if (string.IsNullOrEmpty(arg))
        {
            return false;
        }

        var body = arg.StartsWith("--", StringComparison.Ordinal) ? arg[2..]
            : arg[0] is '/' or '-' ? arg[1..]
            : null;

        if (string.IsNullOrEmpty(body))
        {
            return false;
        }

        var separator = body.IndexOfAny([':', '=']);

        if (separator < 0)
        {
            name = body.ToLowerInvariant();
        }
        else
        {
            name = body[..separator].ToLowerInvariant();
            value = body[(separator + 1)..];
        }

        return true;
    }
}
