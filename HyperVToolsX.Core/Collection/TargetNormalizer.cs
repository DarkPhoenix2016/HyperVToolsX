using HyperVToolsX.Core.Models;

namespace HyperVToolsX.Core.Collection;

public static class TargetNormalizer
{
    public static IReadOnlyList<HyperVTarget> Normalize(
        IEnumerable<string> targetNames)
    {
        return targetNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(name => new HyperVTarget
            {
                Name = name,
                Address = name
            })
            .ToList();
    }
}