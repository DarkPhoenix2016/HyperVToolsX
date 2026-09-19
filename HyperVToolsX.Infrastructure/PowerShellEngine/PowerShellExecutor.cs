using System.Diagnostics;
using System.Management.Automation;
using System.Text;
using HyperVToolsX.Core.Logging;

namespace HyperVToolsX.Infrastructure.PowerShellEngine;

public class PowerShellExecutor
{
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
    public async Task<string> ExecuteWindowsPowerShellAsync(
    string script,
    CancellationToken cancellationToken = default,
    IReadOnlyDictionary<string, string>? environmentVariables = null,
    string? logTarget = null)
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

        var tempDirectory = Path.Combine(Path.GetTempPath(), "HyperVToolsX", "PowerShell");
        Directory.CreateDirectory(tempDirectory);

        var scriptPath = Path.Combine(tempDirectory, $"HyperVToolsX_{Guid.NewGuid():N}.ps1");

        try
        {
            await File.WriteAllTextAsync(
                scriptPath,
                script,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                cancellationToken);

            var startInfo = new ProcessStartInfo
            {
                FileName = powershellPath,
                Arguments = $"-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -File \"{scriptPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

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
                    : string.Empty),
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
        finally
        {
            try
            {
                if (File.Exists(scriptPath))
                {
                    File.Delete(scriptPath);
                }
            }
            catch
            {
                // Cleanup failure should not hide the actual result.
            }
        }
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