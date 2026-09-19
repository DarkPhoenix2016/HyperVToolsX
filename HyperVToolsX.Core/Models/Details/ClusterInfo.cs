namespace HyperVToolsX.Core.Models.Details;

public class ClusterInfo
{
    public string Name { get; set; } = string.Empty;

    public string Domain { get; set; } = string.Empty;

    public string Id { get; set; } = string.Empty;

    public string SharedVolumesRoot { get; set; } = string.Empty;

    public int AddEvictDelay { get; set; }

    public int BackupInProgress { get; set; }

    public int BlockCacheSize { get; set; }

    public int ClusSvcDataPartitionMounted { get; set; }

    public int ClusterEnforcedAntiAffinity { get; set; }

    public int ClusterFunctionalLevel { get; set; }

    public int ClusterGroupWaitDelay { get; set; }

    public int ClusterLogLevel { get; set; }

    public int ClusterLogSize { get; set; }

    public int CsvBalancedValidationThresholdInHours { get; set; }

    public int CsvDirectIoOpt { get; set; }

    public int CsvFltValidationThresholdInHours { get; set; }

    public int CustomDeadlockDetectionTimeout { get; set; }

    public int DatabaseReadWriteMode { get; set; }

    public int DefaultNetworkRole { get; set; }

    public string Description { get; set; } = string.Empty;

    public int DrainOnShutdown { get; set; }

    public long DumpPolicy { get; set; }

    public int DynamicQuorum { get; set; }

    public int EnableAutomaticMetric { get; set; }

    public int AutoAssignNodeSite { get; set; }

    public int AutoBalancerMode { get; set; }

    public int AutoBalancerLevel { get; set; }

    public int FixQuorum { get; set; }

    public int GracePeriodOnUnbalanced { get; set; }

    public int GroupAdministrativeDelay { get; set; }

    public int HangRecoveryAction { get; set; }

    public int IgnorePersistentStateOnStartup { get; set; }

    public int LogResourceControls { get; set; }

    public int LowerQuorumPriorityNodeId { get; set; }

    public int MaxNumberOfNodes { get; set; }

    public int MessageBufferLength { get; set; }

    public int MinimumNeverPreemptPriority { get; set; }

    public int MinimumPreemptorPriority { get; set; }

    public int NetftIPSecEnabled { get; set; }

    public int PlacementOptions { get; set; }

    public int PreventQuorum { get; set; }

    public int QuorumArbitrationTimeMax { get; set; }

    public int QuorumLogFileSize { get; set; }

    public int RequestReplyTimeout { get; set; }

    public int ResiliencyDefaultPeriod { get; set; }

    public int ResiliencyPeriodFilter { get; set; }

    public int ResourceDllDeadlockTimeout { get; set; }

    public long RootMemoryReserved { get; set; }

    public int RouteHistoryLength { get; set; }

    public string S2DCacheBehavior { get; set; } = string.Empty;

    public int S2DCacheFlashReservePercent { get; set; }

    public int S2DCachePageSizeKBytes { get; set; }

    public int S2DEnabled { get; set; }

    public int S2DIOLatencyThreshold { get; set; }

    public int S2DOptimizeFlashPoolThresholdPct { get; set; }

    public int SameSubnetDelay { get; set; }

    public int SameSubnetThreshold { get; set; }

    public int SharedVolumeBlockCacheSizeInMB { get; set; }

    public List<string> SharedVolumeCompatibleFilters { get; set; } = [];

    public string SharedVolumeSecurityDescriptor { get; set; } =
        string.Empty;

    public int ShutdownTimeoutInMinutes { get; set; }

    public int UseClientAccessNetworksForSharedVolumes { get; set; }

    public int WitnessDatabaseWriteTimeout { get; set; }

    public int WitnessDynamicWeight { get; set; }

    public int WitnessRestartInterval { get; set; }

    public int CrossSiteDelay { get; set; }

    public int CrossSiteThreshold { get; set; }

    public int CrossSubnetDelay { get; set; }

    public int CrossSubnetThreshold { get; set; }

    public int PlumbAllCrossSubnetRoutes { get; set; }

    public string PreferredSite { get; set; } = string.Empty;

    public string QuorumType { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;
}