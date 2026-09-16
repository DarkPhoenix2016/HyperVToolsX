using HyperVToolsX.Core.Collection;
using HyperVToolsX.Core.Models;

namespace HyperVToolsX.Core.Interfaces;

public interface IInventoryCollector
{
    Task<HyperVTarget> CollectAsync(
        HyperVTarget target,
        CollectionRequest request,
        IProgress<CollectionProgress>? progress = null,
        CancellationToken cancellationToken = default);
}