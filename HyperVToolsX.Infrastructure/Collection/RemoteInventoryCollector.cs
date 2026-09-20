using System.Text.Json;
using System.Text.Json.Serialization;
using HyperVToolsX.Core.Collection;
using HyperVToolsX.Core.Enums;
using HyperVToolsX.Core.Interfaces;
using HyperVToolsX.Core.Logging;
using HyperVToolsX.Core.Models;
using HyperVToolsX.Infrastructure.Remoting;

namespace HyperVToolsX.Infrastructure.Collection;

public class RemoteInventoryCollector : IInventoryCollector
{
    private readonly RemoteScriptRunner _runner;
    private readonly ILiveLog _log;

    public RemoteInventoryCollector(RemoteScriptRunner runner, ILiveLog? log = null)
    {
        _runner = runner ?? throw new ArgumentNullException(nameof(runner));
        _log = log ?? NullLiveLog.Instance;
    }

    /// <summary>
    /// System.Text.Json has no built-in TimeSpan support (still true as of
    /// recent .NET releases — see dotnet/runtime#29932). The PowerShell script
    /// emits TimeSpan-typed values (Uptime, AutoResynchronizeIntervalStart/End)
    /// as plain strings via TimeSpan.ToString()'s default "c" format, which
    /// this converter parses back. Registering this once here covers both
    /// TimeSpan and TimeSpan? properties — STJ automatically wraps a
    /// JsonConverter&lt;T&gt; for Nullable&lt;T&gt; since .NET 5.
    /// </summary>
    private sealed class TimeSpanJsonConverter : JsonConverter<TimeSpan>
    {
        public override TimeSpan Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            var value = reader.GetString();

            return string.IsNullOrWhiteSpace(value)
                ? TimeSpan.Zero
                : TimeSpan.Parse(value);
        }

