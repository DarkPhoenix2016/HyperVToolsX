using System.Text.Json;
using HyperVToolsX.Infrastructure.PowerShellEngine;
using HyperVToolsX.Core.Collection;
using System.Management.Automation;

namespace HyperVToolsX.Infrastructure.Collection;

public class RemoteInventoryCollector
{
    private readonly PowerShellExecutor _powerShell;

    public RemoteInventoryCollector(PowerShellExecutor powerShell)
    {
        _powerShell = powerShell;
    }

    public async Task<RemoteInventoryPackage> CollectAsync(string computerName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(computerName))
        {
            throw new ArgumentException("Computer name is required.", nameof(computerName));
        }

        cancellationToken.ThrowIfCancellationRequested();
        var script = BuildCollectionScript();

        var result = await _powerShell.ExecutePipelineAsync(
            cancellationToken,
            ("Invoke-Command", new Dictionary<string, object?>
            {
                ["ComputerName"] = computerName,
                ["ScriptBlock"] = ScriptBlock.Create(script)
            }));

        cancellationToken.ThrowIfCancellationRequested();

        var json = result.FirstOrDefault()?.BaseObject?.ToString();
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new InvalidOperationException($"Remote inventory returned no data from '{computerName}'.");
        }

        var package = JsonSerializer.Deserialize<RemoteInventoryPackage>(
            json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (package == null)
        {
            throw new InvalidOperationException($"Remote inventory could not be deserialized from '{computerName}'.");
        }

        package.ComputerName = computerName;
        return package;
    }

    private static string BuildCollectionScript()
    {
        return """
            $ErrorActionPreference = 'Stop'
            $result = [ordered]@{
                ComputerName = $env:COMPUTERNAME
                Hosts = @(); VirtualMachines = @(); Processors = @(); Memories = @()
                NetworkAdapters = @(); NetworkVlans = @(); Checkpoints = @(); IntegrationServices = @()
                VmStorage = @(); Disks = @(); Vhds = @(); Replication = @()
                Dvds = @(); HostStorage = @(); OperatingSystems = @(); Clusters = @()
                Success = $true; ErrorMessage = ''
            }

            try {
                $vmHost = Get-VMHost
                $vms = @(Get-VM)
                $hostName = $vmHost.ComputerName
                if ([string]::IsNullOrWhiteSpace($hostName)) { $hostName = $env:COMPUTERNAME }

                $clusterName = ''
                try {
                    $cluster = Get-Cluster -ErrorAction Stop
                    if ($null -ne $cluster) { $clusterName = $cluster.Name }
                }
                catch { $clusterName = '' }
                $isClusterNode = -not [string]::IsNullOrWhiteSpace($clusterName)

                $result.Hosts = @(
                [PSCustomObject]@{
                    Name = $hostName
                    Fqdn = $hostName
                    OperatingSystem = ''
                    HyperVVersion = ''
                    LogicalProcessorCount = $vmHost.LogicalProcessorCount
                    TotalMemoryBytes = $vmHost.MemoryCapacity
                    UsedMemoryBytes = 0
                    VirtualMachineCount = $vms.Count
                    ClusterName = $clusterName
                    IsClusterNode = $isClusterNode
                    IsConnected = $true
                }
            )

                $result.HostStorage = @(
                    $vmHost | ForEach-Object {
                        [PSCustomObject]@{
                            HostName = $_.ComputerName; ComputerName = $_.ComputerName; VirtualHardDiskPath = $_.VirtualHardDiskPath
                            VirtualMachinePath = $_.VirtualMachinePath; ParentSnapshotPath = $_.ParentSnapshotPath; MemoryCapacity = $_.MemoryCapacity
                            LogicalProcessorCount = $_.LogicalProcessorCount; MaximumStorageMigrations = $_.MaximumStorageMigrations
                            MaximumVirtualMachineMigrations = $_.MaximumVirtualMachineMigrations; VirtualMachineMigrationEnabled = $_.VirtualMachineMigrationEnabled
                            VirtualMachineMigrationAuthenticationType = $_.VirtualMachineMigrationAuthenticationType
                            VirtualMachineMigrationPerformanceOption = $_.VirtualMachineMigrationPerformanceOption
                            UseAnyNetworkForMigration = $_.UseAnyNetworkForMigration; EnableEnhancedSessionMode = $_.EnableEnhancedSessionMode; IsDeleted = $_.IsDeleted
                        }
                    }
                )

                $result.OperatingSystems = @(
                    Get-CimInstance -ClassName Win32_OperatingSystem | ForEach-Object {
                        [PSCustomObject]@{
                            ComputerName = $_.CSName; Caption = $_.Caption; Version = $_.Version; BuildNumber = $_.BuildNumber
                            OSArchitecture = $_.OSArchitecture; LastBootUpTime = $_.LastBootUpTime
                            TotalVisibleMemorySizeKb = $_.TotalVisibleMemorySize; FreePhysicalMemoryKb = $_.FreePhysicalMemory
                        }
                    }
                )

                $result.VirtualMachines = @(
                    $vms | ForEach-Object {
                        [PSCustomObject]@{
                            Name = if ($_.VMName) { $_.VMName } else { $_.Name }
                            VMId = $_.VMId.ToString()
                            HostName = if ($_.ComputerName) { $_.ComputerName } else { $hostName }
                            CheckpointFileLocation = $_.CheckpointFileLocation; ConfigurationLocation = $_.ConfigurationLocation
                            GuestStatePath = $_.GuestStatePath; SmartPagingFileInUse = $_.SmartPagingFileInUse
                            SmartPagingFilePath = $_.SmartPagingFilePath; SnapshotFileLocation = $_.SnapshotFileLocation
                            AutomaticStartAction = $_.AutomaticStartAction; AutomaticStartDelay = $_.AutomaticStartDelay
                            AutomaticStopAction = $_.AutomaticStopAction; AutomaticCriticalErrorAction = $_.AutomaticCriticalErrorAction
                            AutomaticCriticalErrorActionTimeout = $_.AutomaticCriticalErrorActionTimeout; AutomaticCheckpointsEnabled = $_.AutomaticCheckpointsEnabled
                            CPUUsage = $_.CPUUsage; MemoryAssigned = $_.MemoryAssigned; MemoryDemand = $_.MemoryDemand
                            MemoryStatus = if ($null -ne $_.MemoryStatus) { $_.MemoryStatus.ToString() } else { '' }
                            NumaAligned = $_.NumaAligned; NumaNodesCount = $_.NumaNodesCount; NumaSocketCount = $_.NumaSocketCount
                            Heartbeat = if ($null -ne $_.Heartbeat) { $_.Heartbeat.ToString() } else { '' }
                            IntegrationServicesState = if ($null -ne $_.IntegrationServicesState) { $_.IntegrationServicesState.ToString() } else { '' }
                            IntegrationServicesVersion = $_.IntegrationServicesVersion; Uptime = $_.Uptime
                            OperationalStatus = @($_.OperationalStatus | ForEach-Object { if ($null -ne $_) { $_.ToString() } })
                            StatusDescriptions = @($_.StatusDescriptions | ForEach-Object { if ($null -ne $_) { $_.ToString() } })
                            PrimaryOperationalStatus = if ($null -ne $_.PrimaryOperationalStatus) { $_.PrimaryOperationalStatus.ToString() } else { '' }
                            SecondaryOperationalStatus = if ($null -ne $_.SecondaryOperationalStatus) { $_.SecondaryOperationalStatus.ToString() } else { '' }
                            PrimaryStatusDescription = $_.PrimaryStatusDescription; SecondaryStatusDescription = $_.SecondaryStatusDescription
                            Status = if ($null -ne $_.Status) { $_.Status.ToString() } else { '' }
                            ReplicationHealth = if ($null -ne $_.ReplicationHealth) { $_.ReplicationHealth.ToString() } else { '' }
                            ReplicationMode = if ($null -ne $_.ReplicationMode) { $_.ReplicationMode.ToString() } else { '' }
                            ReplicationState = if ($null -ne $_.ReplicationState) { $_.ReplicationState.ToString() } else { '' }
                            ResourceMeteringEnabled = $_.ResourceMeteringEnabled
                            CheckpointType = if ($null -ne $_.CheckpointType) { $_.CheckpointType.ToString() } else { '' }
                            EnhancedSessionTransportType = if ($null -ne $_.EnhancedSessionTransportType) { $_.EnhancedSessionTransportType.ToString() } else { '' }
                            Groups = @($_.Groups | ForEach-Object { if ($null -ne $_) { $_.ToString() } })
                            Version = if ($null -ne $_.Version) { $_.Version.ToString() } else { '' }
                            VirtualMachineType = if ($null -ne $_.VirtualMachineType) { $_.VirtualMachineType.ToString() } else { '' }
                            VirtualMachineSubType = if ($null -ne $_.VirtualMachineSubType) { $_.VirtualMachineSubType.ToString() } else { '' }
                            GuestStateIsolationType = if ($null -ne $_.GuestStateIsolationType) { $_.GuestStateIsolationType.ToString() } else { '' }
                            Notes = $_.Notes; State = if ($null -ne $_.State) { $_.State.ToString() } else { '' }
                            DynamicMemoryEnabled = $_.DynamicMemoryEnabled; MemoryMaximum = $_.MemoryMaximum; MemoryMinimum = $_.MemoryMinimum
                            MemoryStartup = $_.MemoryStartup; ProcessorCount = $_.ProcessorCount; BatteryPassthroughEnabled = $_.BatteryPassthroughEnabled
                            Generation = $_.Generation; IsClustered = $_.IsClustered
                            BootTime = if ($null -ne $_.Uptime -and $_.Uptime -gt [TimeSpan]::Zero) { (Get-Date) - $_.Uptime } else { $null }
                        }
                    }
                )

                $result.Processors = @(
                    $vms | Get-VMProcessor | ForEach-Object {
                        [PSCustomObject]@{
                            VmName = $_.VMName; HostName = if ($_.ComputerName) { $_.ComputerName } else { $hostName }
                            StatusDescription = @($_.StatusDescription | ForEach-Object { if ($null -ne $_) { $_.ToString() } })
                            OperationalStatus = @($_.OperationalStatus | ForEach-Object { if ($null -ne $_) { $_.ToString() } })
                            ResourcePoolName = $_.ResourcePoolName; Count = $_.Count
                            CompatibilityForMigrationEnabled = $_.CompatibilityForMigrationEnabled
                            CompatibilityForMigrationMode = if ($null -ne $_.CompatibilityForMigrationMode) { $_.CompatibilityForMigrationMode.ToString() } else { '' }
                            CompatibilityForOlderOperatingSystemsEnabled = $_.CompatibilityForOlderOperatingSystemsEnabled
                            HwThreadCountPerCore = $_.HwThreadCountPerCore; ExposeVirtualizationExtensions = $_.ExposeVirtualizationExtensions
                            EnablePerfmonPmu = $_.EnablePerfmonPmu; EnablePerfmonArchPmu = $_.EnablePerfmonArchPmu; EnablePerfmonLbr = $_.EnablePerfmonLbr
                            EnablePerfmonPebs = $_.EnablePerfmonPebs; EnablePerfmonIpt = $_.EnablePerfmonIpt; EnableLegacyApicMode = $_.EnableLegacyApicMode
                            ApicMode = if ($null -ne $_.ApicMode) { $_.ApicMode.ToString() } else { '' }
                            AllowACountMCount = $_.AllowACountMCount; CpuBrandString = $_.CpuBrandString; PerfCpuFreqCapMhz = $_.PerfCpuFreqCapMhz
                            L3CacheWays = $_.L3CacheWays; PhysicalAddressWidth = $_.PhysicalAddressWidth; Maximum = $_.Maximum; Reserve = $_.Reserve
                            RelativeWeight = $_.RelativeWeight; MaximumCountPerNumaNode = $_.MaximumCountPerNumaNode
                            MaximumCountPerNumaSocket = $_.MaximumCountPerNumaSocket; EnableHostResourceProtection = $_.EnableHostResourceProtection
                        }
                    }
                )

                $result.Memories = @(
                    $vms | Get-VMMemory | ForEach-Object {
                        [PSCustomObject]@{
                            VmName = $_.VMName; HostName = if ($_.ComputerName) { $_.ComputerName } else { $hostName }
                            ResourcePoolName = $_.ResourcePoolName; Buffer = $_.Buffer; DynamicMemoryEnabled = $_.DynamicMemoryEnabled
                            Maximum = $_.Maximum; MaximumPerNumaNode = $_.MaximumPerNumaNode; Minimum = $_.Minimum; Priority = $_.Priority
                            Startup = $_.Startup; HugePagesEnabled = $_.HugePagesEnabled; MemoryEncryptionPolicy = $_.MemoryEncryptionPolicy
                            MemoryEncryptionEnabled = $_.MemoryEncryptionEnabled; BackingType = $_.BackingType; Name = $_.Name
                        }
                    }
                )

                $result.NetworkAdapters = @(
                    $vms | Get-VMNetworkAdapter | ForEach-Object {
                        $ipAddresses = @($_.IPAddresses | ForEach-Object { if ($null -ne $_) { $_.ToString() } })
                        $ipv4 = @(); $ipv6 = @()
                        foreach ($ip in $ipAddresses) {
                            if ([System.Net.IPAddress]::TryParse($ip, [ref]$parsedAddress)) {
                                if ($parsedAddress.AddressFamily -eq [System.Net.Sockets.AddressFamily]::InterNetwork) { $ipv4 += $ip }
                                elseif ($parsedAddress.AddressFamily -eq [System.Net.Sockets.AddressFamily]::InterNetworkV6) { $ipv6 += $ip }
                            }
                        }
                        [PSCustomObject]@{
                            VmName = $_.VMName; HostName = if ($_.ComputerName) { $_.ComputerName } else { $hostName }
                            Name = $_.Name; SwitchName = $_.SwitchName; MacAddress = $_.MacAddress
                            IPv4Addresses = $ipv4; IPv6Addresses = $ipv6
                            Status = @($_.Status | ForEach-Object { if ($null -ne $_) { $_.ToString() } }) -join ', '
                        }
                    }
                )

                $result.NetworkVlans = @()
                foreach ($adapter in $vms | Get-VMNetworkAdapter) {
                    try {
                        $vlan = $adapter | Get-VMNetworkAdapterVlan -ErrorAction Stop
                        if ($null -eq $vlan) { continue }
                        $result.NetworkVlans += [PSCustomObject]@{
                            VmName = $adapter.VMName; AdapterName = $adapter.Name
                            OperationMode = if ($null -ne $vlan.OperationMode) { $vlan.OperationMode.ToString() } else { '' }
                            AccessVlanId = $vlan.AccessVlanId; NativeVlanId = $vlan.NativeVlanId
                            AllowedVlanIdList = @($vlan.AllowedVlanIdList); AllowedVlanIdListString = $vlan.AllowedVlanIdListString
                            PrivateVlanMode = $vlan.PrivateVlanMode; PrimaryVlanId = $vlan.PrimaryVlanId; SecondaryVlanId = $vlan.SecondaryVlanId
                            SecondaryVlanIdList = @($vlan.SecondaryVlanIdList); SecondaryVlanIdListString = $vlan.SecondaryVlanIdListString
                            ParentAdapter = if ($null -ne $vlan.ParentAdapter) { $vlan.ParentAdapter.ToString() } else { '' }
                            IsTemplate = $vlan.IsTemplate
                        }
                    }
                    catch { continue }
                }

                $result.Checkpoints = @(
                    $vms | Get-VMSnapshot | ForEach-Object {
                        [PSCustomObject]@{
                            VmName = $_.VMName; HostName = if ($_.ComputerName) { $_.ComputerName } else { $hostName }
                            VMId = if ($_.VMId) { $_.VMId.ToString() } else { '' }
                            Name = $_.Name; Id = if ($_.Id) { $_.Id.ToString() } else { '' }
                            ParentCheckpointId = if ($_.ParentCheckpointId) { $_.ParentCheckpointId.ToString() } else { '' }
                            ParentCheckpointName = $_.ParentCheckpointName
                            CheckpointType = if ($null -ne $_.CheckpointType) { $_.CheckpointType.ToString() } else { '' }
                            SnapshotType = if ($null -ne $_.SnapshotType) { $_.SnapshotType.ToString() } else { '' }
                            IsAutomaticCheckpoint = $_.IsAutomaticCheckpoint
                            State = if ($null -ne $_.State) { $_.State.ToString() } else { '' }
                            Path = $_.Path; CreationTime = $_.CreationTime; IsDeleted = $_.IsDeleted; Version = $_.Version
                            SizeOfSystemFiles = $_.SizeOfSystemFiles
                        }
                    }
                )

                $result.IntegrationServices = @(
                    $vms | Get-VMIntegrationService | ForEach-Object {
                        [PSCustomObject]@{
                            VmName = $_.VMName; HostName = if ($_.ComputerName) { $_.ComputerName } else { $hostName }
                            VMId = if ($_.VMId) { $_.VMId.ToString() } else { '' }
                            Id = if ($_.Id) { $_.Id.ToString() } else { '' }
                            Name = $_.Name; Enabled = $_.Enabled
                            OperationalStatus = @($_.OperationalStatus | ForEach-Object { if ($null -ne $_) { $_.ToString() } })
                            PrimaryOperationalStatus = if ($null -ne $_.PrimaryOperationalStatus) { $_.PrimaryOperationalStatus.ToString() } else { '' }
                            PrimaryStatusDescription = $_.PrimaryStatusDescription
                            SecondaryOperationalStatus = if ($null -ne $_.SecondaryOperationalStatus) { $_.SecondaryOperationalStatus.ToString() } else { '' }
                            SecondaryStatusDescription = $_.SecondaryStatusDescription
                            StatusDescription = @($_.StatusDescription | ForEach-Object { if ($null -ne $_) { $_.ToString() } })
                            VMCheckpointId = if ($_.VMCheckpointId) { $_.VMCheckpointId.ToString() } else { '' }
                            VMCheckpointName = $_.VMCheckpointName
                            VMSnapshotId = if ($_.VMSnapshotId) { $_.VMSnapshotId.ToString() } else { '' }
                            VMSnapshotName = $_.VMSnapshotName; IsClustered = $_.IsClustered; IsDeleted = $_.IsDeleted
                        }
                    }
                )

                $result.VmStorage = @(
                    $vms | ForEach-Object {
                        [PSCustomObject]@{
                            VmName = if ($_.VMName) { $_.VMName } else { $_.Name }
                            HostName = if ($_.ComputerName) { $_.ComputerName } else { $hostName }
                            VMId = if ($_.VMId) { $_.VMId.ToString() } else { '' }
                            ParentCheckpointId = if ($_.ParentCheckpointId) { $_.ParentCheckpointId.ToString() } else { '' }
                            ParentCheckpointName = $_.ParentCheckpointName; CheckpointFileLocation = $_.CheckpointFileLocation
                            ConfigurationLocation = $_.ConfigurationLocation; GuestStatePath = $_.GuestStatePath
                            SmartPagingFileInUse = $_.SmartPagingFileInUse; SmartPagingFilePath = $_.SmartPagingFilePath
                            SnapshotFileLocation = $_.SnapshotFileLocation; Path = $_.Path; SizeOfSystemFiles = $_.SizeOfSystemFiles
                            ParentSnapshotId = if ($_.ParentSnapshotId) { $_.ParentSnapshotId.ToString() } else { '' }
                            ParentSnapshotName = $_.ParentSnapshotName; AutomaticStartAction = $_.AutomaticStartAction
                            AutomaticStartDelay = $_.AutomaticStartDelay; AutomaticStopAction = $_.AutomaticStopAction
                            AutomaticCriticalErrorAction = $_.AutomaticCriticalErrorAction
                            AutomaticCriticalErrorActionTimeout = $_.AutomaticCriticalErrorActionTimeout
                            AutomaticCheckpointsEnabled = $_.AutomaticCheckpointsEnabled
                            State = if ($null -ne $_.State) { $_.State.ToString() } else { '' }
                            Status = if ($null -ne $_.Status) { $_.Status.ToString() } else { '' }
                            CheckpointType = if ($null -ne $_.CheckpointType) { $_.CheckpointType.ToString() } else { '' }
                            ResourceMeteringEnabled = $_.ResourceMeteringEnabled
                            EnhancedSessionTransportType = if ($null -ne $_.EnhancedSessionTransportType) { $_.EnhancedSessionTransportType.ToString() } else { '' }
                            GuestStateIsolationType = if ($null -ne $_.GuestStateIsolationType) { $_.GuestStateIsolationType.ToString() } else { '' }
                            VirtualMachineType = if ($null -ne $_.VirtualMachineType) { $_.VirtualMachineType.ToString() } else { '' }
                            VirtualMachineSubType = if ($null -ne $_.VirtualMachineSubType) { $_.VirtualMachineSubType.ToString() } else { '' }
                            Version = if ($null -ne $_.Version) { $_.Version.ToString() } else { '' }
                            GuestControlledCacheTypes = $_.GuestControlledCacheTypes; LowMemoryMappedIoSpace = $_.LowMemoryMappedIoSpace
                            HighMemoryMappedIoSpace = $_.HighMemoryMappedIoSpace; HighMemoryMappedIoBaseAddress = $_.HighMemoryMappedIoBaseAddress
                            LockOnDisconnect = if ($null -ne $_.LockOnDisconnect) { $_.LockOnDisconnect.ToString() } else { '' }
                            CreationTime = $_.CreationTime; IsDeleted = $_.IsDeleted
                        }
                    }
                )

                $disks = @($vms | Get-VMHardDiskDrive)
                $result.Disks = @(
                    $disks | ForEach-Object {
                        [PSCustomObject]@{
                            VmName = $_.VMName; HostName = if ($_.ComputerName) { $_.ComputerName } else { $hostName }
                            Path = $_.Path; DiskNumber = $_.DiskNumber; MaximumIOPS = $_.MaximumIOPS; MinimumIOPS = $_.MinimumIOPS
                            QoSPolicyID = if ($_.QoSPolicyID) { $_.QoSPolicyID.ToString() } else { '' }
                            SupportPersistentReservations = $_.SupportPersistentReservations
                            WriteHardeningMethod = $_.WriteHardeningMethod; ControllerLocation = $_.ControllerLocation
                            ControllerNumber = $_.ControllerNumber
                            ControllerType = if ($null -ne $_.ControllerType) { $_.ControllerType.ToString() } else { '' }
                            Name = $_.Name; PoolName = $_.PoolName
                        }
                    }
                )

                $vhdPaths = @($disks | Where-Object { -not [string]::IsNullOrWhiteSpace($_.Path) } | Select-Object -ExpandProperty Path -Unique)
                $result.Vhds = @()
                foreach ($vhdPath in $vhdPaths) {
                    try {
                        Get-VHD -Path $vhdPath -ErrorAction Stop | ForEach-Object {
                            $result.Vhds += [PSCustomObject]@{
                                HostName = $hostName; Path = $_.Path
                                VhdFormat = if ($null -ne $_.VhdFormat) { $_.VhdFormat.ToString() } else { '' }
                                VhdType = if ($null -ne $_.VhdType) { $_.VhdType.ToString() } else { '' }
                                FileSize = $_.FileSize; Size = $_.Size; MinimumSize = $_.MinimumSize
                                LogicalSectorSize = $_.LogicalSectorSize; PhysicalSectorSize = $_.PhysicalSectorSize; BlockSize = $_.BlockSize
                                ParentPath = $_.ParentPath; DiskIdentifier = if ($_.DiskIdentifier) { $_.DiskIdentifier.ToString() } else { '' }
                                FragmentationPercentage = if ($null -ne $_.FragmentationPercentage) { $_.FragmentationPercentage.ToString() } else { '' }
                                Alignment = $_.Alignment; Attached = $_.Attached
                                DiskNumber = if ($null -ne $_.DiskNumber) { $_.DiskNumber.ToString() } else { '' }
                                IsPMEMCompatible = $_.IsPMEMCompatible
                                AddressAbstractionType = if ($null -ne $_.AddressAbstractionType) { $_.AddressAbstractionType.ToString() } else { '' }
                            }
                        }
                    }
                    catch { continue }
                }

                try {
                    $result.Replication = @(
                        Get-VMReplication | ForEach-Object {
                            [PSCustomObject]@{
                                VmName = $_.VMName; VMId = if ($_.VMId) { $_.VMId.ToString() } else { '' }
                                ReplicationState = if ($null -ne $_.ReplicationState) { $_.ReplicationState.ToString() } else { '' }
                                ReplicationHealth = if ($null -ne $_.ReplicationHealth) { $_.ReplicationHealth.ToString() } else { '' }
                                ReplicationMode = if ($null -ne $_.ReplicationMode) { $_.ReplicationMode.ToString() } else { '' }
                                PrimaryServer = $_.PrimaryServer; ReplicaServer = $_.ReplicaServer; ReplicaServerPort = $_.ReplicaServerPort
                                AuthenticationType = $_.AuthenticationType; CertificateThumbprint = $_.CertificateThumbprint
                                CompressionEnabled = $_.CompressionEnabled; AutoResynchronizeEnabled = $_.AutoResynchronizeEnabled
                                AutoResynchronizeIntervalStart = $_.AutoResynchronizeIntervalStart
                                AutoResynchronizeIntervalEnd = $_.AutoResynchronizeIntervalEnd
                                ReplicationIntervalSec = $_.ReplicationIntervalSec; FrequencySec = $_.FrequencySec
                                LastReplicationTime = $_.LastReplicationTime; CurrentReplicaTime = $_.CurrentReplicaTime
                                LastSuccessfulReplicationTime = $_.LastSuccessfulReplicationTime; FailedOver = $_.FailedOver
                                TestFailoverInProcess = $_.TestFailoverInProcess; TestFailoverTime = $_.TestFailoverTime
                                TestFailoverVMName = $_.TestFailoverVMName
                                ReplicationHealthDetails = @($_.ReplicationHealthDetails | ForEach-Object { if ($null -ne $_) { $_.ToString() } })
                                IncludedDisks = @($_.IncludedDisks | ForEach-Object { if ($null -ne $_) { $_.ToString() } })
                                ExcludedDisks = @($_.ExcludedDisks | ForEach-Object { if ($null -ne $_) { $_.ToString() } })
                                RecoveryHistory = $_.RecoveryHistory
                                ApplicationConsistentSnapshotFrequencyInHours = $_.ApplicationConsistentSnapshotFrequencyInHours
                                ExtendedReplicationState = $_.ExtendedReplicationState; ExtendedReplicaServer = $_.ExtendedReplicaServer
                                ExtendedReplicaServerPort = $_.ExtendedReplicaServerPort; ExtendedAuthenticationType = $_.ExtendedAuthenticationType
                                ExtendedCertificateThumbprint = $_.ExtendedCertificateThumbprint
                            }
                        }
                    )
                }
                catch { $result.Replication = @() }

                $result.Dvds = @(
                    $vms | Get-VMDvdDrive | ForEach-Object {
                        [PSCustomObject]@{
                            VmName = $_.VMName; HostName = if ($_.ComputerName) { $_.ComputerName } else { $hostName }
                            VMId = if ($_.VMId) { $_.VMId.ToString() } else { '' }
                            Id = if ($_.Id) { $_.Id.ToString() } else { '' }
                            Path = $_.Path; DvdMediaType = if ($null -ne $_.DvdMediaType) { $_.DvdMediaType.ToString() } else { '' }
                            ControllerLocation = $_.ControllerLocation; ControllerNumber = $_.ControllerNumber
                            ControllerType = if ($null -ne $_.ControllerType) { $_.ControllerType.ToString() } else { '' }
                            Name = $_.Name; PoolName = $_.PoolName
                            VMCheckpointId = if ($_.VMCheckpointId) { $_.VMCheckpointId.ToString() } else { '' }
                            VMCheckpointName = $_.VMCheckpointName
                            VMSnapshotId = if ($_.VMSnapshotId) { $_.VMSnapshotId.ToString() } else { '' }
                            VMSnapshotName = $_.VMSnapshotName; IsDeleted = $_.IsDeleted
                        }
                    }
                )

                $result.Clusters = @()
                if ($isClusterNode) {
                    try {
                        $result.Clusters += @(
                            Get-Cluster -Name $clusterName -ErrorAction Stop | ForEach-Object {
                                [PSCustomObject]@{
                                    Name = $_.Name; Domain = $_.Domain; Id = if ($_.Id) { $_.Id.ToString() } else { '' }
                                    SharedVolumesRoot = $_.SharedVolumesRoot; AddEvictDelay = $_.AddEvictDelay
                                    BackupInProgress = $_.BackupInProgress; BlockCacheSize = $_.BlockCacheSize
                                    ClusSvcDataPartitionMounted = $_.ClusSvcDataPartitionMounted
                                    ClusterEnforcedAntiAffinity = $_.ClusterEnforcedAntiAffinity
                                    ClusterFunctionalLevel = $_.ClusterFunctionalLevel; ClusterGroupWaitDelay = $_.ClusterGroupWaitDelay
                                    ClusterLogLevel = $_.ClusterLogLevel; ClusterLogSize = $_.ClusterLogSize
                                    CsvBalancedValidationThresholdInHours = $_.CsvBalancedValidationThresholdInHours
                                    CsvDirectIoOpt = $_.CsvDirectIoOpt; CsvFltValidationThresholdInHours = $_.CsvFltValidationThresholdInHours
                                    CustomDeadlockDetectionTimeout = $_.CustomDeadlockDetectionTimeout
                                    DatabaseReadWriteMode = $_.DatabaseReadWriteMode; DefaultNetworkRole = $_.DefaultNetworkRole
                                    Description = $_.Description; DrainOnShutdown = $_.DrainOnShutdown; DumpPolicy = $_.DumpPolicy
                                    DynamicQuorum = $_.DynamicQuorum; EnableAutomaticMetric = $_.EnableAutomaticMetric
                                    AutoAssignNodeSite = $_.AutoAssignNodeSite; AutoBalancerMode = $_.AutoBalancerMode
                                    AutoBalancerLevel = $_.AutoBalancerLevel; FixQuorum = $_.FixQuorum
                                    GracePeriodOnUnbalanced = $_.GracePeriodOnUnbalanced
                                    GroupAdministrativeDelay = $_.GroupAdministrativeDelay; HangRecoveryAction = $_.HangRecoveryAction
                                    IgnorePersistentStateOnStartup = $_.IgnorePersistentStateOnStartup
                                    LogResourceControls = $_.LogResourceControls; LowerQuorumPriorityNodeId = $_.LowerQuorumPriorityNodeId
                                    MaxNumberOfNodes = $_.MaxNumberOfNodes; MessageBufferLength = $_.MessageBufferLength
                                    MinimumNeverPreemptPriority = $_.MinimumNeverPreemptPriority
                                    MinimumPreemptorPriority = $_.MinimumPreemptorPriority; NetftIPSecEnabled = $_.NetftIPSecEnabled
                                    PlacementOptions = $_.PlacementOptions; PreventQuorum = $_.PreventQuorum
                                    QuorumArbitrationTimeMax = $_.QuorumArbitrationTimeMax; QuorumLogFileSize = $_.QuorumLogFileSize
                                    RequestReplyTimeout = $_.RequestReplyTimeout; ResiliencyDefaultPeriod = $_.ResiliencyDefaultPeriod
                                    ResiliencyPeriodFilter = $_.ResiliencyPeriodFilter; ResourceDllDeadlockTimeout = $_.ResourceDllDeadlockTimeout
                                    RootMemoryReserved = $_.RootMemoryReserved; RouteHistoryLength = $_.RouteHistoryLength
                                    S2DCacheBehavior = $_.S2DCacheBehavior; S2DCacheFlashReservePercent = $_.S2DCacheFlashReservePercent
                                    S2DCachePageSizeKBytes = $_.S2DCachePageSizeKBytes; S2DEnabled = $_.S2DEnabled
                                    S2DIOLatencyThreshold = $_.S2DIOLatencyThreshold; S2DOptimizeFlashPoolThresholdPct = $_.S2DOptimizeFlashPoolThresholdPct
                                    SameSubnetDelay = $_.SameSubnetDelay; SameSubnetThreshold = $_.SameSubnetThreshold
                                    SharedVolumeBlockCacheSizeInMB = $_.SharedVolumeBlockCacheSizeInMB
                                    SharedVolumeCompatibleFilters = @($_.SharedVolumeCompatibleFilters | ForEach-Object { if ($null -ne $_) { $_.ToString() } })
                                    SharedVolumeSecurityDescriptor = $_.SharedVolumeSecurityDescriptor
                                    ShutdownTimeoutInMinutes = $_.ShutdownTimeoutInMinutes
                                    UseClientAccessNetworksForSharedVolumes = $_.UseClientAccessNetworksForSharedVolumes
                                    WitnessDatabaseWriteTimeout = $_.WitnessDatabaseWriteTimeout; WitnessDynamicWeight = $_.WitnessDynamicWeight
                                    WitnessRestartInterval = $_.WitnessRestartInterval; CrossSiteDelay = $_.CrossSiteDelay
                                    CrossSiteThreshold = $_.CrossSiteThreshold; CrossSubnetDelay = $_.CrossSubnetDelay
                                    CrossSubnetThreshold = $_.CrossSubnetThreshold; PlumbAllCrossSubnetRoutes = $_.PlumbAllCrossSubnetRoutes
                                    PreferredSite = $_.PreferredSite
                                    QuorumType = if ($null -ne $_.QuorumType) { $_.QuorumType.ToString() } else { '' }
                                    Status = if ($null -ne $_.Status) { $_.Status.ToString() } else { '' }
                                }
                            }
                        )
                    }
                    catch { $result.Clusters = @() }
                }

                $result.Success = $true
                $result.ErrorMessage = ''
            }
            catch {
                $result.Success = $false
                $result.ErrorMessage = $_.Exception.Message
            }

            $result | ConvertTo-Json -Depth 30 -Compress
            """;
    }
}
