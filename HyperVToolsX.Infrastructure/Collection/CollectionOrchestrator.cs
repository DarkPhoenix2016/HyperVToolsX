using HyperVToolsX.Core.Collection;
using HyperVToolsX.Core.Interfaces;
using HyperVToolsX.Core.Logging;
using HyperVToolsX.Core.Models;

namespace HyperVToolsX.Infrastructure.Collection;

public class CollectionOrchestrator
{
    private readonly ITargetValidator _targetValidator;

    private readonly IInventoryCollector _inventoryCollector;

    private readonly IInventoryCache _inventoryCache;

    private readonly ILiveLog _log;

    public CollectionOrchestrator(
        ITargetValidator targetValidator,
        IInventoryCollector inventoryCollector,
        IInventoryCache inventoryCache,
        ILiveLog? log = null)
    {
        _log = log ?? NullLiveLog.Instance;

        _targetValidator = targetValidator;

        _inventoryCollector =
            inventoryCollector;

        _inventoryCache =
            inventoryCache;
    }

    public async Task<CollectionResult> CollectAsync(
        IEnumerable<HyperVTarget> targets,
        CollectionRequest request,
        IProgress<CollectionProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var targetList = targets
            .GroupBy(
                target => target.Name,
                StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();

        var result = new CollectionResult
        {
            Targets = targetList,
            TotalTargets = targetList.Count,
            StartedAt = DateTime.Now
        };

        if (targetList.Count == 0)
        {
            result.CompletedAt = DateTime.Now;
            return result;
        }

        var workerCount =
    WorkerConfiguration.CalculateWorkerCount(
        targetList.Count);

        _log.Info(
            "Orchestrator",
            $"Collection started: {targetList.Count} target(s), {workerCount} worker(s) (auto, max {WorkerConfiguration.MaximumWorkers})");

        var queue = new Queue<HyperVTarget>(targetList);

        var syncLock = new object();

        var workers = Enumerable
            .Range(0, workerCount)
            .Select(_ => ProcessWorkerAsync())
            .ToArray();

        await Task.WhenAll(workers);

        result.TotalHosts =
            result.Targets.Sum(target => target.Hosts.Count);

        result.TotalVirtualMachines =
            result.Targets.Sum(
                target => target.VirtualMachines.Count);

        result.CompletedAt = DateTime.Now;

        _log.Info(
            "Orchestrator",
            $"Collection finished in {(DateTime.Now - result.StartedAt).TotalSeconds:F1}s: " +
            $"{result.SuccessfulTargets} succeeded, {result.FailedTargets} failed, " +
            $"{result.TotalHosts} host(s), {result.TotalVirtualMachines} VM(s)");

        return result;


        async Task ProcessWorkerAsync()
        {
            while (true)
            {
                HyperVTarget? target = null;

                lock (syncLock)
                {
                    if (queue.Count > 0)
                    {
                        target = queue.Dequeue();
                    }
                }

                if (target == null)
                {
                    return;
                }

                cancellationToken.ThrowIfCancellationRequested();

                await ProcessTargetAsync(target);
            }
        }


        async Task ProcessTargetAsync(
            HyperVTarget target)
        {
            _log.Step("Orchestrator", "Re-validating before collection", target.Name);

            var validation =
                await _targetValidator.ValidateAsync(
                    target,
                    cancellationToken);

            target.Validation = validation;

            if (!validation.CanCollect)
            {
                target.Status =
                    Core.Enums.ConnectionStatus.Failed;

                _log.Error("Orchestrator", $"Skipped: validation failed ({validation.Status})", target.Name);

                lock (syncLock)
                {
                    result.FailedTargets++;
                }

                ReportProgress(target);

                return;
            }

            try
            {
                target.Status =
                    Core.Enums.ConnectionStatus.Connecting;

                target.Validation.Status =
                    Core.Enums.TargetValidationStatus.Collecting;

                var collectedTarget =
    await _inventoryCollector.CollectAsync(
        target,
        request,
        progress,
        cancellationToken);

                _inventoryCache.UpdateTarget(
                    collectedTarget);

                target.Status =
                    Core.Enums.ConnectionStatus.Connected;

                target.Validation.Status =
                    Core.Enums.TargetValidationStatus.Completed;

                target.LastSuccessfulScan =
                    DateTime.Now;

                lock (syncLock)
                {
                    result.SuccessfulTargets++;
                }

                _log.Info("Orchestrator", "Collection succeeded", target.Name);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                target.Status =
                    Core.Enums.ConnectionStatus.Failed;

                target.Validation.Status =
                    Core.Enums.TargetValidationStatus.Failed;

                target.Validation.ErrorMessage =
                    ex.Message;

                _log.Error("Orchestrator", $"Collection failed: {ex.Message}", target.Name);

                lock (syncLock)
                {
                    result.FailedTargets++;
                }
            }

            ReportProgress(target);
        }


        void ReportProgress(HyperVTarget target)
        {
            if (progress == null)
            {
                return;
            }

            int completed;

            lock (syncLock)
            {
                completed =
                    result.SuccessfulTargets +
                    result.FailedTargets;
            }

            progress.Report(
                new CollectionProgress
                {
                    TotalTargets = targetList.Count,
                    CompletedTargets = completed,
                    FailedTargets = result.FailedTargets,
                    TotalHosts = result.TotalHosts,
                    TotalVirtualMachines =
                        result.TotalVirtualMachines,
                    CurrentTarget = target.Name,
                    CurrentStage =
                        target.Validation.Status.ToString()
                });
        }
    }
}