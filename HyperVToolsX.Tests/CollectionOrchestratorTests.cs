using HyperVToolsX.Core.Collection;
using HyperVToolsX.Core.Enums;
using HyperVToolsX.Core.Interfaces;
using HyperVToolsX.Core.Models;
using HyperVToolsX.Infrastructure.Collection;
using Xunit;

namespace HyperVToolsX.Tests;

public class CollectionOrchestratorTests
{
    private sealed class CountingValidator : ITargetValidator
    {
        public int Calls;

        public Task<TargetValidationResult> ValidateAsync(HyperVTarget target, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref Calls);

            return Task.FromResult(new TargetValidationResult
            {
                TargetName = target.Name,
                Status = TargetValidationStatus.Ready,
                HyperVConnectionSucceeded = true,
                StartedAt = DateTime.Now,
                CompletedAt = DateTime.Now
            });
        }
    }

    private sealed class FakeCollector : IInventoryCollector
    {
        public Func<HyperVTarget, Task>? Behaviour;

        public async Task<HyperVTarget> CollectAsync(
            HyperVTarget target,
            CollectionRequest request,
            IProgress<CollectionProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            if (Behaviour is not null)
            {
                await Behaviour(target);
            }

            return target;
        }
    }

    private static (CollectionOrchestrator Orchestrator, CountingValidator Validator) Build(FakeCollector collector)
    {
        var validator = new CountingValidator();

        return (new CollectionOrchestrator(validator, collector, new InventoryCache()), validator);
    }

    [Fact]
    public async Task Recently_validated_target_is_not_validated_again()
    {
        var (orchestrator, validator) = Build(new FakeCollector());

        var target = new HyperVTarget
        {
            Name = "HV01",
            Validation = new TargetValidationResult
            {
                Status = TargetValidationStatus.Ready,
                HyperVConnectionSucceeded = true,
                StartedAt = DateTime.Now,
                CompletedAt = DateTime.Now
            }
        };

        var result = await orchestrator.CollectAsync([target], new CollectionRequest());

        Assert.Equal(0, validator.Calls);
        Assert.Equal(1, result.SuccessfulTargets);
    }

    [Fact]
    public async Task Stale_or_unvalidated_targets_are_validated()
    {
        var (orchestrator, validator) = Build(new FakeCollector());

        var stale = new HyperVTarget
        {
            Name = "HV01",
            Validation = new TargetValidationResult
            {
                HyperVConnectionSucceeded = true,
                CompletedAt = DateTime.Now - CollectionOrchestrator.ValidationReuseWindow - TimeSpan.FromMinutes(1)
            }
        };

        await orchestrator.CollectAsync([stale, new HyperVTarget { Name = "HV02" }], new CollectionRequest());

        Assert.Equal(2, validator.Calls);
    }

    [Fact]
    public async Task One_failing_host_does_not_stop_the_others()
    {
        var collector = new FakeCollector
        {
            Behaviour = target => target.Name == "BAD"
                ? throw new TimeoutException("hung")
                : Task.CompletedTask
        };

        var (orchestrator, _) = Build(collector);

        var result = await orchestrator.CollectAsync(
            [new HyperVTarget { Name = "A" }, new HyperVTarget { Name = "BAD" }, new HyperVTarget { Name = "C" }],
            new CollectionRequest());

        Assert.Equal(2, result.SuccessfulTargets);
        Assert.Equal(1, result.FailedTargets);
    }
}
