using System.Collections;
using System.Reflection;
using System.Text.RegularExpressions;
using HyperVToolsX.Core.Collection;
using HyperVToolsX.Core.Enums;
using HyperVToolsX.Core.Models;
using HyperVToolsX.Core.Models.Details;

namespace HyperVToolsX.Core.Templates;

/// <summary>A data source (one inventory tab) that a custom tab can be built from.</summary>
public sealed record InventorySource(
    string Name,
    Type RowType,
    Func<InventorySnapshot, IEnumerable> GetRows)
{
    public IReadOnlyList<InventoryField> Fields => InventoryCatalog.GetFields(this);
}

/// <summary>A selectable column of an <see cref="InventorySource"/>.</summary>
public sealed record InventoryField(
    string Name,
    string DefaultHeader,
    PropertyInfo Property,
    SizeUnit? SizeSourceUnit,
    bool IsList)
{
    public bool IsSize => SizeSourceUnit is not null;
}

/// <summary>
/// The single list of inventory tabs and their fields. Used by the custom-tab
/// editor, the grid builder and the Excel export so they always agree.
/// </summary>
public static partial class InventoryCatalog
{
    public static IReadOnlyList<InventorySource> Sources { get; } =
    [
        new("vInfo", typeof(HyperVVirtualMachine), s => s.VirtualMachines),
        new("vCPU", typeof(VmProcessorInfo), s => s.Processors),
        new("vMemory", typeof(VmMemoryInfo), s => s.Memories),
        new("vNetwork", typeof(VmNetworkAdapter), s => s.NetworkAdapters),
        new("vVLAN", typeof(VmNetworkVlanInfo), s => s.NetworkVlans),
        new("vCheckpoint", typeof(VmCheckpointInfo), s => s.Checkpoints),
        new("vIntegration", typeof(VmIntegrationServiceInfo), s => s.IntegrationServices),
        new("vStorage", typeof(VmStorageInfo), s => s.VmStorage),
        new("vDisk", typeof(VmDiskInfo), s => s.Disks),
        new("vVHD", typeof(VhdInfo), s => s.Vhds),
        new("vReplication", typeof(VmReplicationInfo), s => s.Replication),
        new("vDVD", typeof(VmDvdInfo), s => s.Dvds),
        new("vCluster", typeof(ClusterInfo), s => s.Clusters),
        new("vHost", typeof(HostInventoryRow), HostInventoryBuilder.Build),
        new("vHostStorage", typeof(HostStorageInfo), s => s.HostStorage),
        new("vOS", typeof(OperatingSystemInfo), s => s.OperatingSystems),
    ];

    /// <summary>
    /// Size properties (the set the UI formats with its byte-size converter)
    /// mapped to the unit the model stores them in.
    /// </summary>
    private static readonly Dictionary<string, SizeUnit> SizeProperties = new()
    {
        ["MemoryAssigned"] = SizeUnit.Bytes,
        ["MemoryDemand"] = SizeUnit.Bytes,
        ["MemoryMaximum"] = SizeUnit.Bytes,
        ["MemoryMinimum"] = SizeUnit.Bytes,
        ["MemoryStartup"] = SizeUnit.Bytes,
        ["Maximum"] = SizeUnit.Bytes,
        ["Minimum"] = SizeUnit.Bytes,
        ["Startup"] = SizeUnit.Bytes,
        ["MaximumPerNumaNode"] = SizeUnit.Bytes,
        ["FileSize"] = SizeUnit.Bytes,
        ["Size"] = SizeUnit.Bytes,
        ["MinimumSize"] = SizeUnit.Bytes,
        ["BlockSize"] = SizeUnit.Bytes,
        ["LogicalSectorSize"] = SizeUnit.Bytes,
        ["PhysicalSectorSize"] = SizeUnit.Bytes,
        ["SizeOfSystemFiles"] = SizeUnit.Bytes,
        ["TotalMemoryBytes"] = SizeUnit.Bytes,
        ["UsedMemoryBytes"] = SizeUnit.Bytes,
        ["TotalVisibleMemorySizeKb"] = SizeUnit.KB,
        ["FreePhysicalMemoryKb"] = SizeUnit.KB,
        ["SharedVolumeBlockCacheSizeInMB"] = SizeUnit.MB,
    };

    private static readonly Dictionary<string, IReadOnlyList<InventoryField>> FieldCache =
        new(StringComparer.OrdinalIgnoreCase);

    public static InventorySource? Find(string? name) =>
        Sources.FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));

    public static bool IsSourceName(string name) => Find(name) is not null;

    /// <summary>The row source that means "one row per virtual machine".</summary>
    public const string VmRowSource = "vInfo";

    /// <summary>Row sources a custom tab can be built on. Only the VM source can combine other tabs.</summary>
    public static IReadOnlyList<string> RowSources { get; } = [VmRowSource, "vHost", "vCluster", "vHostStorage", "vOS"];

    /// <summary>Tabs whose fields can be looked up for a VM: every per-VM tab plus its host and cluster.</summary>
    public static IReadOnlyList<string> VmJoinSources { get; } =
    [
        "vInfo", "vCPU", "vMemory", "vNetwork", "vVLAN", "vCheckpoint", "vIntegration",
        "vStorage", "vDisk", "vVHD", "vReplication", "vDVD", "vHost", "vCluster"
    ];

    public static bool IsVmRowSource(string? rowSource) =>
        string.Equals(rowSource, VmRowSource, StringComparison.OrdinalIgnoreCase);

    /// <summary>Sources whose fields may appear as columns of a tab with the given row source.</summary>
    public static IReadOnlyList<string> AllowedSources(string rowSource) =>
        IsVmRowSource(rowSource) ? VmJoinSources : [rowSource];

    /// <summary>
    /// Header for a newly added column. Columns from a tab other than the row
    /// source get the tab name as prefix, e.g. "vNetwork Mac Address".
    /// </summary>
    public static string DefaultColumnHeader(string rowSource, string source, InventoryField field) =>
        string.Equals(rowSource, source, StringComparison.OrdinalIgnoreCase)
            ? field.DefaultHeader
            : $"{source} {field.DefaultHeader}";

    internal static IReadOnlyList<InventoryField> GetFields(InventorySource source)
    {
        lock (FieldCache)
        {
            if (FieldCache.TryGetValue(source.Name, out var cached))
            {
                return cached;
            }

            var fields = source.RowType
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && p.GetIndexParameters().Length == 0)
                .Select(p => new InventoryField(
                    p.Name,
                    Humanize(p.Name),
                    p,
                    p.PropertyType == typeof(long) && SizeProperties.TryGetValue(p.Name, out var unit) ? unit : null,
                    p.PropertyType != typeof(string) && typeof(IEnumerable).IsAssignableFrom(p.PropertyType)))
                .ToList();

            FieldCache[source.Name] = fields;
            return fields;
        }
    }

    /// <summary>"VMId" -> "VM Id", "MemoryAssigned" -> "Memory Assigned", "IPv4Addresses" -> "IPv4 Addresses".</summary>
    public static string Humanize(string name) =>
        HumanizeRegex().Replace(name, " ").Replace("I Pv", "IPv", StringComparison.Ordinal);

    [GeneratedRegex(@"(?<=[a-z0-9])(?=[A-Z])|(?<=[A-Z])(?=[A-Z][a-z])")]
    private static partial Regex HumanizeRegex();
}
