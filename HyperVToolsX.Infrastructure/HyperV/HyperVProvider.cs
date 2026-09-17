using HyperVToolsX.Core.Interfaces;
using HyperVToolsX.Core.Models;
using HyperVToolsX.Core.Models.Details;
using HyperVToolsX.Infrastructure.PowerShellEngine;
using System.Collections;
using System.Management.Automation; 
using System.Xml.Linq;

namespace HyperVToolsX.Infrastructure.HyperV;

public class HyperVProvider : IHyperVProvider
{
    private readonly PowerShellExecutor _powerShell;
    public HyperVProvider(PowerShellExecutor powerShell)
    {
        _powerShell = powerShell;
    }

    // <summary> Host information is retrieved using the Get-VMHost cmdlet. </summary>

    public async Task<HyperVHost> GetHostAsync(string computerName, CancellationToken cancellationToken = default)
    {
        var result = await _powerShell.ExecutePipelineAsync(
            cancellationToken,
            ("Get-VMHost", new Dictionary<string, object?>
            {
                ["ComputerName"] = computerName
            }));

        if (result.Count == 0)
        {
            throw new InvalidOperationException($"Unable to retrieve Hyper-V host '{computerName}'.");
        }

        var host = result[0];

        return new HyperVHost
        {
            Name = GetString(host, "Name", computerName),
            LogicalProcessorCount = GetInt(host, "LogicalProcessorCount"),
            TotalMemoryBytes = GetLong(host, "MemoryCapacity"),
            IsConnected = true
        };
    }
    public async Task<HostStorageInfo> GetHostStorageAsync(string computerName,CancellationToken cancellationToken = default)
    {
        var result = await _powerShell.ExecutePipelineAsync(
            cancellationToken,
            (
                "Get-VMHost",
                new Dictionary<string, object?>
                {
                    ["ComputerName"] = computerName
                }
            ));

        var item = result.FirstOrDefault();

        if (item == null)
        {
            throw new InvalidOperationException(
                $"Get-VMHost returned no data for '{computerName}'.");
        }

        return new HostStorageInfo
        {
            HostName = GetString(item, "ComputerName"),
            ComputerName = GetString(item, "ComputerName"),

            VirtualHardDiskPath =
                GetString(item, "VirtualHardDiskPath"),

            VirtualMachinePath =
                GetString(item, "VirtualMachinePath"),

            ParentSnapshotPath =
                GetString(item, "ParentSnapshotPath"),

            MemoryCapacity =
                GetLong(item, "MemoryCapacity"),

            LogicalProcessorCount =
                GetInt(item, "LogicalProcessorCount"),

            MaximumStorageMigrations =
                GetInt(item, "MaximumStorageMigrations"),

            MaximumVirtualMachineMigrations =
                GetInt(item, "MaximumVirtualMachineMigrations"),

            VirtualMachineMigrationEnabled =
                GetBool(item, "VirtualMachineMigrationEnabled"),

            VirtualMachineMigrationAuthenticationType =
                GetString(
                    item,
                    "VirtualMachineMigrationAuthenticationType"),

            VirtualMachineMigrationPerformanceOption =
                GetString(
                    item,
                    "VirtualMachineMigrationPerformanceOption"),

            UseAnyNetworkForMigration =
                GetBool(item, "UseAnyNetworkForMigration"),

            EnableEnhancedSessionMode =
                GetBool(item, "EnableEnhancedSessionMode"),

            IsDeleted =
                GetBool(item, "IsDeleted")
        };
    }
    public async Task<OperatingSystemInfo> GetOperatingSystemAsync( string computerName,CancellationToken cancellationToken = default)
    {
        var result = await _powerShell.ExecutePipelineAsync(
            cancellationToken,
            (
                "Get-CimInstance",
                new Dictionary<string, object?>
                {
                    ["ClassName"] = "Win32_OperatingSystem",
                    ["ComputerName"] = computerName
                }
            ));

        var item = result.FirstOrDefault();

        if (item == null)
        {
            throw new InvalidOperationException(
                $"Win32_OperatingSystem returned no data for '{computerName}'.");
        }

        return new OperatingSystemInfo
        {
            ComputerName =
                GetString(item, "CSName"),

            Caption =
                GetString(item, "Caption"),

            Version =
                GetString(item, "Version"),

            BuildNumber =
                GetString(item, "BuildNumber"),

            OSArchitecture =
                GetString(item, "OSArchitecture"),

            LastBootUpTime =
                GetDateTime(item, "LastBootUpTime"),

            TotalVisibleMemorySizeKb =
                GetLong(item, "TotalVisibleMemorySize"),

            FreePhysicalMemoryKb =
                GetLong(item, "FreePhysicalMemory")
        };
    }

