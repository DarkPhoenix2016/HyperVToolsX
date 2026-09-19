using System.Collections;
using System.Reflection;
using HyperVToolsX.Core.Collection;
using HyperVToolsX.Core.Enums;
using HyperVToolsX.Core.Models;
using HyperVToolsX.Core.Models.Details;

namespace HyperVToolsX.Core.Templates;

/// <summary>Converts between size units (all factors are powers of 1024).</summary>
public static class SizeUnitMath
{
    public static double Factor(SizeUnit unit) => unit switch
    {
        SizeUnit.KB => 1024d,
        SizeUnit.MB => Math.Pow(1024d, 2),
        SizeUnit.GB => Math.Pow(1024d, 3),
        SizeUnit.TB => Math.Pow(1024d, 4),
        SizeUnit.PB => Math.Pow(1024d, 5),
        _ => 1d
    };

    public static double ToBytes(double value, SizeUnit source) => value * Factor(source);

    public static double Convert(double value, SizeUnit source, SizeUnit target) =>
        value * Factor(source) / Factor(target);
}

/// <summary>
/// A cell that matched several rows of a one-to-many source (e.g. a VM with
/// two network adapters). Shown as "a; b" in the grid and in Excel.
/// </summary>
public sealed record CollapsedValue(IReadOnlyList<object?> Items);

/// <summary>One row of a custom tab: display values plus comparable sort keys, indexed by column.</summary>
public sealed class CompositeRow
{
    public CompositeRow(object?[] values, object?[] sortKeys)
    {
        Values = values;
        SortKeys = sortKeys;
    }

    public object?[] Values { get; }

    /// <summary>Always a single comparable value (or null), so sorting never mixes types.</summary>
    public object?[] SortKeys { get; }
}

public sealed record ResolvedColumn(string Header, InventorySource Source, InventoryField Field);

public sealed class CustomTabData
{
    public required IReadOnlyList<ResolvedColumn> Columns { get; init; }

    public required IReadOnlyList<CompositeRow> Rows { get; init; }
}

