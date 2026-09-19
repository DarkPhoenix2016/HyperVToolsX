using System.Text.Json;
using HyperVToolsX.Core.Models;
using HyperVToolsX.Core.Models.Details;

namespace HyperVToolsX.Infrastructure.Collection;

/// <summary>A node of a failover cluster as reported by Get-ClusterNode.</summary>
public sealed record ClusterNodeRef(string Name, string Fqdn, string State)
{
    /// <summary>Down and unknown nodes can't be queried, so they are skipped instead of waiting for a timeout.</summary>
    public bool IsReachable =>
        !State.Equals("Down", StringComparison.OrdinalIgnoreCase)
        && !State.Equals("Unknown", StringComparison.OrdinalIgnoreCase);

    /// <summary>The name to connect with: the FQDN when the cluster was addressed by one (Kerberos), else the node name.</summary>
    public string ConnectionName(string clusterTargetName) =>
        clusterTargetName.Contains('.') && !string.IsNullOrWhiteSpace(Fqdn) ? Fqdn : Name;
}

/// <summary>Cluster-wide steps of collecting a cluster: finding the nodes, and combining the per-node results.</summary>
public static class ClusterNodes
{
    /// <summary>Runs on the cluster (the cluster name resolves to whichever node owns it) and lists every node.</summary>
    public const string DiscoveryScript = """
        $ErrorActionPreference = 'Stop'
        Import-Module FailoverClusters -ErrorAction Stop

        $nodes = @(
            Get-ClusterNode | ForEach-Object {
                $fqdn = [string]$_.Name
                try { $fqdn = [System.Net.Dns]::GetHostEntry([string]$_.Name).HostName } catch { }

                [PSCustomObject]@{
                    Name  = [string]$_.Name
                    Fqdn  = $fqdn
                    State = [string]$_.State
                }
            }
        )

        [PSCustomObject]@{ Nodes = $nodes } | ConvertTo-Json -Depth 4 -Compress
        """;

    /// <summary>Reads the discovery output. Accepts a single node object as well as an array, and ignores duplicates.</summary>
    public static IReadOnlyList<ClusterNodeRef> ParseNodes(string json)
    {
        var start = json.IndexOf('{');
        var end = json.LastIndexOf('}');

        if (start < 0 || end < start)
        {
            throw new InvalidOperationException($"Cluster node discovery returned no JSON: {json.Trim()}");
        }

        using var document = JsonDocument.Parse(json[start..(end + 1)]);

        if (!document.RootElement.TryGetProperty("Nodes", out var nodes))
        {
            return [];
        }

        var elements = nodes.ValueKind == JsonValueKind.Array
            ? nodes.EnumerateArray().ToList()
            : nodes.ValueKind == JsonValueKind.Object ? [nodes] : [];

        var result = new List<ClusterNodeRef>();

        foreach (var element in elements)
        {
            var name = Read(element, "Name");

            if (string.IsNullOrWhiteSpace(name)
                || result.Any(n => n.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            result.Add(new ClusterNodeRef(name, Read(element, "Fqdn"), Read(element, "State")));
        }

        return result;
    }

    /// <summary>
    /// Replaces the contents of <paramref name="cluster"/> with the combined data of its nodes, in the order given.
    /// Each node appears once. Cluster-level rows are the same on every node, so only the first of each name is kept.
    /// </summary>
    public static void Merge(HyperVTarget cluster, IEnumerable<HyperVTarget> nodes)
    {
        cluster.Hosts.Clear();
        cluster.VirtualMachines.Clear();
        cluster.Processors.Clear();
        cluster.Memories.Clear();
        cluster.NetworkAdapters.Clear();
        cluster.NetworkVlans.Clear();
        cluster.Checkpoints.Clear();
        cluster.IntegrationServices.Clear();
        cluster.VmStorage.Clear();
        cluster.Disks.Clear();
        cluster.Vhds.Clear();
        cluster.Replication.Clear();
        cluster.Dvds.Clear();
        cluster.HostStorage.Clear();
        cluster.OperatingSystems.Clear();
        cluster.Clusters.Clear();

        foreach (var node in nodes)
        {
            foreach (var host in node.Hosts)
            {
                if (!cluster.Hosts.Any(h => h.Name.Equals(host.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    cluster.Hosts.Add(host);
                }
            }

            cluster.VirtualMachines.AddRange(node.VirtualMachines);
            cluster.Processors.AddRange(node.Processors);
            cluster.Memories.AddRange(node.Memories);
            cluster.NetworkAdapters.AddRange(node.NetworkAdapters);
            cluster.NetworkVlans.AddRange(node.NetworkVlans);
            cluster.Checkpoints.AddRange(node.Checkpoints);
            cluster.IntegrationServices.AddRange(node.IntegrationServices);
            cluster.VmStorage.AddRange(node.VmStorage);
            cluster.Disks.AddRange(node.Disks);
            cluster.Vhds.AddRange(node.Vhds);
            cluster.Replication.AddRange(node.Replication);
            cluster.Dvds.AddRange(node.Dvds);
            cluster.HostStorage.AddRange(node.HostStorage);
            cluster.OperatingSystems.AddRange(node.OperatingSystems);

            foreach (ClusterInfo info in node.Clusters)
            {
                if (!cluster.Clusters.Any(c => c.Name.Equals(info.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    cluster.Clusters.Add(info);
                }
            }
        }
    }

    private static string Read(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;
}
