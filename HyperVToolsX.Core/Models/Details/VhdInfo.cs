namespace HyperVToolsX.Core.Models.Details;

public class VhdInfo
{
    public string HostName { get; set; } = string.Empty;

    public string Path { get; set; } = string.Empty;

    public string VhdFormat { get; set; } = string.Empty;

    public string VhdType { get; set; } = string.Empty;

    public long FileSize { get; set; }

    public long Size { get; set; }

    public long MinimumSize { get; set; }

    public long LogicalSectorSize { get; set; }

    public long PhysicalSectorSize { get; set; }

    public long BlockSize { get; set; }

    public string ParentPath { get; set; } = string.Empty;

    public string DiskIdentifier { get; set; } = string.Empty;

    public string FragmentationPercentage { get; set; } =string.Empty;

    public int Alignment { get; set; }

    public bool Attached { get; set; }

    public string DiskNumber { get; set; } = string.Empty;

    public bool IsPMEMCompatible { get; set; }

    public string AddressAbstractionType { get; set; } =
        string.Empty;
}