    // <summary> Retrieves a list of virtual machines on the specified Hyper-V host. </summary>
    public async Task<IReadOnlyList<HyperVVirtualMachine>> GetVirtualMachinesAsync(string computerName, CancellationToken cancellationToken =default)
    {
        var result = await _powerShell.ExecutePipelineAsync(cancellationToken, 
            ("Get-VM", 
            new Dictionary<string, object?>
            {
                ["ComputerName"] = computerName
            }
            ));

        var machines = new List<HyperVVirtualMachine>(result.Count);

        foreach (var vm in result)
        {
            var uptime = GetNullableTimeSpan(vm, "Uptime");

            machines.Add(new HyperVVirtualMachine
            {
                Name = GetString(vm, "VMName", GetString(vm, "Name")),
                VMId = GetGuid(vm, "VMId"),
                HostName = GetString(vm, "ComputerName", computerName),
                CheckpointFileLocation = GetString(vm, "CheckpointFileLocation"),
                ConfigurationLocation = GetString(vm, "ConfigurationLocation"),
                GuestStatePath = GetString(vm, "GuestStatePath"),
                SmartPagingFileInUse = GetBool(vm, "SmartPagingFileInUse"),
                SmartPagingFilePath = GetString(vm, "SmartPagingFilePath"),
                SnapshotFileLocation = GetString(vm, "SnapshotFileLocation"),
                AutomaticStartAction = GetString(vm, "AutomaticStartAction"),

                AutomaticStartDelay = GetInt(vm, "AutomaticStartDelay"),

                AutomaticStopAction = GetString(vm, "AutomaticStopAction"),

                AutomaticCriticalErrorAction = GetString(vm, "AutomaticCriticalErrorAction"),

                AutomaticCriticalErrorActionTimeout = GetInt(vm, "AutomaticCriticalErrorActionTimeout"),

                AutomaticCheckpointsEnabled = GetBool(vm, "AutomaticCheckpointsEnabled"),

                CPUUsage = GetInt(vm, "CPUUsage"),

                MemoryAssigned = GetLong(vm, "MemoryAssigned"),

                MemoryDemand = GetLong(vm, "MemoryDemand"),

                MemoryStatus = GetString(vm, "MemoryStatus"),

                NumaAligned = GetBool(vm, "NumaAligned"),

                NumaNodesCount = GetInt(vm, "NumaNodesCount"),

                NumaSocketCount = GetInt(vm, "NumaSocketCount"),

                Heartbeat = GetString(vm, "Heartbeat"),

                IntegrationServicesState = GetString(vm, "IntegrationServicesState"),

                IntegrationServicesVersion = GetString(vm, "IntegrationServicesVersion"),

                Uptime = uptime,

                OperationalStatus = GetStringList(vm, "OperationalStatus"),

                StatusDescriptions = GetStringList(vm, "StatusDescriptions"),

                PrimaryOperationalStatus = GetString(vm, "PrimaryOperationalStatus"),

                SecondaryOperationalStatus = GetString(vm, "SecondaryOperationalStatus"),

                PrimaryStatusDescription = GetString(vm, "PrimaryStatusDescription"),

                SecondaryStatusDescription = GetString(vm, "SecondaryStatusDescription"),

                Status = GetString(vm, "Status"),

                ReplicationHealth = GetString(vm, "ReplicationHealth"),

                ReplicationMode = GetString(vm, "ReplicationMode"),

                ReplicationState = GetString(vm, "ReplicationState"),

                ResourceMeteringEnabled = GetBool(vm, "ResourceMeteringEnabled"),

                CheckpointType = GetString(vm, "CheckpointType"),

                EnhancedSessionTransportType = GetString(vm, "EnhancedSessionTransportType"),

                Groups = GetStringList(vm, "Groups"),

                Version = GetString(vm, "Version"),

                VirtualMachineType = GetString(vm, "VirtualMachineType"),

                VirtualMachineSubType = GetString(vm, "VirtualMachineSubType"),

                GuestStateIsolationType = GetString(vm, "GuestStateIsolationType"),

                Notes = GetString(vm, "Notes"),

                State = GetString(vm, "State"),

                DynamicMemoryEnabled = GetBool(vm, "DynamicMemoryEnabled"),

                MemoryMaximum = GetLong(vm, "MemoryMaximum"),

                MemoryMinimum = GetLong(vm, "MemoryMinimum"),

                MemoryStartup = GetLong(vm, "MemoryStartup"),

                ProcessorCount = GetInt(vm, "ProcessorCount"),

                BatteryPassthroughEnabled = GetBool(vm, "BatteryPassthroughEnabled"),

                Generation = GetInt(vm, "Generation"),

                IsClustered = GetBool(vm, "IsClustered"),

                BootTime = uptime is { }
              up && up > TimeSpan.Zero ? DateTime.Now - up : null
            });
        }

        return machines;
    }
    public async Task<IReadOnlyList<VmProcessorInfo>> GetVmProcessorsAsync(string computerName, CancellationToken cancellationToken = default)
    {
        var result = await _powerShell.ExecutePipelineAsync(
            cancellationToken,
            ("Get-VM", new Dictionary<string, object?>
            {
                ["ComputerName"] = computerName
            }),
            ("Get-VMProcessor", null));

        var processors = new List<VmProcessorInfo>(result.Count);

        foreach (var processor in result)
        {
            processors.Add(new VmProcessorInfo
            {
                VmName = GetString(processor, "VMName"),
                HostName = GetString(processor, "ComputerName", computerName),
                StatusDescription = string.Join(", ", GetStringList(processor, "StatusDescription")),
                OperationalStatus = GetStringList(processor, "OperationalStatus"),
                ResourcePoolName = GetString(processor, "ResourcePoolName"),
                Count = GetInt(processor, "Count"),
                CompatibilityForMigrationEnabled = GetBool(processor, "CompatibilityForMigrationEnabled"),
                CompatibilityForMigrationMode = GetString(processor, "CompatibilityForMigrationMode"),
                CompatibilityForOlderOperatingSystemsEnabled = GetBool(processor, "CompatibilityForOlderOperatingSystemsEnabled"),
                HwThreadCountPerCore = GetInt(processor, "HwThreadCountPerCore"),
                ExposeVirtualizationExtensions = GetBool(processor, "ExposeVirtualizationExtensions"),
                EnablePerfmonPmu = GetBool(processor, "EnablePerfmonPmu"),
                EnablePerfmonArchPmu = GetBool(processor, "EnablePerfmonArchPmu"),
                EnablePerfmonLbr = GetBool(processor, "EnablePerfmonLbr"),
                EnablePerfmonPebs = GetBool(processor, "EnablePerfmonPebs"),
                EnablePerfmonIpt = GetBool(processor, "EnablePerfmonIpt"),
                EnableLegacyApicMode = GetBool(processor, "EnableLegacyApicMode"),
                ApicMode = GetString(processor, "ApicMode"),
                AllowACountMCount = GetBool(processor, "AllowACountMCount"),
                CpuBrandString = GetString(processor, "CpuBrandString"),
                PerfCpuFreqCapMhz = GetInt(processor, "PerfCpuFreqCapMhz"),
                L3CacheWays = GetInt(processor, "L3CacheWays"),
                PhysicalAddressWidth = GetInt(processor, "PhysicalAddressWidth"),
                Maximum = GetInt(processor, "Maximum"),
                Reserve = GetInt(processor, "Reserve"),
                RelativeWeight = GetInt(processor, "RelativeWeight"),
                MaximumCountPerNumaNode = GetInt(processor, "MaximumCountPerNumaNode"),
                MaximumCountPerNumaSocket = GetInt(processor, "MaximumCountPerNumaSocket"),
                EnableHostResourceProtection = GetBool(processor, "EnableHostResourceProtection")
            });
        }

        return processors;
    }
    public async Task<IReadOnlyList<VmMemoryInfo>> GetVmMemoryAsync(string computerName, CancellationToken cancellationToken = default)
    {
        var result = await _powerShell.ExecutePipelineAsync(
            cancellationToken,
            ("Get-VM", new Dictionary<string, object?>
            {
                ["ComputerName"] = computerName
            }),
            ("Get-VMMemory", null));

        var memory = new List<VmMemoryInfo>(result.Count);

        foreach (var item in result)
        {
            memory.Add(new VmMemoryInfo
            {
                VmName = GetString(item, "VMName"),
                HostName = GetString(item, "ComputerName", computerName),
                ResourcePoolName = GetString(item, "ResourcePoolName"),
                Buffer = GetInt(item, "Buffer"),
                DynamicMemoryEnabled = GetBool(item, "DynamicMemoryEnabled"),
                Maximum = GetLong(item, "Maximum"),
                MaximumPerNumaNode = GetLong(item, "MaximumPerNumaNode"),
                Minimum = GetLong(item, "Minimum"),
                Priority = GetInt(item, "Priority"),
                Startup = GetLong(item, "Startup"),
                HugePagesEnabled = GetBool(item, "HugePagesEnabled"),
                MemoryEncryptionPolicy = GetString(item, "MemoryEncryptionPolicy"),
                MemoryEncryptionEnabled = GetBool(item, "MemoryEncryptionEnabled"),
                BackingType = GetString(item, "BackingType"),
                Name = GetString(item, "Name")
            });
        }

        return memory;
    }
    public async Task<IReadOnlyList<VmNetworkAdapter>> GetNetworkAdaptersAsync(string computerName, CancellationToken cancellationToken = default)
    {
        var adapters = await _powerShell.ExecutePipelineAsync(
            cancellationToken,
            (
                "Get-VM",
                new Dictionary<string, object?>
                {
                    ["ComputerName"] = computerName
                }
            ),
            (
                "Get-VMNetworkAdapter",
                null
            )
        );

        var result = new List<VmNetworkAdapter>();

        foreach (var item in adapters)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var vmName = GetString(item, "VMName");
            var adapterName = GetString(item, "Name");

            var adapter = new VmNetworkAdapter
            {
                VmName = vmName,
                HostName = computerName,
                Name = adapterName,
                SwitchName = GetString(item, "SwitchName"),
                MacAddress = GetString(item, "MacAddress"),
                Status = string.Join(", ", GetStringList(item, "Status"))
            };

            var ipAddresses = GetStringList(item, "IPAddresses");

            foreach (var ipAddress in ipAddresses)
            {
                if (string.IsNullOrWhiteSpace(ipAddress))
                {
                    continue;
                }

                if (!System.Net.IPAddress.TryParse(ipAddress, out var parsedAddress))
                {
                    continue;
                }

                if (parsedAddress.AddressFamily ==
                    System.Net.Sockets.AddressFamily.InterNetwork)
                {
                    adapter.IPv4Addresses.Add(ipAddress);
                }
                else if (parsedAddress.AddressFamily ==
                         System.Net.Sockets.AddressFamily.InterNetworkV6)
                {
                    adapter.IPv6Addresses.Add(ipAddress);
                }
            }

            result.Add(adapter);
        }

