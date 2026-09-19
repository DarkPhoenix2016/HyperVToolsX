namespace HyperVToolsX.Core.Models.Details;

public class OperatingSystemInfo
{
    public string ComputerName { get; set; } = string.Empty;

    public string Caption { get; set; } = string.Empty;

    public string Version { get; set; } = string.Empty;

    public string BuildNumber { get; set; } = string.Empty;

    public string OSArchitecture { get; set; } = string.Empty;

    public DateTime? LastBootUpTime { get; set; }

    public long TotalVisibleMemorySizeKb { get; set; }

    public long FreePhysicalMemoryKb { get; set; }
}