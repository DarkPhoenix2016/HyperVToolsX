using System.Management.Automation;

namespace HyperVToolsX.Infrastructure.PowerShellEngine;

public class PowerShellExecutor
{
    public async Task<IReadOnlyList<PSObject>> ExecutePipelineAsync(
        CancellationToken cancellationToken,
        params (string CommandName, IReadOnlyDictionary<string, object?>? Parameters)[] steps)
    {
        if (steps.Length == 0)
        {
            throw new ArgumentException("At least one pipeline step is required.", nameof(steps));
        }

        using var powerShell = PowerShell.Create();

        foreach (var (commandName, parameters) in steps)
        {
            powerShell.AddCommand(commandName);

            if (parameters == null)
            {
                continue;
            }

            foreach (var parameter in parameters)
            {
                powerShell.AddParameter(parameter.Key, parameter.Value);
            }
        }

        return await InvokeAsync(powerShell, cancellationToken);
    }

    private static async Task<IReadOnlyList<PSObject>> InvokeAsync(
        PowerShell powerShell,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var registration = cancellationToken.Register(() =>
        {
            try
            {
                powerShell.Stop();
            }
            catch
            {
            }
        });

        PSDataCollection<PSObject> output;

        try
        {
            output = await Task.Factory.FromAsync(
                powerShell.BeginInvoke(),
                powerShell.EndInvoke);
        }
        catch (PipelineStoppedException) when (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(cancellationToken);
        }

        cancellationToken.ThrowIfCancellationRequested();

        if (powerShell.HadErrors)
        {
            var errors = string.Join(
                Environment.NewLine,
                powerShell.Streams.Error.Select(error => error.ToString()));

            throw new InvalidOperationException(errors);
        }

        return output.ToList();
    }
}