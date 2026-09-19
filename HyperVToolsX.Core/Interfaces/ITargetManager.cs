using HyperVToolsX.Core.Models;

namespace HyperVToolsX.Core.Interfaces;

public interface ITargetManager
{
    IReadOnlyList<HyperVTarget> Normalize(
        IEnumerable<string> targetNames);

    int CalculateWorkerCount(int targetCount);
}