        return result;
    }
    public async Task<IReadOnlyList<VmNetworkVlanInfo>> GetVmNetworkVlansAsync(string computerName,CancellationToken cancellationToken = default)
    {
        var results = await _powerShell.ExecutePipelineAsync(
        cancellationToken,
        (
            "Get-VM",
            new Dictionary<string, object?>
            {
                ["ComputerName"] = computerName
            }
        ),
        ("Get-VMNetworkAdapter", null),
        ("Get-VMNetworkAdapterVlan", null));

        return results
            .Select(item => new VmNetworkVlanInfo
            {
                VmName = GetParentAdapterProperty(item,"VMName"),
                AdapterName = GetParentAdapterProperty( item,"Name"),
                OperationMode = GetString(
                    item,
                    "OperationMode"),
                AccessVlanId = GetInt(
                    item,
                    "AccessVlanId"),
                NativeVlanId = GetInt(
                    item,
                    "NativeVlanId"),
                AllowedVlanIdList = GetIntList(
                    item,
                    "AllowedVlanIdList"),
                AllowedVlanIdListString = GetString(
                    item,
                    "AllowedVlanIdListString"),
                PrivateVlanMode = GetInt(
                    item,
                    "PrivateVlanMode"),
                PrimaryVlanId = GetInt(
                    item,
                    "PrimaryVlanId"),
                SecondaryVlanId = GetInt(
                    item,
                    "SecondaryVlanId"),
                SecondaryVlanIdList = GetIntList(
                    item,
                    "SecondaryVlanIdList"),
                SecondaryVlanIdListString = GetString(
                    item,
                    "SecondaryVlanIdListString"),
                IsTemplate = GetBool(
                    item,
                    "IsTemplate")
            })
            .ToList();
    }
    public async Task<IReadOnlyList<VmCheckpointInfo>> GetVmCheckpointsAsync(string computerName,CancellationToken cancellationToken = default)
    {
        var result = await _powerShell.ExecutePipelineAsync(
            cancellationToken,
            (
                "Get-VM",
                new Dictionary<string, object?>
                {
                    ["ComputerName"] = computerName
                }
            ),
            ("Get-VMSnapshot", null));

        var checkpoints = new List<VmCheckpointInfo>(result.Count);

        foreach (var item in result)
        {
            cancellationToken.ThrowIfCancellationRequested();

            checkpoints.Add(new VmCheckpointInfo
            {
                VmName = GetString(item, "VMName"),
                HostName = GetString(item, "ComputerName", computerName),
                VMId = GetString(item, "VMId"),
                Name = GetString(item, "Name"),
                Id = GetString(item, "Id"),
                ParentCheckpointId = GetString(item, "ParentCheckpointId"),
                ParentCheckpointName = GetString(item, "ParentCheckpointName"),
                CheckpointType = GetString(item, "CheckpointType"),
                SnapshotType = GetString(item, "SnapshotType"),
                IsAutomaticCheckpoint = GetBool(item, "IsAutomaticCheckpoint"),
                State = GetString(item, "State"),
                Path = GetString(item, "Path"),
                CreationTime = GetDateTime(item, "CreationTime"),
                IsDeleted = GetBool(item, "IsDeleted"),
                Version = GetString(item, "Version"),
                SizeOfSystemFiles = GetLong(item, "SizeOfSystemFiles")
            });
        }

        return checkpoints;
    }
    public async Task<IReadOnlyList<VmIntegrationServiceInfo>> GetVmIntegrationServicesAsync(string computerName,CancellationToken cancellationToken = default)
    {
        var result = await _powerShell.ExecutePipelineAsync(
            cancellationToken,
            (
                "Get-VM",
                new Dictionary<string, object?>
                {
                    ["ComputerName"] = computerName
                }
            ),
            ("Get-VMIntegrationService", null));

        var services = new List<VmIntegrationServiceInfo>(result.Count);

        foreach (var item in result)
        {
            services.Add(new VmIntegrationServiceInfo
            {
                VmName = GetString(item, "VMName"),
                HostName = GetString(item, "ComputerName", computerName),
                VMId = GetString(item, "VMId"),
                Id = GetString(item, "Id"),
                Name = GetString(item, "Name"),
                Enabled = GetBool(item, "Enabled"),
                OperationalStatus = GetStringList(item, "OperationalStatus"),
                PrimaryOperationalStatus = GetString(item, "PrimaryOperationalStatus"),
                PrimaryStatusDescription = GetString(item, "PrimaryStatusDescription"),
                SecondaryOperationalStatus = GetString(item, "SecondaryOperationalStatus"),
                SecondaryStatusDescription = GetString(item, "SecondaryStatusDescription"),
                StatusDescription = GetStringList(item, "StatusDescription"),
                VMCheckpointId = GetString(item, "VMCheckpointId"),
                VMCheckpointName = GetString(item, "VMCheckpointName"),
                VMSnapshotId = GetString(item, "VMSnapshotId"),
                VMSnapshotName = GetString(item, "VMSnapshotName"),
                IsClustered = GetBool(item, "IsClustered"),
                IsDeleted = GetBool(item, "IsDeleted")
            });
        }

        return services;
    }
    public async Task<IReadOnlyList<VmStorageInfo>> GetVmStorageAsync(string computerName,CancellationToken cancellationToken = default)
    {
        var result = await _powerShell.ExecutePipelineAsync(
            cancellationToken,
            (
                "Get-VM",
                new Dictionary<string, object?>
                {
                    ["ComputerName"] = computerName
                }
            ));

        var storage = new List<VmStorageInfo>(result.Count);

        foreach (var item in result)
        {
            storage.Add(new VmStorageInfo
            {
                VmName = GetString(item, "VMName"),
                HostName = GetString(item, "ComputerName", computerName),
                VMId = GetString(item, "VMId"),
                ParentCheckpointId = GetString(item, "ParentCheckpointId"),
                ParentCheckpointName = GetString(item, "ParentCheckpointName"),
                CheckpointFileLocation = GetString(item, "CheckpointFileLocation"),
                ConfigurationLocation = GetString(item, "ConfigurationLocation"),
                GuestStatePath = GetString(item, "GuestStatePath"),
                SmartPagingFileInUse = GetBool(item, "SmartPagingFileInUse"),
                SmartPagingFilePath = GetString(item, "SmartPagingFilePath"),
                SnapshotFileLocation = GetString(item, "SnapshotFileLocation"),
                Path = GetString(item, "Path"),
                SizeOfSystemFiles = GetLong(item, "SizeOfSystemFiles"),
                ParentSnapshotId = GetString(item, "ParentSnapshotId"),
                ParentSnapshotName = GetString(item, "ParentSnapshotName"),
                AutomaticStartAction = GetString(item, "AutomaticStartAction"),
                AutomaticStartDelay = GetInt(item, "AutomaticStartDelay"),
                AutomaticStopAction = GetString(item, "AutomaticStopAction"),
                AutomaticCriticalErrorAction = GetString(item, "AutomaticCriticalErrorAction"),
                AutomaticCriticalErrorActionTimeout =
                    GetInt(item, "AutomaticCriticalErrorActionTimeout"),
                AutomaticCheckpointsEnabled =
                    GetBool(item, "AutomaticCheckpointsEnabled"),
                State = GetString(item, "State"),
                Status = GetString(item, "Status"),
                CheckpointType = GetString(item, "CheckpointType"),
                ResourceMeteringEnabled =
                    GetBool(item, "ResourceMeteringEnabled"),
                EnhancedSessionTransportType =
                    GetString(item, "EnhancedSessionTransportType"),
                GuestStateIsolationType =
                    GetString(item, "GuestStateIsolationType"),
                VirtualMachineType =
                    GetString(item, "VirtualMachineType"),
                VirtualMachineSubType =
                    GetString(item, "VirtualMachineSubType"),
                Version = GetString(item, "Version"),
                GuestControlledCacheTypes =
                    GetBool(item, "GuestControlledCacheTypes"),
                LowMemoryMappedIoSpace =
                    GetLong(item, "LowMemoryMappedIoSpace"),
                HighMemoryMappedIoSpace =
                    GetLong(item, "HighMemoryMappedIoSpace"),
                HighMemoryMappedIoBaseAddress =
                    GetLong(item, "HighMemoryMappedIoBaseAddress"),
                LockOnDisconnect =
                    GetString(item, "LockOnDisconnect"),
                CreationTime = GetDateTime(item, "CreationTime"),
                IsDeleted = GetBool(item, "IsDeleted")
            });
        }

        return storage;
    }
    public async Task<IReadOnlyList<VmDiskInfo>> GetVmDisksAsync(string computerName,CancellationToken cancellationToken = default)
    {
        var result = await _powerShell.ExecutePipelineAsync(
            cancellationToken,
            (
                "Get-VM",
                new Dictionary<string, object?>
                {
                    ["ComputerName"] = computerName
                }
            ),
            ("Get-VMHardDiskDrive", null));

        var disks = new List<VmDiskInfo>(result.Count);

        foreach (var item in result)
        {
            disks.Add(new VmDiskInfo
            {
                VmName = GetString(item, "VMName"),
                HostName = GetString(item, "ComputerName", computerName),
                Path = GetString(item, "Path"),
                DiskNumber = GetString(item, "DiskNumber"),
                MaximumIOPS = GetLong(item, "MaximumIOPS"),
                MinimumIOPS = GetLong(item, "MinimumIOPS"),
                QoSPolicyID = GetString(item, "QoSPolicyID"),
                SupportPersistentReservations =GetBool(item, "SupportPersistentReservations"),
                WriteHardeningMethod = GetString(item, "WriteHardeningMethod"),
                ControllerLocation =GetInt(item, "ControllerLocation"),
                ControllerNumber =GetInt(item, "ControllerNumber"),
                ControllerType =GetString(item, "ControllerType"),
                Name = GetString(item, "Name"),
                PoolName = GetString(item, "PoolName")
            });
        }

        return disks;
    }
    public async Task<IReadOnlyList<VhdInfo>> GetVhdsAsync(string computerName,CancellationToken cancellationToken = default)
    {
        var diskResult = await _powerShell.ExecutePipelineAsync(
            cancellationToken,
            (
                "Get-VM",
                new Dictionary<string, object?>
                {
                    ["ComputerName"] = computerName
                }
            ),
            ("Get-VMHardDiskDrive", null));

        var vhds = new List<VhdInfo>();

        foreach (var disk in diskResult)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var path = GetString(disk, "Path");

            if (string.IsNullOrWhiteSpace(path))
            {
                continue;
            }

            var result = await _powerShell.ExecutePipelineAsync(
                cancellationToken,
                (
                    "Get-VHD",
                    new Dictionary<string, object?>
                    {
                        ["ComputerName"] = computerName,
                        ["Path"] = path
                    }
                ));

            foreach (var item in result)
            {
                vhds.Add(new VhdInfo
                {
                    HostName = computerName,
                    Path = GetString(item, "Path"),
                    VhdFormat = GetString(item, "VhdFormat"),
                    VhdType = GetString(item, "VhdType"),
                    FileSize = GetLong(item, "FileSize"),
                    Size = GetLong(item, "Size"),
                    MinimumSize = GetLong(item, "MinimumSize"),
                    LogicalSectorSize = GetLong(item, "LogicalSectorSize"),
                    PhysicalSectorSize = GetLong(item, "PhysicalSectorSize"),
                    BlockSize = GetLong(item, "BlockSize"),
                    ParentPath = GetString(item, "ParentPath"),
                    DiskIdentifier = GetString(item, "DiskIdentifier"),
                    FragmentationPercentage =
                        GetString(item, "FragmentationPercentage"),
                    Alignment = GetInt(item, "Alignment"),
                    Attached = GetBool(item, "Attached"),
                    DiskNumber = GetString(item, "DiskNumber"),
                    IsPMEMCompatible =
                        GetBool(item, "IsPMEMCompatible"),
                    AddressAbstractionType =
                        GetString(item, "AddressAbstractionType")
                });
            }
        }

        return vhds;
    }
    public async Task<IReadOnlyList<VmReplicationInfo>> GetVmReplicationAsync(string computerName,CancellationToken cancellationToken = default)
    {
        var result = await _powerShell.ExecutePipelineAsync(
            cancellationToken,
            (
                "Get-VMReplication",
                new Dictionary<string, object?>
                {
                    ["ComputerName"] = computerName
                }
            ));

        var replications = new List<VmReplicationInfo>();

        foreach (var item in result)
        {
            cancellationToken.ThrowIfCancellationRequested();

            replications.Add(new VmReplicationInfo
            {
                VmName = GetString(item, "VMName"),
                VMId = GetString(item, "VMId"),

                ReplicationState =
                    GetString(item, "ReplicationState"),

                ReplicationHealth =
                    GetString(item, "ReplicationHealth"),

                ReplicationMode =
                    GetString(item, "ReplicationMode"),

                PrimaryServer =
                    GetString(item, "PrimaryServer"),

                ReplicaServer =
                    GetString(item, "ReplicaServer"),

                ReplicaServerPort =
                    GetInt(item, "ReplicaServerPort"),

                AuthenticationType =
                    GetString(item, "AuthenticationType"),

                CertificateThumbprint =
                    GetString(item, "CertificateThumbprint"),

                CompressionEnabled =
                    GetBool(item, "CompressionEnabled"),

                AutoResynchronizeEnabled =
                    GetBool(item, "AutoResynchronizeEnabled"),

                AutoResynchronizeIntervalStart =
                    GetTimeSpan(item, "AutoResynchronizeIntervalStart"),

                AutoResynchronizeIntervalEnd =
                    GetTimeSpan(item, "AutoResynchronizeIntervalEnd"),

                ReplicationIntervalSec =
                    GetInt(item, "ReplicationIntervalSec"),

                FrequencySec =
                    GetInt(item, "FrequencySec"),

                LastReplicationTime =
                    GetDateTime(item, "LastReplicationTime"),

                CurrentReplicaTime =
                    GetDateTime(item, "CurrentReplicaTime"),

                LastSuccessfulReplicationTime =
                    GetDateTime(item, "LastSuccessfulReplicationTime"),

                FailedOver =
                    GetBool(item, "FailedOver"),

                TestFailoverInProcess =
                    GetBool(item, "TestFailoverInProcess"),

                TestFailoverTime =
                    GetDateTime(item, "TestFailoverTime"),

                TestFailoverVMName =
                    GetString(item, "TestFailoverVMName"),

                ReplicationHealthDetails =
                    GetStringList(item, "ReplicationHealthDetails"),

                IncludedDisks =
                    GetStringList(item, "IncludedDisks"),

                ExcludedDisks =
                    GetStringList(item, "ExcludedDisks"),

                RecoveryHistory =
                    GetInt(item, "RecoveryHistory"),

                ApplicationConsistentSnapshotFrequencyInHours =
                    GetInt(
                        item,
                        "ApplicationConsistentSnapshotFrequencyInHours"),

                ExtendedReplicationState =
                    GetString(item, "ExtendedReplicationState"),

                ExtendedReplicaServer =
                    GetString(item, "ExtendedReplicaServer"),

                ExtendedReplicaServerPort =
                    GetNullableInt(
                        item,
                        "ExtendedReplicaServerPort"),

                ExtendedAuthenticationType =
                    GetString(
                        item,
                        "ExtendedAuthenticationType"),

                ExtendedCertificateThumbprint =
                    GetString(
                        item,
                        "ExtendedCertificateThumbprint")
            });
        }

        return replications;
    }
    public async Task<IReadOnlyList<VmDvdInfo>> GetVmDvdsAsync(string computerName,CancellationToken cancellationToken = default)
    {
        var result = await _powerShell.ExecutePipelineAsync(
            cancellationToken,
            (
                "Get-VM",
                new Dictionary<string, object?>
                {
                    ["ComputerName"] = computerName
                }
            ),
            (
                "Get-VMDvdDrive",
                null
            ));

        var dvds = new List<VmDvdInfo>();

        foreach (var item in result)
        {
            cancellationToken.ThrowIfCancellationRequested();

            dvds.Add(new VmDvdInfo
            {
                VmName = GetString(item, "VMName"),
                HostName = GetString(item, "ComputerName", computerName),
                VMId = GetString(item, "VMId"),
                Id = GetString(item, "Id"),
                Path = GetString(item, "Path"),
                DvdMediaType = GetString(item, "DvdMediaType"),
                ControllerLocation = GetInt(item, "ControllerLocation"),
                ControllerNumber = GetInt(item, "ControllerNumber"),
                ControllerType = GetString(item, "ControllerType"),
                Name = GetString(item, "Name"),
                PoolName = GetString(item, "PoolName"),
                VMCheckpointId = GetString(item, "VMCheckpointId"),
                VMCheckpointName = GetString(item, "VMCheckpointName"),
                VMSnapshotId = GetString(item, "VMSnapshotId"),
                VMSnapshotName = GetString(item, "VMSnapshotName"),
                IsDeleted = GetBool(item, "IsDeleted")
            });
        }

        return dvds;
    }

    // Cluster Information
    public async Task<IReadOnlyList<ClusterInfo>> GetClustersAsync(string computerName,CancellationToken cancellationToken = default)
    {
        var result = await _powerShell.ExecutePipelineAsync(
            cancellationToken,
            (
                "Get-Cluster",
                new Dictionary<string, object?>
                {
                    ["Name"] = computerName
                }
            ));

        var clusters = new List<ClusterInfo>();

        foreach (var item in result)
        {
            cancellationToken.ThrowIfCancellationRequested();

            clusters.Add(new ClusterInfo
            {
                Name = GetString(item, "Name"),
                Domain = GetString(item, "Domain"),
                Id = GetString(item, "Id"),
                SharedVolumesRoot = GetString(item, "SharedVolumesRoot"),
                AddEvictDelay = GetInt(item, "AddEvictDelay"),
                BackupInProgress = GetInt(item, "BackupInProgress"),
                BlockCacheSize = GetInt(item, "BlockCacheSize"),
                ClusSvcDataPartitionMounted = GetInt(item, "ClusSvcDataPartitionMounted"),
                ClusterEnforcedAntiAffinity = GetInt(item, "ClusterEnforcedAntiAffinity"),
                ClusterFunctionalLevel = GetInt(item, "ClusterFunctionalLevel"),
                ClusterGroupWaitDelay = GetInt(item, "ClusterGroupWaitDelay"),
                ClusterLogLevel = GetInt(item, "ClusterLogLevel"),
                ClusterLogSize = GetInt(item, "ClusterLogSize"),
                CsvBalancedValidationThresholdInHours = GetInt(item, "CsvBalancedValidationThresholdInHours"),
                CsvDirectIoOpt = GetInt(item, "CsvDirectIoOpt"),
                CsvFltValidationThresholdInHours = GetInt(item, "CsvFltValidationThresholdInHours"),
                CustomDeadlockDetectionTimeout = GetInt(item, "CustomDeadlockDetectionTimeout"),
                DatabaseReadWriteMode = GetInt(item, "DatabaseReadWriteMode"),
                DefaultNetworkRole = GetInt(item, "DefaultNetworkRole"),
                Description = GetString(item, "Description"),
                DrainOnShutdown = GetInt(item, "DrainOnShutdown"),
                DumpPolicy = GetLong(item, "DumpPolicy"),
                DynamicQuorum = GetInt(item, "DynamicQuorum"),
                EnableAutomaticMetric = GetInt(item, "EnableAutomaticMetric"),
                AutoAssignNodeSite = GetInt(item, "AutoAssignNodeSite"),
                AutoBalancerMode = GetInt(item, "AutoBalancerMode"),
                AutoBalancerLevel = GetInt(item, "AutoBalancerLevel"),
                FixQuorum = GetInt(item, "FixQuorum"),
                GracePeriodOnUnbalanced = GetInt(item, "GracePeriodOnUnbalanced"),
                GroupAdministrativeDelay = GetInt(item, "GroupAdministrativeDelay"),
                HangRecoveryAction = GetInt(item, "HangRecoveryAction"),
                IgnorePersistentStateOnStartup = GetInt(item, "IgnorePersistentStateOnStartup"),
                LogResourceControls = GetInt(item, "LogResourceControls"),
                LowerQuorumPriorityNodeId = GetInt(item, "LowerQuorumPriorityNodeId"),
                MaxNumberOfNodes = GetInt(item, "MaxNumberOfNodes"),
                MessageBufferLength = GetInt(item, "MessageBufferLength"),
                MinimumNeverPreemptPriority = GetInt(item, "MinimumNeverPreemptPriority"),
                MinimumPreemptorPriority = GetInt(item, "MinimumPreemptorPriority"),
                NetftIPSecEnabled = GetInt(item, "NetftIPSecEnabled"),
                PlacementOptions = GetInt(item, "PlacementOptions"),
                PreventQuorum = GetInt(item, "PreventQuorum"),
                QuorumArbitrationTimeMax = GetInt(item, "QuorumArbitrationTimeMax"),
                QuorumLogFileSize = GetInt(item, "QuorumLogFileSize"),
                RequestReplyTimeout = GetInt(item, "RequestReplyTimeout"),
                ResiliencyDefaultPeriod = GetInt(item, "ResiliencyDefaultPeriod"),
                ResiliencyPeriodFilter = GetInt(item, "ResiliencyPeriodFilter"),
                ResourceDllDeadlockTimeout = GetInt(item, "ResourceDllDeadlockTimeout"),
                RootMemoryReserved = GetLong(item, "RootMemoryReserved"),
                RouteHistoryLength = GetInt(item, "RouteHistoryLength"),
                S2DCacheBehavior = GetString(item, "S2DCacheBehavior"),
                S2DCacheFlashReservePercent = GetInt(item, "S2DCacheFlashReservePercent"),
                S2DCachePageSizeKBytes = GetInt(item, "S2DCachePageSizeKBytes"),
                S2DEnabled = GetInt(item, "S2DEnabled"),
                S2DIOLatencyThreshold = GetInt(item, "S2DIOLatencyThreshold"),
                S2DOptimizeFlashPoolThresholdPct = GetInt(item, "S2DOptimizeFlashPoolThresholdPct"),
                SameSubnetDelay = GetInt(item, "SameSubnetDelay"),
                SameSubnetThreshold = GetInt(item, "SameSubnetThreshold"),
                SharedVolumeBlockCacheSizeInMB = GetInt(item, "SharedVolumeBlockCacheSizeInMB"),
                SharedVolumeCompatibleFilters = GetStringList(item, "SharedVolumeCompatibleFilters"),
                SharedVolumeSecurityDescriptor = GetString(item, "SharedVolumeSecurityDescriptor"),
                ShutdownTimeoutInMinutes = GetInt(item, "ShutdownTimeoutInMinutes"),
                UseClientAccessNetworksForSharedVolumes = GetInt(item, "UseClientAccessNetworksForSharedVolumes"),
                WitnessDatabaseWriteTimeout = GetInt(item, "WitnessDatabaseWriteTimeout"),
                WitnessDynamicWeight = GetInt(item, "WitnessDynamicWeight"),
                WitnessRestartInterval = GetInt(item, "WitnessRestartInterval"),
                CrossSiteDelay = GetInt(item, "CrossSiteDelay"),
                CrossSiteThreshold = GetInt(item, "CrossSiteThreshold"),
                CrossSubnetDelay = GetInt(item, "CrossSubnetDelay"),
                CrossSubnetThreshold = GetInt(item, "CrossSubnetThreshold"),
                PlumbAllCrossSubnetRoutes = GetInt(item, "PlumbAllCrossSubnetRoutes"),
                PreferredSite = GetString(item, "PreferredSite"),
                QuorumType = GetString(item, "QuorumType"),
                Status = GetString(item, "Status")
            });
        }

        return clusters;
    }





    // Helper methods to extract properties from PSObject

    private static string GetString(PSObject value, string propertyName, string defaultValue = "")
    {
        return value.Properties[propertyName]?.Value?.ToString() ?? defaultValue;
    }
    private static int GetInt(PSObject value, string propertyName)
    {
        var property = value.Properties[propertyName]?.Value;
        return property == null ? 0 : Convert.ToInt32(property);
    }
    private static long GetLong(PSObject value, string propertyName)
    {
        var property = value.Properties[propertyName]?.Value;
        return property == null ? 0L : Convert.ToInt64(property);
    }
    private static bool GetBool(PSObject value, string propertyName)
    {
        var property = value.Properties[propertyName]?.Value;
        return property != null && Convert.ToBoolean(property);
    }
    private static TimeSpan? GetNullableTimeSpan(PSObject value, string propertyName)
    {
        var property = value.Properties[propertyName]?.Value;

        if (property == null)
        {
            return null;
        }

        if (property is TimeSpan timeSpan)
        {
            return timeSpan;
        }

        return TimeSpan.TryParse(property.ToString(), out var parsed)
            ? parsed
            : null;
    }
    private static List<string> GetStringList(PSObject value, string propertyName)
    {
        var property = value.Properties[propertyName]?.Value;
        var result = new List<string>();

        if (property == null)
        {
            return result;
        }

        if (property is IEnumerable collection && property is not string)
        {
            foreach (var item in collection)
            {
                if (item != null && !string.IsNullOrWhiteSpace(item.ToString()))
                {
                    result.Add(item.ToString()!);
                }
            }

            return result;
        }

        var text = property.ToString();

        if (!string.IsNullOrWhiteSpace(text))
        {
            result.Add(text);
        }

        return result;
    }
    private static Guid GetGuid(PSObject value, string propertyName)
    {
        var property = value.Properties[propertyName]?.Value;

        if (property is Guid guid)
        {
            return guid;
        }

        return Guid.TryParse(property?.ToString(), out
        var parsed) ? parsed : Guid.Empty;
    }
    private static List<int> GetIntList(PSObject item, string propertyName)
    {
        var value = item.Properties[propertyName]?.Value;

        if (value is null)
        {
            return [];
        }

        if (value is System.Collections.IEnumerable enumerable &&
            value is not string)
        {
            return enumerable
                .Cast<object>()
                .Select(value => int.TryParse(
                    value?.ToString(),
                    out var number)
                    ? number
                    : 0)
                .Where(number => number != 0)
                .ToList();
        }

        return int.TryParse(
            value.ToString(),
            out var singleValue)
            ? [singleValue]
            : [];
    }
    private static string GetNestedString(PSObject item,string parentPropertyName,string childPropertyName)
    {
        var parent =
            item.Properties[parentPropertyName]?.Value as PSObject;

        if (parent == null)
        {
            return string.Empty;
        }

        return GetString(parent, childPropertyName);
    }
    private static string GetParentAdapterProperty( PSObject item,string propertyName)
    {
        var parentAdapter = GetString(item, "ParentAdapter");

        if (string.IsNullOrWhiteSpace(parentAdapter))
        {
            return string.Empty;
        }

        var pattern =
            $@"{System.Text.RegularExpressions.Regex.Escape(propertyName)}\s*=\s*'([^']*)'";

        var match = System.Text.RegularExpressions.Regex.Match(
            parentAdapter,
            pattern,
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        return match.Success
            ? match.Groups[1].Value
            : string.Empty;
    }
    private static DateTime? GetDateTime(PSObject value, string propertyName)
    {
        var property = value.Properties[propertyName]?.Value;

        if (property is null)
        {
            return null;
        }

        if (property is DateTime dateTime)
        {
            return dateTime;
        }

        return DateTime.TryParse(
            property.ToString(),
            out var parsed)
            ? parsed
            : null;
    }
    private static TimeSpan? GetTimeSpan(PSObject item,string propertyName)
    {
        var value = item.Properties[propertyName]?.Value;

        if (value == null)
        {
            return null;
        }

        if (value is TimeSpan timeSpan)
        {
            return timeSpan;
        }

        if (TimeSpan.TryParse(
                value.ToString(),
                out var parsed))
        {
            return parsed;
        }

        return null;
    }
    private static int? GetNullableInt(PSObject item,string propertyName)
    {
        var value = item.Properties[propertyName]?.Value;

        if (value == null)
        {
            return null;
        }

        if (value is int intValue)
        {
            return intValue;
        }

        if (int.TryParse(
                value.ToString(),
                out var parsed))
        {
            return parsed;
        }

        return null;
    }

}