using HyperVToolsX.Infrastructure.Collection;
using Xunit;

namespace HyperVToolsX.Tests;

public class TargetManagerTests
{
    [Fact]
    public void DuplicateTargets_ShouldBeRemoved()
    {
        var manager = new TargetManager();

        var targets = manager.Normalize(
        [
            "HYPER01",
            "HYPER02",
            "HYPER01",
            "hyper02",
            " HYPER03 "
        ]);

        Assert.Equal(3, targets.Count);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(3, 3)]
    [InlineData(9, 9)]
    [InlineData(10, 10)]
    [InlineData(11, 10)]
    [InlineData(100, 10)]
    [InlineData(1000, 10)]
    public void WorkerCount_ShouldBeLimitedToTen(
        int targetCount,
        int expectedWorkers)
    {
        var manager = new TargetManager();

        var workers =
            manager.CalculateWorkerCount(targetCount);

        Assert.Equal(expectedWorkers, workers);
    }
}