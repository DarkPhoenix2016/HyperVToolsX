using HyperVToolsX.Core.Collection;
using HyperVToolsX.Core.Interfaces;
using HyperVToolsX.Core.Models;

namespace HyperVToolsX.Infrastructure.Collection;

public class TargetManager : ITargetManager
{
    public IReadOnlyList<HyperVTarget> Normalize(
        IEnumerable<string> targetNames)
    {
        return TargetNormalizer.Normalize(targetNames);
    }

    public int CalculateWorkerCount(int targetCount)
    {
        return WorkerConfiguration.CalculateWorkerCount(
            targetCount);
    }
}