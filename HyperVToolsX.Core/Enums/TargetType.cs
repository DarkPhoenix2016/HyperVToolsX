namespace HyperVToolsX.Core.Enums;

public enum TargetType
{
    StandaloneHost,
    Cluster,

    /// <summary>A standalone-addressed node that belongs to a failover cluster.</summary>
    ClusteredHost
}