using HyperVToolsX.Core.Collection;
using HyperVToolsX.Core.Models;

namespace HyperVToolsX.Core.Interfaces;

public interface IInventoryCache
{
    InventorySnapshot GetSnapshot();

    void UpdateTarget(HyperVTarget target);

    void RemoveTarget(string targetName);

    void Clear();

    bool ContainsTarget(string targetName);
}