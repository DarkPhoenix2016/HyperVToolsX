using System.Diagnostics;
using System.Management.Automation;
using System.Text;
using HyperVToolsX.Core.Logging;

namespace HyperVToolsX.Infrastructure.PowerShellEngine;

public class PowerShellExecutor
{
    private const string StdinBootstrap =
        "$hvtxIn = New-Object System.IO.StreamReader([Console]::OpenStandardInput(), [System.Text.Encoding]::UTF8); " +
        "& ([scriptblock]::Create($hvtxIn.ReadToEnd()))";

    // Same, but the first stdin line is a base64 secret that becomes the SecureString $hvtxSecret
    // for the script. The secret never touches disk, the environment or the command line.
    private const string StdinBootstrapWithSecret =
        "$ErrorActionPreference = 'Stop'; " +
        "$hvtxIn = New-Object System.IO.StreamReader([Console]::OpenStandardInput(), [System.Text.Encoding]::UTF8); " +
        "$hvtxPlain = [System.Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($hvtxIn.ReadLine())); " +
        "if ($hvtxPlain.Length -eq 0) { $hvtxSecret = New-Object System.Security.SecureString } " +
        "else { $hvtxSecret = ConvertTo-SecureString $hvtxPlain -AsPlainText -Force }; " +
        "$hvtxPlain = $null; " +
        "& ([scriptblock]::Create($hvtxIn.ReadToEnd()))";

    private readonly ILiveLog _log;

    public PowerShellExecutor(ILiveLog? log = null)
    {
        _log = log ?? NullLiveLog.Instance;
    }

    /// <summary>
    /// Executes a raw script string inside the application's PowerShell runspace.
    /// Prefer ExecuteCommandAsync/ExecutePipelineAsync for anything that includes
    /// user-controlled input such as computer names.
    /// </summary>
    public async Task<IReadOnlyList<PSObject>> ExecuteAsync(
        string command,
        CancellationToken cancellationToken = default)
    {
        using var powerShell =
            System.Management.Automation.PowerShell.Create();

        powerShell.AddScript(command);

        return await InvokeAsync(
            powerShell,
            cancellationToken);
    }

    /// <summary>
    /// Executes a single cmdlet with parameters.
    /// </summary>
    public async Task<IReadOnlyList<PSObject>> ExecuteCommandAsync(
        string commandName,
        IReadOnlyDictionary<string, object?>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        using var powerShell =
            System.Management.Automation.PowerShell.Create();

        AddCommand(
            powerShell,
            commandName,
            parameters);

        return await InvokeAsync(
            powerShell,
            cancellationToken);
    }

    /// <summary>
    /// Executes a chain of cmdlets as a single pipeline.
    /// </summary>
    public async Task<IReadOnlyList<PSObject>> ExecutePipelineAsync(
        CancellationToken cancellationToken,
        params (
            string CommandName,
            IReadOnlyDictionary<string, object?>? Parameters)[] steps)
    {
        if (steps.Length == 0)
        {
            throw new ArgumentException(
                "At least one pipeline step is required.",
                nameof(steps));
        }

        using var powerShell =
            System.Management.Automation.PowerShell.Create();

        foreach (var (commandName, parameters) in steps)
        {
            AddCommand(
                powerShell,
                commandName,
                parameters);
        }

        return await InvokeAsync(
            powerShell,
            cancellationToken);
    }

