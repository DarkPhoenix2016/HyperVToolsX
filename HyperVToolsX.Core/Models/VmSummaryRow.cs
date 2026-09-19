namespace HyperVToolsX.Core.Models;

public class VmSummaryRow
{
    public string VmName { get; set; } = string.Empty;

    public string HostName { get; set; } = string.Empty;

    public string ClusterName { get; set; } = string.Empty;

    public string IpAddress { get; set; } = string.Empty;

    public string State { get; set; } = string.Empty;

    public int ProcessorCount { get; set; }

    public long MemoryBytes { get; set; }

    public string MemoryDisplay =>
        FormatMemory(MemoryBytes);

    public string Generation { get; set; } = string.Empty;

    public string Version { get; set; } = string.Empty;

    public DateTime? BootTime { get; set; }

    public string? Uptime { get; set; }


    private static string FormatMemory(long bytes)
    {
        if (bytes <= 0)
        {
            return "0 MB";
        }

        var gb =
            bytes /
            1024.0 /
            1024.0 /
            1024.0;

        if (gb >= 1)
        {
            return $"{gb:0.##} GB";
        }

        var mb =
            bytes /
            1024.0 /
            1024.0;

        return $"{mb:0.##} MB";
    }
}