/// <summary>
/// Turns a <see cref="CustomTabTemplate"/> into rows. For the virtual-machine
/// row source there is exactly one row per VM; columns taken from other tabs
/// are looked up for that VM, and when a tab has several rows for the VM they
/// are collapsed into one <see cref="CollapsedValue"/> cell.
/// </summary>
public static class CustomTabBuilder
{
    /// <summary>Columns that still exist in the catalog and are allowed for the template's row source.</summary>
    public static IReadOnlyList<ResolvedColumn> ResolveColumns(CustomTabTemplate template)
    {
        var rowSource = InventoryCatalog.Find(template.Source);

        if (rowSource is null)
        {
            return [];
        }

        var allowed = InventoryCatalog.AllowedSources(rowSource.Name);
        var columns = new List<ResolvedColumn>();
        var seen = new HashSet<(string, string)>();

        foreach (var column in template.Columns)
        {
            var sourceName = string.IsNullOrWhiteSpace(column.Source) ? rowSource.Name : column.Source;
            var source = InventoryCatalog.Find(sourceName);

            if (source is null || !allowed.Contains(source.Name, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            var field = source.Fields.FirstOrDefault(f => f.Name == column.Field);

            if (field is null || !seen.Add((source.Name, field.Name)))
            {
                continue;
            }

            var header = string.IsNullOrWhiteSpace(column.Header)
                ? InventoryCatalog.DefaultColumnHeader(rowSource.Name, source.Name, field)
                : column.Header.Trim();

            columns.Add(new ResolvedColumn(header, source, field));
        }

        return columns;
    }

    public static CustomTabData Build(InventorySnapshot snapshot, CustomTabTemplate template)
    {
        var columns = ResolveColumns(template);
        var rowSource = InventoryCatalog.Find(template.Source);
        var rows = new List<CompositeRow>();

        if (rowSource is null || columns.Count == 0)
        {
            return new CustomTabData { Columns = columns, Rows = rows };
        }

        if (!InventoryCatalog.IsVmRowSource(rowSource.Name))
        {
            foreach (var item in rowSource.GetRows(snapshot))
            {
                var values = columns.Select(c => c.Field.Property.GetValue(item)).ToArray();
                rows.Add(new CompositeRow(values, values.Select(SortKey).ToArray()));
            }

            return new CustomTabData { Columns = columns, Rows = rows };
        }

        var matchers = new Dictionary<string, Func<HyperVVirtualMachine, IReadOnlyList<object>>>(StringComparer.OrdinalIgnoreCase);
        var context = new JoinContext(snapshot);

        foreach (var name in columns.Select(c => c.Source.Name).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            matchers[name] = context.MatcherFor(name);
        }

        foreach (var vm in snapshot.VirtualMachines)
        {
            var matched = new Dictionary<string, IReadOnlyList<object>>(StringComparer.OrdinalIgnoreCase);
            var values = new object?[columns.Count];

            for (var i = 0; i < columns.Count; i++)
            {
                var column = columns[i];

                if (!matched.TryGetValue(column.Source.Name, out var sourceRows))
                {
                    sourceRows = matchers[column.Source.Name](vm);
                    matched[column.Source.Name] = sourceRows;
                }

                values[i] = sourceRows.Count switch
                {
                    0 => null,
                    1 => column.Field.Property.GetValue(sourceRows[0]),
                    _ => new CollapsedValue(sourceRows.Select(r => column.Field.Property.GetValue(r)).ToList())
                };
            }

            rows.Add(new CompositeRow(values, values.Select(SortKey).ToArray()));
        }

        return new CustomTabData { Columns = columns, Rows = rows };
    }

    /// <summary>Text for a value that may be a list or a collapsed set, using <paramref name="scalar"/> for single values.</summary>
    public static string JoinText(object? value, Func<object?, string> scalar)
    {
        switch (value)
        {
            case null:
                return string.Empty;

            case CollapsedValue collapsed:
                return string.Join("; ", collapsed.Items.Select(i => JoinText(i, scalar)));

            case string s:
                return s;

            case IEnumerable list:
                return string.Join("; ", list.Cast<object?>().Select(scalar));

            default:
                return scalar(value);
        }
    }

    private static object? SortKey(object? value)
    {
        if (value is CollapsedValue collapsed)
        {
            value = collapsed.Items.FirstOrDefault();
        }

        return value switch
        {
            null => null,
            string => value,
            IEnumerable list => string.Join("; ", list.Cast<object?>().Select(x => x?.ToString())),
            IComparable => value,
            _ => value.ToString()
        };
    }

    // ---------------------------------------------------------

    /// <summary>Builds the per-source lookups that find the rows belonging to a VM.</summary>
    private sealed class JoinContext
    {
        private readonly InventorySnapshot _snapshot;
        private List<HostInventoryRow>? _hosts;
        private Func<HyperVVirtualMachine, IReadOnlyList<object>>? _disks;

        public JoinContext(InventorySnapshot snapshot) => _snapshot = snapshot;

        public Func<HyperVVirtualMachine, IReadOnlyList<object>> MatcherFor(string sourceName)
        {
            switch (sourceName.ToLowerInvariant())
            {
                case "vinfo":
                    return vm => [vm];

                case "vhost":
                    _hosts ??= HostInventoryBuilder.Build(_snapshot);
                    var hosts = _hosts;

                    return vm => hosts
                        .Where(h => Same(h.HostName, vm.HostName) || Same(h.Fqdn, vm.HostName))
                        .Cast<object>()
                        .Take(1)
                        .ToList();

                case "vcluster":
                    return vm => string.IsNullOrEmpty(vm.ClusterName)
                        ? []
                        : _snapshot.Clusters.Where(c => Same(c.Name, vm.ClusterName)).Cast<object>().Take(1).ToList();

                case "vvhd":
                    return VhdMatcher();

                default:
                    var source = InventoryCatalog.Find(sourceName)
                        ?? throw new ArgumentException($"Unknown source '{sourceName}'.", nameof(sourceName));

                    return VmLink.Create(source, source.GetRows(_snapshot)).Match;
            }
        }

        /// <summary>VHD rows have no VM name; they belong to a VM through its disks' paths.</summary>
        private Func<HyperVVirtualMachine, IReadOnlyList<object>> VhdMatcher()
        {
            _disks ??= VmLink.Create(InventoryCatalog.Find("vDisk")!, _snapshot.Disks).Match;
            var disks = _disks;

            var byHostAndPath = _snapshot.Vhds
                .ToLookup(v => Key(v.HostName, v.Path), StringComparer.OrdinalIgnoreCase);

            return vm => disks(vm)
                .Cast<VmDiskInfo>()
                .SelectMany(d => byHostAndPath[Key(d.HostName, d.Path)])
                .Distinct()
                .Cast<object>()
                .ToList();
        }

        private static string Key(string host, string path) => $"{host}|{path}";
    }

    private sealed class VmLink
    {
        private readonly ILookup<string, object> _byName;
        private readonly PropertyInfo? _host;
        private readonly PropertyInfo? _id;

        private VmLink(ILookup<string, object> byName, PropertyInfo? host, PropertyInfo? id)
        {
            _byName = byName;
            _host = host;
            _id = id;
        }

        public static VmLink Create(InventorySource source, IEnumerable rows)
        {
            var type = source.RowType;
            var name = type.GetProperty("VmName");

            var byName = name is null
                ? Enumerable.Empty<object>().ToLookup(_ => string.Empty)
                : rows.Cast<object>().ToLookup(
                    r => (string?)name.GetValue(r) ?? string.Empty,
                    StringComparer.OrdinalIgnoreCase);

            return new VmLink(byName, type.GetProperty("HostName"), type.GetProperty("VMId"));
        }

        /// <summary>
        /// A row belongs to the VM when the VM id matches; without a usable id it
        /// falls back to the VM name, narrowed by host when both sides have one.
        /// </summary>
        public IReadOnlyList<object> Match(HyperVVirtualMachine vm)
        {
            var result = new List<object>();

            foreach (var row in _byName[vm.Name])
            {
                var rowId = _id?.GetValue(row) as string;

                if (Guid.TryParse(rowId, out var id) && vm.VMId != Guid.Empty)
                {
                    if (id == vm.VMId)
                    {
                        result.Add(row);
                    }

                    continue;
                }

                var rowHost = _host?.GetValue(row) as string;

                if (string.IsNullOrEmpty(rowHost) || string.IsNullOrEmpty(vm.HostName) || Same(rowHost, vm.HostName))
                {
                    result.Add(row);
                }
            }

            return result;
        }
    }

    private static bool Same(string? a, string? b) =>
        !string.IsNullOrEmpty(a) && string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
}
