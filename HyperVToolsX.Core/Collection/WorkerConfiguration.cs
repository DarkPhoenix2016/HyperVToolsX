namespace HyperVToolsX.Core.Collection;

public static class WorkerConfiguration
{
    public const int MinimumWorkers = 1;

    public const int MaximumWorkers = 10;

    public static int CalculateWorkerCount(int targetCount)
    {
        if (targetCount <= 0)
        {
            return MinimumWorkers;
        }

        return Math.Clamp(
            targetCount,
            MinimumWorkers,
            MaximumWorkers);
    }
}