    /// <summary>
    /// Executes a command using the local Windows PowerShell 5.1 executable.
    /// This is used for Hyper-V remote collection so the remote command is
    /// executed through the Windows PowerShell Microsoft.PowerShell endpoint
    /// rather than the application's PowerShell Core runspace.
    /// </summary>
    /// <param name="environmentVariables">
    /// Optional per-process environment variables set on the child process
    /// before it starts. Used to pass sensitive values (e.g. a credential
    /// password) into the script via $env:Name without writing them into
    /// the .ps1 file on disk or into any logged command line. The caller
    /// is responsible for having the script read and then clear these
    /// (Remove-Item Env:\Name) as soon as they're used.
    /// </param>
    /// <param name="secret">
    /// Optional secret (e.g. a password) handed to the script as the SecureString
    /// <c>$hvtxSecret</c> through the private stdin pipe: not in the script text, on disk, in the
    /// environment or on the command line. Never logged.
    /// </param>
    public async Task<string> ExecuteWindowsPowerShellAsync(
    string script,
    CancellationToken cancellationToken = default,
    IReadOnlyDictionary<string, string>? environmentVariables = null,
    string? logTarget = null,
    string? secret = null)
    {
        if (string.IsNullOrWhiteSpace(script))
        {
            throw new ArgumentException("PowerShell script is required.", nameof(script));
        }

        var powershellPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.System),
            @"WindowsPowerShell\v1.0\powershell.exe");

        if (!File.Exists(powershellPath))
        {
            throw new FileNotFoundException("Windows PowerShell 5.1 was not found.", powershellPath);
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = powershellPath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardInputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)
        };

        // The script travels over stdin, never through a file: a file in %TEMP% could be
        // swapped by any unelevated process of the same user before this elevated
        // process runs it. The bootstrap reads stdin as UTF-8 regardless of console codepage.
        foreach (var argument in new[] { "-NoLogo", "-NoProfile", "-NonInteractive", "-Command", secret is null ? StdinBootstrap : StdinBootstrapWithSecret })
        {
            startInfo.ArgumentList.Add(argument);
        }

        // The in-process PowerShell SDK (PowerShell 7) rewrites PSModulePath for this process. Windows
        // PowerShell 5.1 inheriting that can no longer autoload its own modules (Security, Hyper-V...),
        // so let the child rebuild its default module path.
        startInfo.EnvironmentVariables.Remove("PSModulePath");

        if (environmentVariables != null)
        {
            foreach (var (key, value) in environmentVariables)
            {
                startInfo.EnvironmentVariables[key] = value;
            }
        }

        using var process = new Process
        {
            StartInfo = startInfo,
            EnableRaisingEvents = true
        };

        var stopwatch = Stopwatch.StartNew();

        if (!process.Start())
        {
            throw new InvalidOperationException("Unable to start Windows PowerShell 5.1.");
        }

        // Only variable NAMES are logged, never their values.
        _log.Step(
            "PowerShell",
            $"Started powershell.exe 5.1 (pid {process.Id}), script {script.Length:N0} chars" +
            (environmentVariables is { Count: > 0 }
                ? $", env: {string.Join(", ", environmentVariables.Keys)}"
                : string.Empty) +
            (secret is null ? string.Empty : ", secret via stdin"),
            logTarget);

        using var registration = cancellationToken.Register(() =>
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch
            {
                // Process may already have exited.
            }
        });

        var standardOutputTask = process.StandardOutput.ReadToEndAsync();
        var standardErrorTask = process.StandardError.ReadToEndAsync();

        try
        {
            if (secret is not null)
            {
                await process.StandardInput.WriteLineAsync(
                    Convert.ToBase64String(Encoding.UTF8.GetBytes(secret)).AsMemory(),
                    cancellationToken);
            }

            await process.StandardInput.WriteAsync(script.AsMemory(), cancellationToken);
            process.StandardInput.Close();
        }
        catch (IOException)
        {
            // The process exited before reading everything; its exit code and stderr explain why.
        }

        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch
            {
                // Process may already have exited.
            }

            throw;
        }

        var standardOutput = await standardOutputTask;
        var standardError = await standardErrorTask;

        stopwatch.Stop();

        if (cancellationToken.IsCancellationRequested)
        {
            _log.Warn("PowerShell", $"Process cancelled after {stopwatch.Elapsed.TotalSeconds:F1}s", logTarget);
        }

        cancellationToken.ThrowIfCancellationRequested();

        _log.Info(
            "PowerShell",
            $"Process exited with code {process.ExitCode} after {stopwatch.Elapsed.TotalSeconds:F1}s " +
            $"(stdout {standardOutput.Length:N0} chars, stderr {standardError.Length:N0} chars)",
            logTarget);

        if (process.ExitCode != 0)
        {
            _log.Error(
                "PowerShell",
                $"stderr: {Truncate(standardError, 600)}",
                logTarget);

            var error = string.IsNullOrWhiteSpace(standardError)
                ? $"Windows PowerShell exited with code {process.ExitCode}."
                : standardError.Trim();

            throw new InvalidOperationException(error);
        }

        if (string.IsNullOrWhiteSpace(standardOutput))
        {
            throw new InvalidOperationException("Windows PowerShell returned no output.");
        }

        return standardOutput.Trim();
    }

    private static string Truncate(string value, int max)
    {
        var trimmed = value.Trim();
        return trimmed.Length <= max ? trimmed : trimmed[..max] + "...";
    }

    private static void AddCommand(
        System.Management.Automation.PowerShell powerShell,
        string commandName,
        IReadOnlyDictionary<string, object?>? parameters)
    {
        powerShell.AddCommand(commandName);

        if (parameters == null)
        {
            return;
        }

        foreach (var parameter in parameters)
        {
            powerShell.AddParameter(
                parameter.Key,
                parameter.Value);
        }
    }

    private static async Task<IReadOnlyList<PSObject>> InvokeAsync(
        System.Management.Automation.PowerShell powerShell,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var registration =
            cancellationToken.Register(() =>
            {
                try
                {
                    powerShell.Stop();
                }
                catch
                {
                    // Pipeline may have already completed/disposed.
                }
            });

        PSDataCollection<PSObject> output;

        try
        {
            output = await Task.Factory.FromAsync(
                powerShell.BeginInvoke(),
                powerShell.EndInvoke);
        }
        catch (PipelineStoppedException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(
                cancellationToken);
        }

        cancellationToken.ThrowIfCancellationRequested();

        ThrowIfErrors(powerShell);

        return output.ToList();
    }

    private static void ThrowIfErrors(
        System.Management.Automation.PowerShell powerShell)
    {
        if (!powerShell.HadErrors)
        {
            return;
        }

        var errors = string.Join(
            Environment.NewLine,
            powerShell.Streams.Error.Select(
                error => error.ToString()));

        throw new InvalidOperationException(errors);
    }
}