        public override void Write(
            Utf8JsonWriter writer,
            TimeSpan value,
            JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString());
        }
    }

    public async Task<HyperVTarget> CollectAsync(
        HyperVTarget target,
        CollectionRequest request,
        IProgress<CollectionProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (target == null)
        {
            throw new ArgumentNullException(nameof(target));
        }

        cancellationToken.ThrowIfCancellationRequested();

        // Prefer the name the user typed over the validator's resolved IP:
        // WinRM Kerberos/Negotiate needs a hostname, not an address.
        var computerName =
            !string.IsNullOrWhiteSpace(target.Name)
            && !RemoteConnectionOptions.RequiresExplicitCredentials(target.Name)
                ? target.Name
                : string.IsNullOrWhiteSpace(target.Address)
                    ? target.Name
                    : target.Address;

        if (string.IsNullOrWhiteSpace(computerName))
        {
            throw new ArgumentException(
                "Target computer name or address is required.",
                nameof(target));
        }

        if (target.Type == TargetType.Cluster && target.Validation.IsCluster)
        {
            await CollectClusterAsync(target, computerName, cancellationToken);
        }
        else
        {
            _log.Step("Collection", $"Collecting inventory via {computerName}", target.Name);

            var package = await CollectRemotePackageAsync(
                computerName,
                target.Name,
                cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            if (!package.Success)
            {
                throw new InvalidOperationException(
                    string.IsNullOrWhiteSpace(package.ErrorMessage)
                        ? $"Remote inventory collection failed for '{computerName}'."
                        : package.ErrorMessage);
            }

            _log.Step("Collection", "Mapping remote package to inventory model", target.Name);

            RemoteInventoryMapper.MapToTarget(target, package);
            ApplyHostDefaults(target);
        }

        _log.Info(
            "Collection",
            $"Mapped {target.Hosts.Count} host(s), {target.VirtualMachines.Count} VM(s), " +
            $"{target.NetworkAdapters.Count} NIC(s), {target.Processors.Count} CPU row(s)",
            target.Name);

        progress?.Report(
            new CollectionProgress
            {
                TotalTargets = 1,
                CompletedTargets = 1,
                TotalHosts = target.Hosts.Count,
                TotalVirtualMachines =
                    target.VirtualMachines.Count,
                CurrentTarget = target.Name,
                CurrentStage = "Remote data collection completed"
            });

        return target;
    }

    /// <summary>Most cluster nodes queried at once for one cluster (each is a separate PowerShell process).</summary>
    private const int MaxParallelNodes = 4;

    /// <summary>
    /// A cluster name only reaches whichever node owns it, and Get-VM only lists the VMs running on the node it
    /// runs on. So the nodes are listed first and each one is collected on its own; the results are combined into
    /// the cluster target, with every node (the owner included) appearing once.
    /// </summary>
    private async Task CollectClusterAsync(
        HyperVTarget target,
        string computerName,
        CancellationToken cancellationToken)
    {
        _log.Step("Collection", $"'{target.Name}' is a cluster: listing its nodes", target.Name);

        var json = await _runner.RunAsync(computerName, ClusterNodes.DiscoveryScript, cancellationToken);
        var nodes = ClusterNodes.ParseNodes(json);

        if (nodes.Count == 0)
        {
            throw new InvalidOperationException($"No cluster nodes were found for '{target.Name}'.");
        }

        _log.Info(
            "Collection",
            $"Cluster nodes: {string.Join(", ", nodes.Select(n => $"{n.Name} ({n.State})"))}",
            target.Name);

        foreach (var down in nodes.Where(n => !n.IsReachable))
        {
            _log.Warn("Collection", $"Skipping node {down.Name}: it is {down.State}", target.Name);
        }

        using var gate = new SemaphoreSlim(MaxParallelNodes);

        var outcomes = await Task.WhenAll(nodes.Where(n => n.IsReachable).Select(async node =>
        {
            await gate.WaitAsync(cancellationToken);

            try
            {
                return (Node: node, Data: await CollectNodeAsync(target, node, cancellationToken), Error: (string?)null);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _log.Warn("Collection", $"Node {node.Name} failed: {ex.Message}", target.Name);
                return (Node: node, Data: (HyperVTarget?)null, Error: ex.Message);
            }
            finally
            {
                gate.Release();
            }
        }));

        var collected = outcomes.Where(o => o.Data is not null).Select(o => o.Data!).ToList();

        if (collected.Count == 0)
        {
            throw new InvalidOperationException(
                $"Could not collect any node of cluster '{target.Name}': " +
                string.Join("; ", outcomes.Select(o => $"{o.Node.Name}: {o.Error}")));
        }

        ClusterNodes.Merge(target, collected);

        target.NodeTargets.Clear();
        target.NodeTargets.AddRange(collected);

        _log.Info(
            "Collection",
            $"Collected {collected.Count} of {nodes.Count} cluster node(s)",
            target.Name);
    }

    private async Task<HyperVTarget> CollectNodeAsync(
        HyperVTarget cluster,
        ClusterNodeRef node,
        CancellationToken cancellationToken)
    {
        var connectionName = node.ConnectionName(cluster.Name);

        _log.Step("Collection", $"Collecting cluster node {node.Name} via {connectionName}", cluster.Name);

        var package = await CollectRemotePackageAsync(connectionName, node.Name, cancellationToken);

        if (!package.Success)
        {
            throw new InvalidOperationException(
                string.IsNullOrWhiteSpace(package.ErrorMessage)
                    ? $"Remote inventory collection failed for '{connectionName}'."
                    : package.ErrorMessage);
        }

        var nodeTarget = new HyperVTarget
        {
            Name = node.Name,
            Address = connectionName,
            Type = TargetType.ClusteredHost,
            Validation = new TargetValidationResult
            {
                TargetName = node.Name,
                IsCluster = true,
                ClusterName = cluster.Validation.ClusterName
            }
        };

        RemoteInventoryMapper.MapToTarget(nodeTarget, package);
        ApplyHostDefaults(nodeTarget);

        return nodeTarget;
    }

    /// <summary>Fills in cluster and host names the script left empty, and marks the hosts as connected.</summary>
    private static void ApplyHostDefaults(HyperVTarget target)
    {
        var primaryHost =
            target.Hosts.FirstOrDefault();

        foreach (var host in target.Hosts)
        {
            if (string.IsNullOrWhiteSpace(host.ClusterName))
            {
                host.ClusterName =
                    target.Validation.ClusterName ?? string.Empty;
            }

            host.IsClusterNode =
                target.Validation.IsCluster ||
                host.IsClusterNode;

            host.IsConnected = true;
        }

        foreach (var vm in target.VirtualMachines)
        {
            if (string.IsNullOrWhiteSpace(vm.HostName) &&
                primaryHost != null)
            {
                vm.HostName = primaryHost.Name;
            }

            if (string.IsNullOrWhiteSpace(vm.ClusterName))
            {
                vm.ClusterName =
                    target.Validation.ClusterName ?? string.Empty;
            }
        }

        foreach (var processor in target.Processors)
        {
            if (string.IsNullOrWhiteSpace(processor.HostName) &&
                primaryHost != null)
            {
                processor.HostName = primaryHost.Name;
            }
        }

        foreach (var memory in target.Memories)
        {
            if (string.IsNullOrWhiteSpace(memory.HostName) &&
                primaryHost != null)
            {
                memory.HostName = primaryHost.Name;
            }
        }

        foreach (var adapter in target.NetworkAdapters)
        {
            if (string.IsNullOrWhiteSpace(adapter.HostName) &&
                primaryHost != null)
            {
                adapter.HostName = primaryHost.Name;
            }
        }

        foreach (var service in target.IntegrationServices)
        {
            if (string.IsNullOrWhiteSpace(service.HostName) &&
                primaryHost != null)
            {
                service.HostName = primaryHost.Name;
            }
        }

        foreach (var dvd in target.Dvds)
        {
            if (string.IsNullOrWhiteSpace(dvd.HostName) &&
                primaryHost != null)
            {
                dvd.HostName = primaryHost.Name;
            }
        }
    }

    private async Task<RemoteInventoryPackage> CollectRemotePackageAsync(
        string computerName,
        string logTarget,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var script = BuildCollectionScript();

        _log.Step("Collection", "Running collection script (hosts, VMs, storage, network, cluster)", logTarget);

        var json =
            await _runner.RunAsync(
                computerName,
                script,
                cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(json))
        {
            throw new InvalidOperationException(
                $"Remote inventory returned no data from '{computerName}'.");
        }

        // Hyper-V cmdlets can emit WARNING/verbose text ahead of (or after) the
        // JSON. Keep only the outermost {...} so that noise can't break parsing.
        var jsonStart = json.IndexOf('{');
        var jsonEnd = json.LastIndexOf('}');

        if (jsonStart < 0 || jsonEnd < jsonStart)
        {
            _log.Error("Collection", $"Output contained no JSON. First 300 chars: {Preview(json)}", logTarget);

            throw new InvalidOperationException(
                $"Remote inventory from '{computerName}' was not JSON: {Preview(json)}");
        }

        if (jsonStart > 0 || jsonEnd < json.Length - 1)
        {
            _log.Warn(
                "Collection",
                $"Ignored non-JSON output around the payload: '{Preview(json[..jsonStart])}'",
                logTarget);

            json = json[jsonStart..(jsonEnd + 1)];
        }

        _log.Step("Collection", $"Received {json.Length:N0} chars of JSON; deserializing", logTarget);

        RemoteInventoryPackage? package;

        try
        {
            package =
            JsonSerializer.Deserialize<RemoteInventoryPackage>(
                json,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    Converters = { new TimeSpanJsonConverter() }
                });
        }
        catch (JsonException ex)
        {
            _log.Error("Collection", $"JSON deserialization failed: {ex.Message}", logTarget);
            throw;
        }

        if (package == null)
        {
            throw new InvalidOperationException(
                $"Remote inventory could not be deserialized from '{computerName}'.");
        }

        package.ComputerName = computerName;

        foreach (var warning in package.Warnings)
        {
            _log.Warn("Collection", $"Partial data - section skipped: {warning}", logTarget);
        }

        return package;
    }

    private static string Preview(string value)
    {
        var flat = value.Replace((char)13, ' ').Replace((char)10, ' ').Trim();
        return flat.Length <= 300 ? flat : flat[..300] + "...";
    }

    private static string BuildCollectionScript()
    {
        return """
            $ErrorActionPreference = 'Stop'
            function ToStr($val) {
                if ($null -eq $val) { return '' }
                if ($val -is [System.Collections.IEnumerable] -and $val -isnot [string]) {
                    $items = @($val | ForEach-Object { if ($null -ne $_) { $_.ToString().Trim() } } | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
                    return ($items -join ', ')
                }
                return $val.ToString().Trim()
            }

            function ToStrList($val) {
                if ($null -eq $val) { return ,@() }
                $items = [System.Collections.Generic.List[string]]::new()
                if ($val -is [System.Collections.IEnumerable] -and $val -isnot [string]) {
                    foreach ($item in $val) {
                        if ($null -ne $item) {
                            $s = $item.ToString().Trim()
                            if (-not [string]::IsNullOrWhiteSpace($s)) {
                                $items.Add($s)
                            }
                        }
                    }
                }
                else {
                    $s = $val.ToString().Trim()
                    if (-not [string]::IsNullOrWhiteSpace($s)) {
                        $items.Add($s)
                    }
                }
                return ,$items.ToArray()
            }

            function ToInt($val, [int]$default = 0) {
                if ($null -eq $val) { return $default }
                $out = 0
                if ([int]::TryParse($val.ToString(), [ref]$out)) { return $out }
                return $default
            }

            function ToLong($val, [long]$default = 0L) {
                if ($null -eq $val) { return $default }
                $out = 0L
                if ([long]::TryParse($val.ToString(), [ref]$out)) { return $out }
                return $default
            }

            function ToBool($val, [bool]$default = $false) {
                if ($null -eq $val) { return $default }
                if ($val -is [bool]) { return $val }
                $out = $false
                if ([bool]::TryParse($val.ToString(), [ref]$out)) { return $out }
                return $default
            }

            function ToIsoDate($val) {
                # System.Text.Json's built-in DateTime?/DateTime converter only accepts
                # ISO-8601. PowerShell's bare .ToString() on a DateTime uses the current
                # culture's format (e.g. "1/15/2025 3:04:05 PM"), which fails to parse,
                # and ToStr on a $null value produces "" which also fails to parse as a
                # date. This always emits either $null or a round-trip ("o") ISO string.
                if ($null -eq $val) { return $null }

                $dateValue = $val
                if ($dateValue -isnot [DateTime]) {
                    $parsed = [DateTime]::MinValue
                    if (-not [DateTime]::TryParse($val.ToString(), [ref]$parsed)) {
                        return $null
                    }
                    $dateValue = $parsed
                }

                if ($dateValue -eq [DateTime]::MinValue) { return $null }

                return $dateValue.ToString('o')
            }

            $result = [ordered]@{
                ComputerName        = $env:COMPUTERNAME
                Hosts               = @()
                VirtualMachines     = @()
                Processors          = @()
                Memories            = @()
                NetworkAdapters     = @()
                NetworkVlans        = @()
                Checkpoints         = @()
                IntegrationServices = @()
                VmStorage           = @()
                Disks               = @()
                Vhds                = @()
                Replication         = @()
                Dvds                = @()
                HostStorage         = @()
                OperatingSystems    = @()
                Clusters            = @()
                Warnings            = @()
                Success             = $true
                ErrorMessage        = ''
            }

            try {
                # ============================================================
                # HOST
                # ============================================================
                $vmHost = Get-VMHost
                $vms = @(Get-VM)

                $hostName = ToStr $vmHost.ComputerName
                if ([string]::IsNullOrWhiteSpace($hostName)) {
                    $hostName = $env:COMPUTERNAME
                }

                $clusterName = ''
                try {
                    $cluster = Get-Cluster -ErrorAction Stop
                    if ($null -ne $cluster) {
                        $clusterName = ToStr $cluster.Name
                    }
                }
                catch {
                    $clusterName = ''
                }

                $isClusterNode = -not [string]::IsNullOrWhiteSpace($clusterName)

                # The node that owns the cluster core group (the cluster name / CNO).
                $isClusterOwner = $false
                if ($isClusterNode) {
                    try {
                        $coreGroup = @(Get-ClusterGroup -ErrorAction Stop | Where-Object { $_.GroupType -eq 'Cluster' })[0]
                        if ($null -ne $coreGroup -and $null -ne $coreGroup.OwnerNode) {
                            $ownerName = ToStr $coreGroup.OwnerNode.Name
                            $isClusterOwner = ($ownerName -ieq $env:COMPUTERNAME) -or ($ownerName -ieq $hostName) -or ($hostName -ilike "$ownerName.*")
                        }
                    }
                    catch {
                        $isClusterOwner = $false
                    }
                }

                $result.Hosts = @(
                    [PSCustomObject]@{
                        Name                  = $hostName
                        Fqdn                  = $hostName
                        OperatingSystem       = ''
                        HyperVVersion         = ''
                        LogicalProcessorCount = ToInt $vmHost.LogicalProcessorCount
                        TotalMemoryBytes      = ToLong $vmHost.MemoryCapacity
                        UsedMemoryBytes       = 0L
                        VirtualMachineCount   = [int]$vms.Count
                        ClusterName           = $clusterName
                        IsClusterNode         = $isClusterNode
                        IsClusterOwner        = $isClusterOwner
                        IsConnected           = $true
                    }
                )

                try {
                # ============================================================
                # HOST STORAGE
                # ============================================================
                $result.HostStorage = @(
                    $vmHost | ForEach-Object {
                        [PSCustomObject]@{
                            HostName                                  = ToStr $_.ComputerName
                            ComputerName                              = ToStr $_.ComputerName
                            VirtualHardDiskPath                       = ToStr $_.VirtualHardDiskPath
                            VirtualMachinePath                        = ToStr $_.VirtualMachinePath
                            ParentSnapshotPath                        = ToStr $_.ParentSnapshotPath
                            MemoryCapacity                            = ToLong $_.MemoryCapacity
                            LogicalProcessorCount                     = ToInt $_.LogicalProcessorCount
                            MaximumStorageMigrations                  = ToInt $_.MaximumStorageMigrations
                            MaximumVirtualMachineMigrations           = ToInt $_.MaximumVirtualMachineMigrations
                            VirtualMachineMigrationEnabled            = ToBool $_.VirtualMachineMigrationEnabled
                            VirtualMachineMigrationAuthenticationType = ToStr $_.VirtualMachineMigrationAuthenticationType
                            VirtualMachineMigrationPerformanceOption  = ToStr $_.VirtualMachineMigrationPerformanceOption
                            UseAnyNetworkForMigration                 = ToBool $_.UseAnyNetworkForMigration
                            EnableEnhancedSessionMode                 = ToBool $_.EnableEnhancedSessionMode
                            IsDeleted                                 = ToBool $_.IsDeleted
                        }
                    }
                )
                }
                catch {
                    $result.Warnings += ('HOST STORAGE: ' + $_.Exception.Message)
                }

                try {
                # ============================================================
                # OPERATING SYSTEM
                # ============================================================
                $result.OperatingSystems = @(
                    Get-CimInstance -ClassName Win32_OperatingSystem | ForEach-Object {
                        [PSCustomObject]@{
                            ComputerName             = ToStr $_.CSName
                            Caption                  = ToStr $_.Caption
                            Version                  = ToStr $_.Version
                            BuildNumber              = ToStr $_.BuildNumber
                            OSArchitecture           = ToStr $_.OSArchitecture
                            LastBootUpTime           = ToIsoDate $_.LastBootUpTime
                            TotalVisibleMemorySizeKb = ToLong $_.TotalVisibleMemorySize
                            FreePhysicalMemoryKb     = ToLong $_.FreePhysicalMemory
                        }
                    }
                )
                }
                catch {
                    $result.Warnings += ('OPERATING SYSTEM: ' + $_.Exception.Message)
                }

                try {
                # ============================================================
                # VIRTUAL MACHINES
                # ============================================================
                $result.VirtualMachines = @(
                    $vms | ForEach-Object {
                        [PSCustomObject]@{
                            Name                                = if ($_.VMName) { ToStr $_.VMName } else { ToStr $_.Name }
                            VMId                                = ToStr $_.VMId
                            HostName                            = if ($_.ComputerName) { ToStr $_.ComputerName } else { $hostName }
                            CheckpointFileLocation              = ToStr $_.CheckpointFileLocation
                            ConfigurationLocation               = ToStr $_.ConfigurationLocation
                            GuestStatePath                      = ToStr $_.GuestStatePath
                            SmartPagingFileInUse                = ToBool $_.SmartPagingFileInUse
                            SmartPagingFilePath                 = ToStr $_.SmartPagingFilePath
                            SnapshotFileLocation                = ToStr $_.SnapshotFileLocation
                            AutomaticStartAction                = ToStr $_.AutomaticStartAction
                            AutomaticStartDelay                 = ToInt $_.AutomaticStartDelay
                            AutomaticStopAction                 = ToStr $_.AutomaticStopAction
                            AutomaticCriticalErrorAction        = ToStr $_.AutomaticCriticalErrorAction
                            AutomaticCriticalErrorActionTimeout = ToInt $_.AutomaticCriticalErrorActionTimeout
                            AutomaticCheckpointsEnabled         = ToBool $_.AutomaticCheckpointsEnabled
                            CPUUsage                            = ToInt $_.CPUUsage
                            MemoryAssigned                      = ToLong $_.MemoryAssigned
                            MemoryDemand                        = ToLong $_.MemoryDemand
                            MemoryStatus                        = ToStr $_.MemoryStatus
                            NumaAligned                         = ToBool $_.NumaAligned
                            NumaNodesCount                      = ToInt $_.NumaNodesCount
                            NumaSocketCount                     = ToInt $_.NumaSocketCount
                            Heartbeat                           = ToStr $_.Heartbeat
                            IntegrationServicesState            = ToStr $_.IntegrationServicesState
                            IntegrationServicesVersion          = ToStr $_.IntegrationServicesVersion
                            Uptime                              = ToStr $_.Uptime
                            OperationalStatus                   = (ToStrList $_.OperationalStatus)
                            StatusDescriptions                  = (ToStrList $_.StatusDescriptions)
                            PrimaryOperationalStatus            = ToStr $_.PrimaryOperationalStatus
                            SecondaryOperationalStatus          = ToStr $_.SecondaryOperationalStatus
                            PrimaryStatusDescription            = ToStr $_.PrimaryStatusDescription
                            SecondaryStatusDescription          = ToStr $_.SecondaryStatusDescription
                            Status                              = ToStr $_.Status
                            ReplicationHealth                   = ToStr $_.ReplicationHealth
                            ReplicationMode                     = ToStr $_.ReplicationMode
                            ReplicationState                    = ToStr $_.ReplicationState
                            ResourceMeteringEnabled             = ToBool $_.ResourceMeteringEnabled
                            CheckpointType                      = ToStr $_.CheckpointType
                            EnhancedSessionTransportType        = ToStr $_.EnhancedSessionTransportType
                            Groups                              = (ToStrList $_.Groups)
                            Version                             = ToStr $_.Version
                            VirtualMachineType                  = ToStr $_.VirtualMachineType
                            VirtualMachineSubType               = ToStr $_.VirtualMachineSubType
                            GuestStateIsolationType             = ToStr $_.GuestStateIsolationType
                            Notes                               = ToStr $_.Notes
                            State                               = ToStr $_.State
                            StateId                             = [int]$_.State
                            DynamicMemoryEnabled                = ToBool $_.DynamicMemoryEnabled
                            MemoryMaximum                       = ToLong $_.MemoryMaximum
                            MemoryMinimum                       = ToLong $_.MemoryMinimum
                            MemoryStartup                       = ToLong $_.MemoryStartup
                            ProcessorCount                      = ToLong $_.ProcessorCount
                            BatteryPassthroughEnabled           = ToBool $_.BatteryPassthroughEnabled
                            Generation                          = ToInt $_.Generation
                            IsClustered                         = ToBool $_.IsClustered
                            BootTime                            = if ($null -ne $_.Uptime -and $_.Uptime -gt [TimeSpan]::Zero) { ToIsoDate ((Get-Date) - $_.Uptime) } else { $null }
                        }
                    }
                )
                }
                catch {
                    $result.Warnings += ('VIRTUAL MACHINES: ' + $_.Exception.Message)
                }

                try {
                # ============================================================
                # PROCESSOR
                # ============================================================
                $result.Processors = @(
                    $vms | Get-VMProcessor | ForEach-Object {
                        [PSCustomObject]@{
                            VmName                                       = ToStr $_.VMName
                            HostName                                     = if ($_.ComputerName) { ToStr $_.ComputerName } else { $hostName }
                            StatusDescription                            = ToStr $_.StatusDescription
                            OperationalStatus                            = (ToStrList $_.OperationalStatus)
                            ResourcePoolName                             = ToStr $_.ResourcePoolName
                            Count                                        = ToLong $_.Count
                            CompatibilityForMigrationEnabled             = ToBool $_.CompatibilityForMigrationEnabled
                            CompatibilityForMigrationMode                = ToStr $_.CompatibilityForMigrationMode
                            CompatibilityForOlderOperatingSystemsEnabled = ToBool $_.CompatibilityForOlderOperatingSystemsEnabled
                            HwThreadCountPerCore                         = ToLong $_.HwThreadCountPerCore
                            ExposeVirtualizationExtensions               = ToBool $_.ExposeVirtualizationExtensions
                            EnablePerfmonPmu                             = ToBool $_.EnablePerfmonPmu
                            EnablePerfmonArchPmu                         = ToBool $_.EnablePerfmonArchPmu
                            EnablePerfmonLbr                             = ToBool $_.EnablePerfmonLbr
                            EnablePerfmonPebs                            = ToBool $_.EnablePerfmonPebs
                            EnablePerfmonIpt                             = ToBool $_.EnablePerfmonIpt
                            EnableLegacyApicMode                         = ToBool $_.EnableLegacyApicMode
                            ApicMode                                     = ToStr $_.ApicMode
                            AllowACountMCount                            = ToBool $_.AllowACountMCount
                            CpuBrandString                               = ToStr $_.CpuBrandString
                            PerfCpuFreqCapMhz                            = ToLong $_.PerfCpuFreqCapMhz
                            L3CacheWays                                  = ToLong $_.L3CacheWays
                            PhysicalAddressWidth                         = ToLong $_.PhysicalAddressWidth
                            Maximum                                      = ToLong $_.Maximum
                            Reserve                                      = ToLong $_.Reserve
                            RelativeWeight                               = ToInt $_.RelativeWeight
                            MaximumCountPerNumaNode                      = ToLong $_.MaximumCountPerNumaNode
                            MaximumCountPerNumaSocket                    = ToLong $_.MaximumCountPerNumaSocket
                            EnableHostResourceProtection                 = ToBool $_.EnableHostResourceProtection
                        }
                    }
                )
                }
                catch {
                    $result.Warnings += ('PROCESSOR: ' + $_.Exception.Message)
                }

                try {
                # ============================================================
                # MEMORY
                # ============================================================
                $result.Memories = @(
                    $vms | Get-VMMemory | ForEach-Object {
                        [PSCustomObject]@{
                            VmName                  = ToStr $_.VMName
                            HostName                = if ($_.ComputerName) { ToStr $_.ComputerName } else { $hostName }
                            ResourcePoolName        = ToStr $_.ResourcePoolName
                            Buffer                  = ToInt $_.Buffer
                            DynamicMemoryEnabled    = ToBool $_.DynamicMemoryEnabled
                            Maximum                 = ToLong $_.Maximum
                            MaximumPerNumaNode      = ToLong $_.MaximumPerNumaNode
                            Minimum                 = ToLong $_.Minimum
                            Priority                = ToInt $_.Priority
                            Startup                 = ToLong $_.Startup
                            HugePagesEnabled        = ToBool $_.HugePagesEnabled
                            MemoryEncryptionPolicy  = ToStr $_.MemoryEncryptionPolicy
                            MemoryEncryptionEnabled = ToBool $_.MemoryEncryptionEnabled
                            BackingType             = ToStr $_.BackingType
                            Name                    = ToStr $_.Name
                        }
                    }
                )
                }
                catch {
                    $result.Warnings += ('MEMORY: ' + $_.Exception.Message)
                }

                try {
                # ============================================================
                # NETWORK ADAPTERS
                # ============================================================
                $result.NetworkAdapters = @(
                    $vms | Get-VMNetworkAdapter | ForEach-Object {
                        $ipAddresses = @($_.IPAddresses | ForEach-Object { ToStr $_ })
                        $ipv4 = [System.Collections.Generic.List[string]]::new()
                        $ipv6 = [System.Collections.Generic.List[string]]::new()

                        foreach ($ip in $ipAddresses) {
                            $parsedAddress = $null
                            if ([System.Net.IPAddress]::TryParse($ip, [ref]$parsedAddress)) {
                                if ($parsedAddress.AddressFamily -eq [System.Net.Sockets.AddressFamily]::InterNetwork) {
                                    $ipv4.Add($ip)
                                }
                                elseif ($parsedAddress.AddressFamily -eq [System.Net.Sockets.AddressFamily]::InterNetworkV6) {
                                    $ipv6.Add($ip)
                                }
                            }
                        }

                        [PSCustomObject]@{
                            VmName        = ToStr $_.VMName
                            HostName      = if ($_.ComputerName) { ToStr $_.ComputerName } else { $hostName }
                            Name          = ToStr $_.Name
                            SwitchName    = ToStr $_.SwitchName
                            MacAddress    = ToStr $_.MacAddress
                            IPv4Addresses = $ipv4.ToArray()
                            IPv6Addresses = $ipv6.ToArray()
                            Status        = ToStr $_.Status
                        }
                    }
                )
                }
                catch {
                    $result.Warnings += ('NETWORK ADAPTERS: ' + $_.Exception.Message)
                }

                try {
                # ============================================================
                # VLAN
                # ============================================================
                $result.NetworkVlans = @()

                foreach ($adapter in ($vms | Get-VMNetworkAdapter)) {
                    try {
                        $vlan = $adapter | Get-VMNetworkAdapterVlan -ErrorAction Stop
                        if ($null -eq $vlan) {
                            continue
                        }

                        $allowedVlans = [System.Collections.Generic.List[int]]::new()
                        if ($null -ne $vlan.AllowedVlanIdList) {
                            foreach ($id in $vlan.AllowedVlanIdList) { $allowedVlans.Add((ToInt $id)) }
                        }

                        $secVlans = [System.Collections.Generic.List[int]]::new()
                        if ($null -ne $vlan.SecondaryVlanIdList) {
                            foreach ($id in $vlan.SecondaryVlanIdList) { $secVlans.Add((ToInt $id)) }
                        }

                        $result.NetworkVlans += [PSCustomObject]@{
                            VmName                    = ToStr $adapter.VMName
                            AdapterName               = ToStr $adapter.Name
                            OperationMode             = ToStr $vlan.OperationMode
                            AccessVlanId              = ToInt $vlan.AccessVlanId
                            NativeVlanId              = ToInt $vlan.NativeVlanId
                            AllowedVlanIdList         = $allowedVlans.ToArray()
                            AllowedVlanIdListString   = ToStr $vlan.AllowedVlanIdListString
                            PrivateVlanMode           = ToInt $vlan.PrivateVlanMode
                            PrimaryVlanId             = ToInt $vlan.PrimaryVlanId
                            SecondaryVlanId           = ToInt $vlan.SecondaryVlanId
                            SecondaryVlanIdList       = $secVlans.ToArray()
                            SecondaryVlanIdListString = ToStr $vlan.SecondaryVlanIdListString
                            ParentAdapter             = ToStr $vlan.ParentAdapter
                            IsTemplate                = ToBool $vlan.IsTemplate
                        }
                    }
                    catch {
                        continue
                    }
                }
                }
                catch {
                    $result.Warnings += ('VLAN: ' + $_.Exception.Message)
                }

                try {
                # ============================================================
                # CHECKPOINTS
                # ============================================================
                $result.Checkpoints = @(
                    $vms | Get-VMSnapshot | ForEach-Object {
                        [PSCustomObject]@{
                            VmName                = ToStr $_.VMName
                            HostName              = if ($_.ComputerName) { ToStr $_.ComputerName } else { $hostName }
                            VMId                  = ToStr $_.VMId
                            Name                  = ToStr $_.Name
                            Id                    = ToStr $_.Id
                            ParentCheckpointId    = ToStr $_.ParentCheckpointId
                            ParentCheckpointName  = ToStr $_.ParentCheckpointName
                            CheckpointType        = ToStr $_.CheckpointType
                            SnapshotType          = ToStr $_.SnapshotType
                            IsAutomaticCheckpoint = ToBool $_.IsAutomaticCheckpoint
                            State                 = ToStr $_.State
                            Path                  = ToStr $_.Path
                            CreationTime          = ToIsoDate $_.CreationTime
                            IsDeleted             = ToBool $_.IsDeleted
                            Version               = ToStr $_.Version
                            SizeOfSystemFiles     = ToLong $_.SizeOfSystemFiles
                        }
                    }
                )
                }
                catch {
                    $result.Warnings += ('CHECKPOINTS: ' + $_.Exception.Message)
                }

                try {
                # ============================================================
                # INTEGRATION SERVICES
                # ============================================================
                $result.IntegrationServices = @(
                    $vms | Get-VMIntegrationService | ForEach-Object {
                        [PSCustomObject]@{
                            VmName                      = ToStr $_.VMName
                            HostName                    = if ($_.ComputerName) { ToStr $_.ComputerName } else { $hostName }
                            VMId                        = ToStr $_.VMId
                            Id                          = ToStr $_.Id
                            Name                        = ToStr $_.Name
                            Enabled                     = ToBool $_.Enabled
                            OperationalStatus           = (ToStrList $_.OperationalStatus)
                            PrimaryOperationalStatus    = ToStr $_.PrimaryOperationalStatus
                            PrimaryStatusDescription    = ToStr $_.PrimaryStatusDescription
                            SecondaryOperationalStatus  = ToStr $_.SecondaryOperationalStatus
                            SecondaryStatusDescription  = ToStr $_.SecondaryStatusDescription
                            StatusDescription           = (ToStrList $_.StatusDescription)
                            VMCheckpointId              = ToStr $_.VMCheckpointId
                            VMCheckpointName            = ToStr $_.VMCheckpointName
                            VMSnapshotId                = ToStr $_.VMSnapshotId
                            VMSnapshotName              = ToStr $_.VMSnapshotName
                            IsClustered                 = ToBool $_.IsClustered
                            IsDeleted                   = ToBool $_.IsDeleted
                        }
                    }
                )
                }
                catch {
                    $result.Warnings += ('INTEGRATION SERVICES: ' + $_.Exception.Message)
                }

                try {
                # ============================================================
                # VM STORAGE
                # ============================================================
                $result.VmStorage = @(
                    $vms | ForEach-Object {
                        [PSCustomObject]@{
                            VmName                              = if ($_.VMName) { ToStr $_.VMName } else { ToStr $_.Name }
                            HostName                            = if ($_.ComputerName) { ToStr $_.ComputerName } else { $hostName }
                            VMId                                = ToStr $_.VMId
                            ParentCheckpointId                  = ToStr $_.ParentCheckpointId
                            ParentCheckpointName                = ToStr $_.ParentCheckpointName
                            CheckpointFileLocation              = ToStr $_.CheckpointFileLocation
                            ConfigurationLocation               = ToStr $_.ConfigurationLocation
                            GuestStatePath                      = ToStr $_.GuestStatePath
                            SmartPagingFileInUse                = ToBool $_.SmartPagingFileInUse
                            SmartPagingFilePath                 = ToStr $_.SmartPagingFilePath
                            SnapshotFileLocation                = ToStr $_.SnapshotFileLocation
                            Path                                = ToStr $_.Path
                            SizeOfSystemFiles                   = ToLong $_.SizeOfSystemFiles
                            ParentSnapshotId                    = ToStr $_.ParentSnapshotId
                            ParentSnapshotName                  = ToStr $_.ParentSnapshotName
                            AutomaticStartAction                = ToStr $_.AutomaticStartAction
                            AutomaticStartDelay                 = ToInt $_.AutomaticStartDelay
                            AutomaticStopAction                 = ToStr $_.AutomaticStopAction
                            AutomaticCriticalErrorAction        = ToStr $_.AutomaticCriticalErrorAction
                            AutomaticCriticalErrorActionTimeout = ToInt $_.AutomaticCriticalErrorActionTimeout
                            AutomaticCheckpointsEnabled         = ToBool $_.AutomaticCheckpointsEnabled
                            State                               = ToStr $_.State
                            Status                              = ToStr $_.Status
                            CheckpointType                      = ToStr $_.CheckpointType
                            ResourceMeteringEnabled             = ToBool $_.ResourceMeteringEnabled
                            EnhancedSessionTransportType        = ToStr $_.EnhancedSessionTransportType
                            GuestStateIsolationType             = ToStr $_.GuestStateIsolationType
                            VirtualMachineType                  = ToStr $_.VirtualMachineType
                            VirtualMachineSubType               = ToStr $_.VirtualMachineSubType
                            Version                             = ToStr $_.Version
                            GuestControlledCacheTypes           = ToBool $_.GuestControlledCacheTypes
                            LowMemoryMappedIoSpace              = ToLong $_.LowMemoryMappedIoSpace
                            HighMemoryMappedIoSpace             = ToLong $_.HighMemoryMappedIoSpace
                            HighMemoryMappedIoBaseAddress       = ToLong $_.HighMemoryMappedIoBaseAddress
                            LockOnDisconnect                    = ToStr $_.LockOnDisconnect
                            CreationTime                        = ToIsoDate $_.CreationTime
                            IsDeleted                           = ToBool $_.IsDeleted
                        }
                    }
                )
                }
                catch {
                    $result.Warnings += ('VM STORAGE: ' + $_.Exception.Message)
                }

                try {
                # ============================================================
                # VM DISKS
                # ============================================================
                $disks = @($vms | Get-VMHardDiskDrive)

                $result.Disks = @(
                    $disks | ForEach-Object {
                        [PSCustomObject]@{
                            VmName                        = ToStr $_.VMName
                            HostName                      = if ($_.ComputerName) { ToStr $_.ComputerName } else { $hostName }
                            Path                          = ToStr $_.Path
                            DiskNumber                    = ToStr $_.DiskNumber
                            MaximumIOPS                   = ToLong $_.MaximumIOPS
                            MinimumIOPS                   = ToLong $_.MinimumIOPS
                            QoSPolicyID                   = ToStr $_.QoSPolicyID
                            SupportPersistentReservations = ToBool $_.SupportPersistentReservations
                            WriteHardeningMethod          = ToStr $_.WriteHardeningMethod
                            ControllerLocation            = ToInt $_.ControllerLocation
                            ControllerNumber              = ToInt $_.ControllerNumber
                            ControllerType                = ToStr $_.ControllerType
                            Name                          = ToStr $_.Name
                            PoolName                      = ToStr $_.PoolName
                        }
                    }
                )
                }
                catch {
                    $result.Warnings += ('VM DISKS: ' + $_.Exception.Message)
                }

                try {
                # ============================================================
                # VHD
                # ============================================================
                $vhdPaths = @($disks | Where-Object { -not [string]::IsNullOrWhiteSpace($_.Path) } | Select-Object -ExpandProperty Path -Unique)
                $vhdList = [System.Collections.Generic.List[object]]::new()

                foreach ($vhdPath in $vhdPaths) {
                    try {
                        Get-VHD -Path $vhdPath -ErrorAction Stop | ForEach-Object {
                            $vhdList.Add([PSCustomObject]@{
                                HostName                = $hostName
                                Path                    = ToStr $_.Path
                                VhdFormat               = ToStr $_.VhdFormat
                                VhdType                 = ToStr $_.VhdType
                                FileSize                = ToLong $_.FileSize
                                Size                    = ToLong $_.Size
                                MinimumSize             = ToLong $_.MinimumSize
                                LogicalSectorSize       = ToInt $_.LogicalSectorSize
                                PhysicalSectorSize      = ToInt $_.PhysicalSectorSize
                                BlockSize               = ToLong $_.BlockSize
                                ParentPath              = ToStr $_.ParentPath
                                DiskIdentifier          = ToStr $_.DiskIdentifier
                                FragmentationPercentage = ToStr $_.FragmentationPercentage
                                Alignment               = ToInt $_.Alignment
                                Attached                = ToBool $_.Attached
                                DiskNumber              = ToStr $_.DiskNumber
                                IsPMEMCompatible        = ToBool $_.IsPMEMCompatible
                                AddressAbstractionType  = ToStr $_.AddressAbstractionType
                            })
                        }
                    }
                    catch {
                        continue
                    }
                }

                $result.Vhds = $vhdList.ToArray()
                }
                catch {
                    $result.Warnings += ('VHD: ' + $_.Exception.Message)
                }

                try {
                # ============================================================
                # REPLICATION
                # ============================================================
                try {
                    $result.Replication = @(
                        Get-VMReplication | ForEach-Object {
                            [PSCustomObject]@{
                                VmName                                        = ToStr $_.VMName
                                VMId                                          = ToStr $_.VMId
                                ReplicationState                              = ToStr $_.ReplicationState
                                ReplicationHealth                             = ToStr $_.ReplicationHealth
                                ReplicationMode                               = ToStr $_.ReplicationMode
                                PrimaryServer                                 = ToStr $_.PrimaryServer
                                ReplicaServer                                 = ToStr $_.ReplicaServer
                                ReplicaServerPort                             = ToInt $_.ReplicaServerPort
                                AuthenticationType                            = ToStr $_.AuthenticationType
                                CertificateThumbprint                         = ToStr $_.CertificateThumbprint
                                CompressionEnabled                            = ToBool $_.CompressionEnabled
                                AutoResynchronizeEnabled                      = ToBool $_.AutoResynchronizeEnabled
                                AutoResynchronizeIntervalStart                = ToStr $_.AutoResynchronizeIntervalStart
                                AutoResynchronizeIntervalEnd                  = ToStr $_.AutoResynchronizeIntervalEnd
                                ReplicationIntervalSec                        = ToInt $_.ReplicationIntervalSec
                                FrequencySec                                  = ToInt $_.FrequencySec
                                LastReplicationTime                           = ToIsoDate $_.LastReplicationTime
                                CurrentReplicaTime                            = ToIsoDate $_.CurrentReplicaTime
                                LastSuccessfulReplicationTime                 = ToIsoDate $_.LastSuccessfulReplicationTime
                                FailedOver                                    = ToBool $_.FailedOver
                                TestFailoverInProcess                         = ToBool $_.TestFailoverInProcess
                                TestFailoverTime                              = ToIsoDate $_.TestFailoverTime
                                TestFailoverVMName                            = ToStr $_.TestFailoverVMName
                                ReplicationHealthDetails                      = (ToStrList $_.ReplicationHealthDetails)
                                IncludedDisks                                 = (ToStrList $_.IncludedDisks)
                                ExcludedDisks                                 = (ToStrList $_.ExcludedDisks)
                                RecoveryHistory                               = ToInt $_.RecoveryHistory
                                ApplicationConsistentSnapshotFrequencyInHours = ToInt $_.ApplicationConsistentSnapshotFrequencyInHours
                                ExtendedReplicationState                      = ToStr $_.ExtendedReplicationState
                                ExtendedReplicaServer                         = ToStr $_.ExtendedReplicaServer
                                ExtendedReplicaServerPort                     = ToInt $_.ExtendedReplicaServerPort
                                ExtendedAuthenticationType                    = ToStr $_.ExtendedAuthenticationType
                                ExtendedCertificateThumbprint                 = ToStr $_.ExtendedCertificateThumbprint
                            }
                        }
                    )
                }
                catch {
                    $result.Replication = @()
                }
                }
                catch {
                    $result.Warnings += ('REPLICATION: ' + $_.Exception.Message)
                }

                try {
                # ============================================================
                # DVD
                # ============================================================
                $result.Dvds = @(
                    $vms | Get-VMDvdDrive | ForEach-Object {
                        [PSCustomObject]@{
                            VmName             = ToStr $_.VMName
                            HostName           = if ($_.ComputerName) { ToStr $_.ComputerName } else { $hostName }
                            VMId               = ToStr $_.VMId
                            Id                 = ToStr $_.Id
                            Path               = ToStr $_.Path
                            DvdMediaType       = ToStr $_.DvdMediaType
                            ControllerLocation = ToInt $_.ControllerLocation
                            ControllerNumber   = ToInt $_.ControllerNumber
                            ControllerType     = ToStr $_.ControllerType
                            Name               = ToStr $_.Name
                            PoolName           = ToStr $_.PoolName
                            VMCheckpointId     = ToStr $_.VMCheckpointId
                            VMCheckpointName   = ToStr $_.VMCheckpointName
                            VMSnapshotId       = ToStr $_.VMSnapshotId
                            VMSnapshotName     = ToStr $_.VMSnapshotName
                            IsDeleted          = ToBool $_.IsDeleted
                        }
                    }
                )
                }
                catch {
                    $result.Warnings += ('DVD: ' + $_.Exception.Message)
                }

                try {
                # ============================================================
                # CLUSTER
                # ============================================================
                $result.Clusters = @()

                if ($isClusterNode) {
                    try {
                        $result.Clusters += @(
                            Get-Cluster -Name $clusterName -ErrorAction Stop | ForEach-Object {
                                [PSCustomObject]@{
                                    Name                                    = ToStr $_.Name
                                    Domain                                  = ToStr $_.Domain
                                    Id                                      = ToStr $_.Id
                                    SharedVolumesRoot                       = ToStr $_.SharedVolumesRoot
                                    AddEvictDelay                           = ToInt $_.AddEvictDelay
                                    BackupInProgress                        = ToInt $_.BackupInProgress
                                    BlockCacheSize                          = ToLong $_.BlockCacheSize
                                    ClusSvcDataPartitionMounted             = ToInt $_.ClusSvcDataPartitionMounted
                                    ClusterEnforcedAntiAffinity             = ToInt $_.ClusterEnforcedAntiAffinity
                                    ClusterFunctionalLevel                  = ToInt $_.ClusterFunctionalLevel
                                    ClusterGroupWaitDelay                   = ToInt $_.ClusterGroupWaitDelay
                                    ClusterLogLevel                         = ToInt $_.ClusterLogLevel
                                    ClusterLogSize                          = ToLong $_.ClusterLogSize
                                    CsvBalancedValidationThresholdInHours   = ToInt $_.CsvBalancedValidationThresholdInHours
                                    CsvDirectIoOpt                          = ToInt $_.CsvDirectIoOpt
                                    CsvFltValidationThresholdInHours        = ToInt $_.CsvFltValidationThresholdInHours
                                    CustomDeadlockDetectionTimeout          = ToInt $_.CustomDeadlockDetectionTimeout
                                    DatabaseReadWriteMode                   = ToInt $_.DatabaseReadWriteMode
                                    DefaultNetworkRole                      = ToInt $_.DefaultNetworkRole
                                    Description                             = ToStr $_.Description
                                    DrainOnShutdown                         = ToInt $_.DrainOnShutdown
                                    DumpPolicy                              = ToInt $_.DumpPolicy
                                    DynamicQuorum                           = ToInt $_.DynamicQuorum
                                    EnableAutomaticMetric                   = ToInt $_.EnableAutomaticMetric
                                    AutoAssignNodeSite                      = ToInt $_.AutoAssignNodeSite
                                    AutoBalancerMode                        = ToInt $_.AutoBalancerMode
                                    AutoBalancerLevel                       = ToInt $_.AutoBalancerLevel
                                    FixQuorum                               = ToInt $_.FixQuorum
                                    GracePeriodOnUnbalanced                 = ToInt $_.GracePeriodOnUnbalanced
                                    GroupAdministrativeDelay                = ToInt $_.GroupAdministrativeDelay
                                    HangRecoveryAction                      = ToInt $_.HangRecoveryAction
                                    IgnorePersistentStateOnStartup          = ToInt $_.IgnorePersistentStateOnStartup
                                    LogResourceControls                     = ToInt $_.LogResourceControls
                                    LowerQuorumPriorityNodeId               = ToInt $_.LowerQuorumPriorityNodeId
                                    MaxNumberOfNodes                        = ToInt $_.MaxNumberOfNodes
                                    MessageBufferLength                     = ToInt $_.MessageBufferLength
                                    MinimumNeverPreemptPriority             = ToInt $_.MinimumNeverPreemptPriority
                                    MinimumPreemptorPriority                = ToInt $_.MinimumPreemptorPriority
                                    NetftIPSecEnabled                       = ToInt $_.NetftIPSecEnabled
                                    PlacementOptions                        = ToInt $_.PlacementOptions
                                    PreventQuorum                           = ToInt $_.PreventQuorum
                                    QuorumArbitrationTimeMax                = ToInt $_.QuorumArbitrationTimeMax
                                    QuorumLogFileSize                       = ToLong $_.QuorumLogFileSize
                                    RequestReplyTimeout                     = ToInt $_.RequestReplyTimeout
                                    ResiliencyDefaultPeriod                 = ToInt $_.ResiliencyDefaultPeriod
                                    ResiliencyPeriodFilter                  = ToInt $_.ResiliencyPeriodFilter
                                    ResourceDllDeadlockTimeout              = ToInt $_.ResourceDllDeadlockTimeout
                                    RootMemoryReserved                      = ToLong $_.RootMemoryReserved
                                    RouteHistoryLength                      = ToInt $_.RouteHistoryLength
                                    S2DCacheBehavior                        = ToStr $_.S2DCacheBehavior
                                    S2DCacheFlashReservePercent             = ToInt $_.S2DCacheFlashReservePercent
                                    S2DCachePageSizeKBytes                  = ToInt $_.S2DCachePageSizeKBytes
                                    S2DEnabled                              = ToInt $_.S2DEnabled
                                    S2DIOLatencyThreshold                   = ToInt $_.S2DIOLatencyThreshold
                                    S2DOptimizeFlashPoolThresholdPct        = ToInt $_.S2DOptimizeFlashPoolThresholdPct
                                    SameSubnetDelay                         = ToInt $_.SameSubnetDelay
                                    SameSubnetThreshold                     = ToInt $_.SameSubnetThreshold
                                    SharedVolumeBlockCacheSizeInMB          = ToLong $_.SharedVolumeBlockCacheSizeInMB
                                    SharedVolumeCompatibleFilters           = (ToStrList $_.SharedVolumeCompatibleFilters)
                                    SharedVolumeSecurityDescriptor          = ToStr $_.SharedVolumeSecurityDescriptor
                                    ShutdownTimeoutInMinutes                = ToInt $_.ShutdownTimeoutInMinutes
                                    UseClientAccessNetworksForSharedVolumes = ToInt $_.UseClientAccessNetworksForSharedVolumes
                                    WitnessDatabaseWriteTimeout             = ToInt $_.WitnessDatabaseWriteTimeout
                                    WitnessDynamicWeight                    = ToInt $_.WitnessDynamicWeight
                                    WitnessRestartInterval                  = ToInt $_.WitnessRestartInterval
                                    CrossSiteDelay                          = ToInt $_.CrossSiteDelay
                                    CrossSiteThreshold                      = ToInt $_.CrossSiteThreshold
                                    CrossSubnetDelay                        = ToInt $_.CrossSubnetDelay
                                    CrossSubnetThreshold                    = ToInt $_.CrossSubnetThreshold
                                    PlumbAllCrossSubnetRoutes               = ToInt $_.PlumbAllCrossSubnetRoutes
                                    PreferredSite                           = ToStr $_.PreferredSite
                                    QuorumType                              = ToStr $_.QuorumType
                                    Status                                  = ToStr $_.Status
                                }
                            }
                        )
                    }
                    catch {
                        $result.Clusters = @()
                    }
                }
                }
                catch {
                    $result.Warnings += ('CLUSTER: ' + $_.Exception.Message)
                }

                $result.Success = $true
                $result.ErrorMessage = ''
            }
            catch {
                $result.Success = $false
                $result.ErrorMessage = $_.Exception.Message
            }

            # ================================================================
            # RETURN SINGLE JSON PAYLOAD
            # ================================================================
            $result | ConvertTo-Json -Depth 30 -Compress
            """;
    }
}