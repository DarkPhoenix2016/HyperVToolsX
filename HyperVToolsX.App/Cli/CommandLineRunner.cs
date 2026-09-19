using System.IO;
using System.Reflection;
using HyperVToolsX.Core.Cli;
using HyperVToolsX.Core.Collection;
using HyperVToolsX.Core.Interfaces;
using HyperVToolsX.Core.Models;
using HyperVToolsX.Core.Templates;
using HyperVToolsX.Core.Logging;
using HyperVToolsX.Export;
using HyperVToolsX.Infrastructure.Collection;
using HyperVToolsX.Infrastructure.PowerShellEngine;
using HyperVToolsX.Infrastructure.Remoting;
using HyperVToolsX.Infrastructure.Templates;
using HyperVToolsX.Infrastructure.Validation;

namespace HyperVToolsX.App.Cli;

/// <summary>
/// Headless mode: collect the given hosts, write the export, return an exit code. Uses the same collection
/// pipeline as the window (validation, orchestrator, collector) and the same Templates folder.
/// </summary>
internal static class CommandLineRunner
{
    public static async Task<int> RunAsync(string[] args)
    {
        var parsed = CommandLineParser.Parse(args);

        // Silence only applies to a valid run; help and argument errors always need to be readable.
        var silent = parsed.IsValid && parsed.Options!.Silent;

        using var console = silent ? null : ConsoleSession.Open();

        if (parsed.ShowHelp)
        {
            Console.WriteLine(CommandLineParser.Usage(Version()));
            return CliExitCode.Success;
        }

        if (!parsed.IsValid)
        {
            foreach (var error in parsed.Errors)
            {
                Console.Error.WriteLine($"Error: {error}");
            }

            Console.Error.WriteLine();
            Console.Error.WriteLine("Run HyperVToolsX.exe /? for usage.");
            return CliExitCode.UsageError;
        }

        var options = parsed.Options!;

        try
        {
            return await ExecuteAsync(options);
        }
        catch (Exception ex)
        {
            Write(options, $"Error: {ex.Message}", error: true);
            return CliExitCode.Failure;
        }
    }

