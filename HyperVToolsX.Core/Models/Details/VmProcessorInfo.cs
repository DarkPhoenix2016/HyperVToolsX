namespace HyperVToolsX.Core.Models.Details;

public class VmProcessorInfo
{
    public string VmName { get; set; } = string.Empty;

    public string HostName { get; set; } = string.Empty;

    public string StatusDescription { get; set; } = string.Empty;

    public List<string> OperationalStatus { get; set; } = [];

    public string ResourcePoolName { get; set; } = string.Empty;

    public int Count { get; set; }

    public bool CompatibilityForMigrationEnabled { get; set; }

    public string CompatibilityForMigrationMode { get; set; } =
        string.Empty;

    public bool CompatibilityForOlderOperatingSystemsEnabled { get; set; }

    public int HwThreadCountPerCore { get; set; }

    public bool ExposeVirtualizationExtensions { get; set; }

    public bool EnablePerfmonPmu { get; set; }

    public bool EnablePerfmonArchPmu { get; set; }

    public bool EnablePerfmonLbr { get; set; }

    public bool EnablePerfmonPebs { get; set; }

    public bool EnablePerfmonIpt { get; set; }

    public bool EnableLegacyApicMode { get; set; }

    public string ApicMode { get; set; } = string.Empty;

    public bool AllowACountMCount { get; set; }

    public string CpuBrandString { get; set; } = string.Empty;

    public int PerfCpuFreqCapMhz { get; set; }

    public int L3CacheWays { get; set; }

    public int PhysicalAddressWidth { get; set; }

    public int Maximum { get; set; }

    public int Reserve { get; set; }

    public int RelativeWeight { get; set; }

    public int MaximumCountPerNumaNode { get; set; }

    public int MaximumCountPerNumaSocket { get; set; }

    public bool EnableHostResourceProtection { get; set; }
}