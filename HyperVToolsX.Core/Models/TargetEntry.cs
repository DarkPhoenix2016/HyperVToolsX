using HyperVToolsX.Core.Enums;

namespace HyperVToolsX.Core.Models;

public class TargetEntry
{
    public string Name { get; set; } = string.Empty;

    public TargetType Type { get; set; } = TargetType.StandaloneHost;

    public TargetValidationStatus ValidationStatus { get; set; }
        = TargetValidationStatus.Pending;

    public bool NameResolved { get; set; }

    public bool PingSucceeded { get; set; }

    public bool HyperVConnectionSucceeded { get; set; }

    public string Result { get; set; } = "Pending";

    public string ErrorMessage { get; set; } = string.Empty;
}