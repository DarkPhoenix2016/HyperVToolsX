namespace HyperVToolsX.Core.Enums;

public enum TargetValidationStatus
{
    Pending,

    ResolvingName,
    NameResolutionFailed,

    Pinging,
    PingFailed,

    Connecting,
    ConnectionFailed,

    NotHyperV,

    DiscoveringCluster,
    ClusterReady,

    Ready,

    Collecting,
    Completed,

    Failed
}