    private static async Task<int> ExecuteAsync(CommandLineOptions options)
    {
        using var cts = new CancellationTokenSource();

        ConsoleCancelEventHandler onCancel = (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        Console.CancelKeyPress += onCancel;

        try
        {
            var log = new ConsoleLiveLog(options.Silent);
            var powerShell = new PowerShellExecutor(log);
            var runner = new RemoteScriptRunner(powerShell, options.ToConnectionOptions(), log: log);
            var validator = new TargetValidator(runner, log);
            var collector = new RemoteInventoryCollector(runner, log);

            var templates = new TemplateStore(TemplateStore.DefaultFolder, log).LoadAll();

            IInventoryExporter exporter = options.ExportsCsv ? new CsvInventoryExporter() : new ExcelInventoryExporter();

            var hosts = TargetNormalizer.Normalize(options.Hosts).Select(t => t.Name).ToList();

            Write(options, $"HyperVToolsX v{Version()}");
            Write(options, $"Collecting {hosts.Count} host(s), one output file each: {string.Join(", ", hosts)}");
            Write(
                options,
                templates.Count == 0
                    ? $"Custom tabs: none found in {TemplateStore.DefaultFolder}"
                    : $"Custom tabs: including {templates.Count} from {TemplateStore.DefaultFolder} " +
                      $"({string.Join(", ", templates.Select(t => t.Name))})");

            // Each host is collected on its own so it ends up in its own file, and one
            // host failing can't affect another. A few run at a time, like the window does.
            using var gate = new SemaphoreSlim(WorkerConfiguration.CalculateWorkerCount(hosts.Count));

            async Task<bool> ProcessHostAsync(string host)
            {
                await gate.WaitAsync(cts.Token);

                try
                {
                    var cache = new InventoryCache();
                    var orchestrator = new CollectionOrchestrator(validator, collector, cache, log);

                    Write(options, $"  START   {host}");

                    var result = await orchestrator.CollectAsync(
                        TargetNormalizer.Normalize([host]),
                        new CollectionRequest { MaxConcurrentTargets = 1 },
                        progress: null,
                        cts.Token);

                    if (result.SuccessfulTargets == 0)
                    {
                        var target = result.Targets.FirstOrDefault();

                        Write(
                            options,
                            $"  FAILED  {host}: {target?.Validation.ErrorMessage ?? target?.Validation.Status.ToString() ?? "no result"}",
                            error: true);

                        return false;
                    }

                    var collected = result.Targets[0];

                    // A cluster name is written as one file per node, in a folder named after the cluster.
                    if (collected.Type == Core.Enums.TargetType.Cluster && collected.NodeTargets.Count > 0)
                    {
                        return await ExportClusterAsync(collected, exporter, templates, options, cts.Token);
                    }

                    var file = Path.GetFullPath(options.ResolveExportFile(host, DateTime.Now));
                    var count = await exporter.ExportAsync(cache.GetSnapshot(), file, options.SizeUnit, templates, cts.Token);

                    Write(
                        options,
                        options.ExportsCsv
                            ? $"  OK      {host}: {count} file(s), {Path.Combine(Path.GetDirectoryName(file)!, Path.GetFileNameWithoutExtension(file))}-<tab>.csv " +
                              $"({result.TotalVirtualMachines} VM(s))"
                            : $"  OK      {host}: {file} ({count} sheet(s), {result.TotalVirtualMachines} VM(s))");

                    return true;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    Write(options, $"  FAILED  {host}: {ex.Message}", error: true);
                    return false;
                }
                finally
                {
                    gate.Release();
                }
            }

            var outcomes = await Task.WhenAll(hosts.Select(ProcessHostAsync));

            var succeeded = outcomes.Count(ok => ok);
            var failed = outcomes.Length - succeeded;

            Write(options, $"Done: {succeeded} host(s) exported, {failed} failed.");

            return succeeded == 0 ? CliExitCode.Failure
                : failed > 0 ? CliExitCode.PartialSuccess
                : CliExitCode.Success;
        }
        catch (OperationCanceledException)
        {
            Write(options, "Cancelled.", error: true);
            return CliExitCode.Failure;
        }
        finally
        {
            Console.CancelKeyPress -= onCancel;
        }
    }

    /// <summary>Writes each node of a collected cluster to its own file inside "&lt;export&gt;\&lt;cluster&gt;".</summary>
    private static async Task<bool> ExportClusterAsync(
        HyperVTarget cluster,
        IInventoryExporter exporter,
        IReadOnlyList<CustomTabTemplate> templates,
        CommandLineOptions options,
        CancellationToken cancellationToken)
    {
        var allWritten = true;
        var exported = 0;

        foreach (var node in cluster.NodeTargets)
        {
            try
            {
                // Each node is exported from its own data, as a standalone host would be.
                var nodeCache = new InventoryCache();
                nodeCache.UpdateTarget(node);

                var file = Path.GetFullPath(options.ResolveClusterNodeFile(cluster.Name, node.Name, DateTime.Now));

                var count = await exporter.ExportAsync(nodeCache.GetSnapshot(), file, options.SizeUnit, templates, cancellationToken);
                exported++;

                Write(
                    options,
                    options.ExportsCsv
                        ? $"  OK      {cluster.Name} / {node.Name}: {count} file(s), {Path.Combine(Path.GetDirectoryName(file)!, Path.GetFileNameWithoutExtension(file))}-<tab>.csv " +
                          $"({node.VirtualMachines.Count} VM(s))"
                        : $"  OK      {cluster.Name} / {node.Name}: {file} ({count} sheet(s), {node.VirtualMachines.Count} VM(s))");
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                allWritten = false;
                Write(options, $"  FAILED  {cluster.Name} / {node.Name}: {ex.Message}", error: true);
            }
        }

        Write(options, $"  Cluster {cluster.Name}: {exported} of {cluster.NodeTargets.Count} node file(s) written.");

        return allWritten && exported > 0;
    }

    private static void Write(CommandLineOptions options, string message, bool error = false)
    {
        if (options.Silent)
        {
            return;
        }

        (error ? Console.Error : Console.Out).WriteLine(message);
    }

    private static string Version()
    {
        var assembly = Assembly.GetExecutingAssembly();

        var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? assembly.GetName().Version?.ToString()
            ?? "0.1.0";

        // Drop the "+commit" build metadata.
        var plus = version.IndexOf('+');
        return plus >= 0 ? version[..plus] : version;
    }

    /// <summary>Only warnings and errors from the pipeline reach the console; the rest is too chatty for a CLI.</summary>
    private sealed class ConsoleLiveLog : ILiveLog
    {
        private readonly bool _silent;

        public ConsoleLiveLog(bool silent) => _silent = silent;

        public void Write(LiveLogLevel level, string source, string message, string? target = null)
        {
            if (_silent || level < LiveLogLevel.Warning)
            {
                return;
            }

            try
            {
                Console.Error.WriteLine(
                    $"  {(level == LiveLogLevel.Error ? "ERROR" : "WARN")}  {(target is null ? "" : target + ": ")}{message}");
            }
            catch (Exception)
            {
                // A log sink must never throw.
            }
        }
    }
}
