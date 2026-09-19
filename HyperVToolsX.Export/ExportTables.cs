using HyperVToolsX.Core.Collection;
using HyperVToolsX.Core.Templates;

namespace HyperVToolsX.Export;

internal sealed record ExportColumn(string Header, InventoryField Field);

internal sealed record ExportTable(
    string Name,
    IReadOnlyList<ExportColumn> Columns,
    IEnumerable<object?[]> Rows);

/// <summary>
/// The tables both exporters write, in order: custom tabs first, then one per
/// inventory tab. Keeping this in one place makes the .xlsx sheets and the
/// .csv files identical in content.
/// </summary>
internal static class ExportTables
{
    public static IEnumerable<ExportTable> Build(
        InventorySnapshot snapshot,
        IReadOnlyList<CustomTabTemplate> templates,
        CancellationToken ct)
    {
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var template in templates)
        {
            ct.ThrowIfCancellationRequested();

            if (InventoryCatalog.IsSourceName(template.Name) || !used.Add(template.Name))
            {
                continue;
            }

            var data = CustomTabBuilder.Build(snapshot, template);

            if (data.Columns.Count == 0)
            {
                used.Remove(template.Name);
                continue;
            }

            yield return new ExportTable(
                template.Name,
                data.Columns.Select(c => new ExportColumn(c.Header, c.Field)).ToList(),
                data.Rows.Select(r => r.Values));
        }

        foreach (var source in InventoryCatalog.Sources)
        {
            ct.ThrowIfCancellationRequested();

            var fields = source.Fields;

            yield return new ExportTable(
                source.Name,
                fields.Select(f => new ExportColumn(f.DefaultHeader, f)).ToList(),
                source.GetRows(snapshot)
                    .Cast<object>()
                    .Select(item => fields.Select(f => f.Property.GetValue(item)).ToArray()));
        }
    }
}
