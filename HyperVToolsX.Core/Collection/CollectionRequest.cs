namespace HyperVToolsX.Core.Collection;

public class CollectionRequest
{
    public int MaxConcurrentTargets { get; set; } = 10;
    public bool CollectCpu { get; set; } = true;
    public bool CollectMemory { get; set; } = true;
    public bool CollectStorage { get; set; } = true;
    public bool CollectNetwork { get; set; } = true;
    public bool CollectCheckpoints { get; set; } = true;
    public bool CollectIntegrationServices { get; set; } = true;
}