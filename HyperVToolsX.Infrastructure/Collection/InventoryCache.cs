using HyperVToolsX.Core.Collection;
using HyperVToolsX.Core.Interfaces;
using HyperVToolsX.Core.Models;

namespace HyperVToolsX.Infrastructure.Collection;

public class InventoryCache : IInventoryCache
{
    private readonly object _lock = new();
    private readonly Dictionary<string, HyperVTarget> _targets =
        new(StringComparer.OrdinalIgnoreCase);

    public InventorySnapshot GetSnapshot()
    {
        lock (_lock)
        {
            var targets = _targets.Values.ToList();

            return new InventorySnapshot
            {
                Targets = targets,
                Hosts = targets
                    .SelectMany(target => target.Hosts)
                    .ToList(),
                VirtualMachines = targets
                    .SelectMany(target => target.VirtualMachines)
                    .ToList(),
                NetworkAdapters = targets
                    .SelectMany(target => target.NetworkAdapters)
                    .ToList(),
                Processors = targets
                    .SelectMany(target => target.Processors)
                    .ToList(),
                Memories = targets
                    .SelectMany(target => target.Memories)
                    .ToList(),
                Disks = targets
                    .SelectMany(target => target.Disks)
                    .ToList(),
                Vhds = targets
                    .SelectMany(target => target.Vhds)
                    .ToList(),
                NetworkVlans = targets
                    .SelectMany(target => target.NetworkVlans)
                    .ToList(),
                Checkpoints = targets
                    .SelectMany(target => target.Checkpoints)
                    .ToList(),
                IntegrationServices = targets
                    .SelectMany(target => target.IntegrationServices)
                    .ToList(),
                VmStorage = targets
                    .SelectMany(target => target.VmStorage)
                    .ToList(),
                Replication = targets
                    .SelectMany(target => target.Replication)
                    .ToList(),
                Dvds = targets
                    .SelectMany(target => target.Dvds)
                    .ToList(),
                HostStorage = targets
                    .SelectMany(target => target.HostStorage)
                    .ToList(),
                OperatingSystems = targets
                    .SelectMany(target => target.OperatingSystems)
                    .ToList(),
                Clusters = targets
                    .SelectMany(target => target.Clusters)
                    .ToList(),
                CreatedAt = DateTime.Now
            };
        }
    }

    public void UpdateTarget(HyperVTarget target)
    {
        if (string.IsNullOrWhiteSpace(target.Name))
        {
            return;
        }

        lock (_lock)
        {
            _targets[target.Name] = target;
        }
    }

    public void RemoveTarget(string targetName)
    {
        if (string.IsNullOrWhiteSpace(targetName))
        {
            return;
        }

        lock (_lock)
        {
            _targets.Remove(targetName);
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _targets.Clear();
        }
    }

    public bool ContainsTarget(string targetName)
    {
        if (string.IsNullOrWhiteSpace(targetName))
        {
            return false;
        }

        lock (_lock)
        {
            return _targets.ContainsKey(targetName);
        }
    }
}