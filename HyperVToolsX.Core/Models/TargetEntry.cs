using HyperVToolsX.Core.Enums;

namespace HyperVToolsX.Core.Models;

public class TargetEntry
{
    public string Name { get; set; } = string.Empty;

    public TargetType Type { get; set; } =
        TargetType.StandaloneHost;

    /// <summary>Grid-friendly label for <see cref="Type"/>.</summary>
    public string TypeDisplay =>
        Type switch
        {
            TargetType.Cluster => "Cluster",
            TargetType.ClusteredHost => "Clustered Host",
            _ => "Standalone Host"
        };

    public TargetValidationStatus ValidationStatus { get; set; } =
        TargetValidationStatus.Pending;

    public bool NameResolved { get; set; }

    public string ResolvedAddress { get; set; } =
        string.Empty;

    public bool PingSucceeded { get; set; }

    public bool HyperVConnectionSucceeded { get; set; }

    public bool IsCluster { get; set; }

    public string ClusterName { get; set; } =
        string.Empty;

    public string Result { get; set; } =
        "Pending";

    public string ErrorMessage { get; set; } =
        string.Empty;
}