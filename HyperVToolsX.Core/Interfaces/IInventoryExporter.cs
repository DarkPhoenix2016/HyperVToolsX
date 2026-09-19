using HyperVToolsX.Core.Collection;
using HyperVToolsX.Core.Enums;
using HyperVToolsX.Core.Templates;

namespace HyperVToolsX.Core.Interfaces;

public interface IInventoryExporter
{
    /// <summary>
    /// Writes the snapshot to <paramref name="filePath"/> and returns the number of sheets written.
    /// Size columns are converted to <paramref name="sizeUnit"/> (GB by default).
    /// Custom tab <paramref name="templates"/> are written as the first sheets.
    /// </summary>
    Task<int> ExportAsync(
        InventorySnapshot snapshot,
        string filePath,
        SizeUnit sizeUnit = SizeUnit.GB,
        IReadOnlyList<CustomTabTemplate>? templates = null,
        CancellationToken cancellationToken = default);
}
