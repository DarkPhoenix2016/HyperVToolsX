# HyperVToolsX

A Windows desktop inventory and documentation tool for Hyper-V environments,
inspired by RVTools for VMware. HyperVToolsX connects to one or more Hyper-V
hosts or cluster nodes, collects detailed configuration data, and presents it
in a familiar tabbed grid interface for quick auditing, documentation, and
troubleshooting.

## Features

- **Host & VM inventory** — logical processors, memory capacity, VM count,
  state, generation, and uptime at a glance
- **Per-VM detail tabs** — vCPU, vMemory, vDisk, vNetwork, vVLAN, vCheckpoint,
  vIntegration, vReplication, vDVD, vCluster, and vHost, mirroring the
  RVTools tab layout
- **Cluster-aware collection** — resolves cluster membership and node
  relationships so VMs and hosts roll up correctly across a cluster
- **Search & filter** — filter by cluster, host, or state, and free-text
  search across VMs, hosts, and IP addresses
- **Live summary bar** — total VMs, running/powered-off counts, total vCPUs,
  assigned memory, checkpoints, and nodes queried
- **Built for scale** — designed to collect from 100+ hosts and 1,000+ VMs
  via a throttled, concurrent collection pipeline rather than sequential
  per-host polling

## How it works

Data is collected via PowerShell's Hyper-V module (`Get-VM`, `Get-VMHost`,
`Get-VMProcessor`, `Get-VMMemory`, `Get-VMNetworkAdapter`,
`Get-VMNetworkAdapterVlan`, etc.), executed in-process against each target
host. Collected data is normalized into a set of flat row models (one per
tab) and cached in memory, so the UI stays responsive and can refresh
independently of collection.

## Architecture

- **Core** — provider-agnostic interfaces and models
  (`IHyperVProvider`, `IInventoryCollector`, `IInventoryCache`,
  `ITargetManager`, `ITargetValidator`)
- **Infrastructure** — the PowerShell-based `HyperVProvider` implementation
  and `PowerShellExecutor`, an async wrapper around the PowerShell SDK with
  proper cancellation support
- **Collection** — `BasicInventoryCollector` orchestrates per-target
  collection (host, VMs, CPU, memory, network, VLANs, disks) based on a
  `CollectionRequest`
- **UI** — WPF desktop client with a tabbed `DataGrid` view over the
  cached inventory snapshot

## Status

Actively under development. Core inventory collection (host, VM, CPU,
memory, network, VLAN) is functional; storage, checkpoints, replication,
and cluster tabs are in progress. Not yet optimized for very large fleets —
current focus is correctness of the collection layer before scaling out
the worker pool.

## Requirements

- Windows with Hyper-V PowerShell module installed
- .NET (WPF) — targets Windows only
- Appropriate permissions to query target Hyper-V hosts (local admin or
  delegated Hyper-V administrator rights)