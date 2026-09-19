using System.Collections;
using System.Globalization;
using System.Text;
using HyperVToolsX.Core.Collection;
using HyperVToolsX.Core.Enums;
using HyperVToolsX.Core.Interfaces;
using HyperVToolsX.Core.Templates;

namespace HyperVToolsX.Export;

/// <summary>
/// Exports the same tables as <see cref="ExcelInventoryExporter"/> as CSV. A CSV file holds one table, so
/// <c>report.csv</c> becomes <c>report-vInfo.csv</c>, <c>report-vCPU.csv</c> and so on (custom tabs first,
/// named after the tab). Tables with no rows are skipped. The returned count is the number of files written.
/// Files are UTF-8 with a BOM so Excel opens them correctly.
/// </summary>
public sealed class CsvInventoryExporter : IInventoryExporter
{
    private const string ListSeparator = "; ";

    public Task<int> ExportAsync(
        InventorySnapshot snapshot,
        string filePath,
        SizeUnit sizeUnit = SizeUnit.GB,
        IReadOnlyList<CustomTabTemplate>? templates = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        return Task.Run(
            () => Export(snapshot, filePath, sizeUnit, templates ?? [], cancellationToken),
            cancellationToken);
    }

    /// <summary>The file a table is written to, e.g. ("C:\r\report.csv", "vInfo") -> "C:\r\report-vInfo.csv".</summary>
    public static string PathFor(string filePath, string tableName)
    {
        var fullPath = Path.GetFullPath(filePath);
        var directory = Path.GetDirectoryName(fullPath) ?? string.Empty;
        var stem = Path.GetFileNameWithoutExtension(fullPath);

        return Path.Combine(directory, $"{stem}-{tableName}.csv");
    }

    private static int Export(
        InventorySnapshot snapshot,
        string filePath,
        SizeUnit sizeUnit,
        IReadOnlyList<CustomTabTemplate> templates,
        CancellationToken ct)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(filePath));

        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var written = 0;

        foreach (var table in ExportTables.Build(snapshot, templates, ct))
        {
            using var rows = table.Rows.GetEnumerator();

            if (!rows.MoveNext())
            {
                continue;
            }

            using var writer = new StreamWriter(
                PathFor(filePath, table.Name),
                append: false,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

            writer.Write(string.Join(",", table.Columns.Select(c => Quote(
                c.Field.IsSize ? $"{c.Header} ({sizeUnit})" : c.Header))));
            writer.Write("\r\n");

            do
            {
                ct.ThrowIfCancellationRequested();

                var values = rows.Current;

                writer.Write(string.Join(",", table.Columns.Select((c, i) => Quote(Text(values[i], c.Field, sizeUnit)))));
                writer.Write("\r\n");
            }
            while (rows.MoveNext());

            written++;
        }

        return written;
    }

    private static string Text(object? value, InventoryField field, SizeUnit target)
    {
        if (field.SizeSourceUnit is { } source)
        {
            return CustomTabBuilder.JoinText(value, item =>
            {
                var converted = SizeUnitMath.Convert(
                    Convert.ToDouble(item, CultureInfo.InvariantCulture), source, target);

                return converted.ToString(target == SizeUnit.Bytes ? "0" : "0.##", CultureInfo.InvariantCulture);
            });
        }

        return value switch
        {
            null => string.Empty,
            string s => Neutralize(s),
            CollapsedValue or IEnumerable => Neutralize(CustomTabBuilder.JoinText(value, Scalar)),
            _ => Scalar(value)
        };
    }

    private static string Scalar(object? value) => value switch
    {
        null => string.Empty,
        string s => s,
        DateTime dt => dt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
        DateTimeOffset dto => dto.LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
        TimeSpan ts => $"{(long)ts.TotalHours}:{ts:mm\\:ss}",
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? string.Empty
    };

    /// <summary>
    /// Text that starts with = + - or @ would be run as a formula when the file is opened in Excel
    /// (VM names and notes are free text), so it is prefixed with an apostrophe.
    /// </summary>
    private static string Neutralize(string text) =>
        text.Length > 0 && text[0] is '=' or '+' or '-' or '@' ? "'" + text : text;

    private static string Quote(string text) =>
        text.IndexOfAny([',', '"', '\r', '\n']) >= 0
            ? "\"" + text.Replace("\"", "\"\"") + "\""
            : text;
}
