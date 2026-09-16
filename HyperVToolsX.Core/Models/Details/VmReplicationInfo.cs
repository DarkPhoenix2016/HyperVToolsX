namespace HyperVToolsX.Core.Models.Details;

public class VmReplicationInfo
{
    public string VmName { get; set; } = string.Empty;

    public string VMId { get; set; } = string.Empty;

    public string ReplicationState { get; set; } = string.Empty;

    public string ReplicationHealth { get; set; } = string.Empty;

    public string ReplicationMode { get; set; } = string.Empty;

    public string PrimaryServer { get; set; } = string.Empty;

    public string ReplicaServer { get; set; } = string.Empty;

    public int ReplicaServerPort { get; set; }

    public string AuthenticationType { get; set; } = string.Empty;

    public string CertificateThumbprint { get; set; } = string.Empty;

    public bool CompressionEnabled { get; set; }

    public bool AutoResynchronizeEnabled { get; set; }

    public TimeSpan? AutoResynchronizeIntervalStart { get; set; }

    public TimeSpan? AutoResynchronizeIntervalEnd { get; set; }

    public int ReplicationIntervalSec { get; set; }

    public int FrequencySec { get; set; }

    public DateTime? LastReplicationTime { get; set; }

    public DateTime? CurrentReplicaTime { get; set; }

    public DateTime? LastSuccessfulReplicationTime { get; set; }

    public bool FailedOver { get; set; }

    public bool TestFailoverInProcess { get; set; }

    public DateTime? TestFailoverTime { get; set; }

    public string TestFailoverVMName { get; set; } = string.Empty;

    public List<string> ReplicationHealthDetails { get; set; } = [];

    public List<string> IncludedDisks { get; set; } = [];

    public List<string> ExcludedDisks { get; set; } = [];

    public int RecoveryHistory { get; set; }

    public int ApplicationConsistentSnapshotFrequencyInHours { get; set; }

    public string ExtendedReplicationState { get; set; } = string.Empty;

    public string ExtendedReplicaServer { get; set; } = string.Empty;

    public int? ExtendedReplicaServerPort { get; set; }

    public string ExtendedAuthenticationType { get; set; } = string.Empty;

    public string ExtendedCertificateThumbprint { get; set; } = string.Empty;
}