using HyperVToolsX.Core.Collection;
using HyperVToolsX.Core.Models;
using HyperVToolsX.Core.Models.Details;
using HyperVToolsX.Infrastructure.Collection;
using Xunit;

namespace HyperVToolsX.Tests;

public class ClusterCollectionTests
{
    private static HyperVTarget Node(string name, bool owner, params string[] vms)
    {
        var target = new HyperVTarget { Name = name };

        target.Hosts.Add(new HyperVHost { Name = name, IsClusterNode = true, IsClusterOwner = owner, ClusterName = "CL1" });
        target.HostStorage.Add(new HostStorageInfo { ComputerName = name });
        target.Clusters.Add(new ClusterInfo { Name = "CL1" });

        foreach (var vm in vms)
        {
            target.VirtualMachines.Add(new HyperVVirtualMachine { Name = vm, HostName = name });
        }

        return target;
    }

    [Fact]
    public void ParseNodes_ReadsArrayOrSingleObject_SkipsDuplicates_AndFlagsDownNodes()
    {
        var many = ClusterNodes.ParseNodes(
            """{"Nodes":[{"Name":"N1","Fqdn":"n1.corp.local","State":"Up"},{"Name":"N2","Fqdn":"n2.corp.local","State":"Down"},{"Name":"n1","Fqdn":"","State":"Up"}]}""");

        Assert.Equal(["N1", "N2"], many.Select(n => n.Name));
        Assert.True(many[0].IsReachable);
        Assert.False(many[1].IsReachable);

        var single = ClusterNodes.ParseNodes("""{"Nodes":{"Name":"N1","Fqdn":"n1","State":"Paused"}}""");
        Assert.Single(single);
        Assert.True(single[0].IsReachable);

        Assert.Empty(ClusterNodes.ParseNodes("""{"Nodes":[]}"""));
    }

    [Fact]
    public void ParseNodes_IgnoresNoiseAroundTheJson_AndRejectsMissingJson()
    {
        var nodes = ClusterNodes.ParseNodes("WARNING: something\n{\"Nodes\":[{\"Name\":\"N1\",\"Fqdn\":\"n1\",\"State\":\"Up\"}]}\n");
        Assert.Single(nodes);

        Assert.Throws<InvalidOperationException>(() => ClusterNodes.ParseNodes("no json here"));
    }

    [Fact]
    public void ConnectionName_UsesFqdnOnlyWhenTheClusterWasAddressedByOne()
    {
        var node = new ClusterNodeRef("N1", "n1.corp.local", "Up");

        Assert.Equal("N1", node.ConnectionName("CL1"));
        Assert.Equal("n1.corp.local", node.ConnectionName("cl1.corp.local"));
        Assert.Equal("N1", new ClusterNodeRef("N1", "", "Up").ConnectionName("cl1.corp.local"));
    }

    [Fact]
    public void Merge_CombinesAllNodes_EachHostOnce_AndKeepsOneClusterRow()
    {
        var cluster = new HyperVTarget { Name = "CL1" };
        cluster.Hosts.Add(new HyperVHost { Name = "stale" });

        ClusterNodes.Merge(cluster, [Node("N1", owner: true, "vmA", "vmB"), Node("N2", owner: false, "vmC"), Node("N1", owner: true)]);

        Assert.Equal(["N1", "N2"], cluster.Hosts.Select(h => h.Name));
        Assert.Equal(["vmA", "vmB", "vmC"], cluster.VirtualMachines.Select(v => v.Name));
        Assert.Single(cluster.Clusters);
        Assert.Equal(["N1"], cluster.Hosts.Where(h => h.IsClusterOwner).Select(h => h.Name));
    }

    // ---------------------------------------------------------

    [Fact]
    public void Cache_ListsANodeOnce_WhenItWasAddedAsPartOfTheClusterAndOnItsOwn()
    {
        var cluster = new HyperVTarget { Name = "CL1" };
        ClusterNodes.Merge(cluster, [Node("N1", owner: true, "vmA"), Node("N2", owner: false, "vmB")]);

        var alone = Node("n1.corp.local", owner: true, "vmA");

        var cache = new InventoryCache();
        cache.UpdateTarget(alone);      // added first on purpose
        cache.UpdateTarget(cluster);

        var snapshot = cache.GetSnapshot();

        Assert.Equal(["N1", "N2"], snapshot.Hosts.Select(h => h.Name).Order());
        Assert.Equal(["vmA", "vmB"], snapshot.VirtualMachines.Select(v => v.Name).Order());
        Assert.Equal(2, snapshot.Targets.Count);
    }

    [Fact]
    public void Cache_KeepsDistinctHostsFromDifferentTargets()
    {
        var cache = new InventoryCache();
        cache.UpdateTarget(Node("HV01", owner: false, "a"));
        cache.UpdateTarget(Node("HV02", owner: false, "b"));

        var snapshot = cache.GetSnapshot();

        Assert.Equal(2, snapshot.Hosts.Count);
        Assert.Equal(2, snapshot.VirtualMachines.Count);
    }

    [Fact]
    public void HostRows_CarryTheClusterOwnerFlag_ForExactlyOneNode()
    {
        var snapshot = new InventorySnapshot
        {
            Hosts =
            [
                new HyperVHost { Name = "N1", IsClusterNode = true, IsClusterOwner = true },
                new HyperVHost { Name = "N2", IsClusterNode = true }
            ]
        };

        var rows = HostInventoryBuilder.Build(snapshot);

        Assert.True(rows.Single(r => r.HostName == "N1").IsClusterOwner);
        Assert.False(rows.Single(r => r.HostName == "N2").IsClusterOwner);
    }

    [Fact]
    public async Task EachNodeCanBeExportedOnItsOwn_WithOnlyItsOwnData()
    {
        var cluster = new HyperVTarget { Name = "CL1" };
        var nodes = new[] { Node("N1", owner: true, "vmA"), Node("N2", owner: false, "vmB", "vmC") };
        ClusterNodes.Merge(cluster, nodes);
        cluster.NodeTargets.AddRange(nodes);

        var folder = Path.Combine(Path.GetTempPath(), $"hvtx-node-{Guid.NewGuid():N}");

        try
        {
            foreach (var node in cluster.NodeTargets)
            {
                var cache = new InventoryCache();
                cache.UpdateTarget(node);

                await new HyperVToolsX.Export.CsvInventoryExporter()
                    .ExportAsync(cache.GetSnapshot(), Path.Combine(folder, $"{node.Name}.csv"));
            }

            var n1 = File.ReadAllText(Path.Combine(folder, "N1-vInfo.csv"));
            var n2 = File.ReadAllText(Path.Combine(folder, "N2-vInfo.csv"));

            Assert.Contains("vmA", n1);
            Assert.DoesNotContain("vmB", n1);
            Assert.Contains("vmB", n2);
            Assert.Contains("vmC", n2);
            Assert.DoesNotContain("vmA", n2);

            var host1 = File.ReadAllLines(Path.Combine(folder, "N1-vHost.csv"));
            Assert.Equal(2, host1.Length);                 // header + N1 only
            Assert.Contains("True", host1[1]);             // N1 owns the cluster
        }
        finally
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }
    }
}
