using System.Collections;
using System.Globalization;
using ClosedXML.Excel;
using HyperVToolsX.Core.Collection;
using HyperVToolsX.Core.Enums;
using HyperVToolsX.Core.Interfaces;
using HyperVToolsX.Core.Templates;

namespace HyperVToolsX.Export;

/// <summary>
/// Exports an <see cref="InventorySnapshot"/> to an .xlsx workbook: custom tab
/// templates first, then one worksheet per inventory tab. Columns come from the
/// shared <see cref="InventoryCatalog"/>, so new model fields are exported
/// without extra code. Size columns are written as numbers converted to the
/// requested unit (GB unless the caller passes another) so they stay sortable
/// and usable in formulas.
/// </summary>
public sealed class ExcelInventoryExporter : IInventoryExporter
{
    // Excel rejects cell text longer than this.
    private const int MaxCellLength = 32767;

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

    private static int Export(
        InventorySnapshot snapshot,
        string filePath,
        SizeUnit sizeUnit,
        IReadOnlyList<CustomTabTemplate> templates,
        CancellationToken ct)
    {
        using var workbook = new XLWorkbook();

        var sheetCount = 0;

        foreach (var table in ExportTables.Build(snapshot, templates, ct))
        {
            WriteSheet(workbook.Worksheets.Add(table.Name), table.Columns, table.Rows, sizeUnit, ct);
            sheetCount++;
        }

        var directory = Path.GetDirectoryName(Path.GetFullPath(filePath));

        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        workbook.SaveAs(filePath);

        return sheetCount;
    }

    private static void WriteSheet(
        IXLWorksheet sheet,
        IReadOnlyList<ExportColumn> columns,
        IEnumerable<object?[]> rows,
        SizeUnit sizeUnit,
        CancellationToken ct)
    {
        for (var c = 0; c < columns.Count; c++)
        {
            var title = columns[c].Header;

            sheet.Cell(1, c + 1).Value = columns[c].Field.IsSize ? $"{title} ({sizeUnit})" : title;
        }

        var row = 2;

        foreach (var values in rows)
        {
            ct.ThrowIfCancellationRequested();

            for (var c = 0; c < columns.Count; c++)
            {
                var field = columns[c].Field;
                var cell = sheet.Cell(row, c + 1);

                if (field.SizeSourceUnit is { } source)
                {
                    WriteSize(cell, values[c], source, sizeUnit);
                }
                else
                {
                    WriteCell(cell, values[c]);
                }
            }

            row++;
        }

        var header = sheet.Range(1, 1, 1, Math.Max(columns.Count, 1));
        header.Style.Font.Bold = true;
        header.Style.Font.FontColor = XLColor.White;
        header.Style.Fill.BackgroundColor = XLColor.FromHtml("#0078D4");

        sheet.SheetView.FreezeRows(1);

        if (row > 2)
        {
            sheet.Range(1, 1, row - 1, columns.Count).SetAutoFilter();
        }

        sheet.Columns().AdjustToContents(1, Math.Min(row, 200), 8, 60);
    }

    private static double Converted(object? value, SizeUnit source, SizeUnit target)
    {
        var converted = SizeUnitMath.Convert(
            Convert.ToDouble(value, CultureInfo.InvariantCulture), source, target);

        return target == SizeUnit.Bytes ? converted : Math.Round(converted, 2);
    }

    private static void WriteSize(IXLCell cell, object? value, SizeUnit source, SizeUnit target)
    {
        switch (value)
        {
            case null:
                return;

            case CollapsedValue collapsed:
                // Several matching rows: keep them as text, e.g. "60; 127".
                cell.Value = Truncate(string.Join(
                    ListSeparator,
                    collapsed.Items
                        .Where(i => i is not null)
                        .Select(i => Converted(i, source, target).ToString("0.##", CultureInfo.InvariantCulture))));
                break;

            default:
                cell.Value = Converted(value, source, target);
                cell.Style.NumberFormat.Format = target == SizeUnit.Bytes ? "#,##0" : "#,##0.00";
                break;
        }
    }

    private static void WriteCell(IXLCell cell, object? value)
    {
        switch (value)
        {
            case null:
                return;

            case string s:
                cell.Value = Truncate(s);
                break;

            case bool b:
                cell.Value = b;
                break;

            case DateTime dt:
                cell.Value = dt;
                cell.Style.DateFormat.Format = "yyyy-mm-dd hh:mm:ss";
                break;

            case DateTimeOffset dto:
                cell.Value = dto.LocalDateTime;
                cell.Style.DateFormat.Format = "yyyy-mm-dd hh:mm:ss";
                break;

            case TimeSpan ts:
                cell.Value = ts;
                cell.Style.NumberFormat.Format = "[h]:mm:ss";
                break;

            case Enum e:
                cell.Value = e.ToString();
                break;

            case byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal:
                cell.Value = Convert.ToDouble(value, CultureInfo.InvariantCulture);
                break;

            case CollapsedValue or IEnumerable:
                cell.Value = Truncate(CustomTabBuilder.JoinText(value, ScalarText));
                break;

            default:
                cell.Value = Truncate(ScalarText(value));
                break;
        }
    }

    private static string ScalarText(object? value) => value switch
    {
        null => string.Empty,
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? string.Empty
    };

    private static string Truncate(string s) =>
        s.Length <= MaxCellLength ? s : s[..MaxCellLength];
}
