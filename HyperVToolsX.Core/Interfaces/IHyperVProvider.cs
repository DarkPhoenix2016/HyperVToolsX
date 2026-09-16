using HyperVToolsX.Core.Models;
using HyperVToolsX.Core.Models.Details;

namespace HyperVToolsX.Core.Interfaces;

public interface IHyperVProvider
{
    Task<HyperVHost> GetHostAsync(string computerName, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<HyperVVirtualMachine>> GetVirtualMachinesAsync(string computerName, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<VmNetworkAdapter>> GetNetworkAdaptersAsync(string computerName, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<VmProcessorInfo>> GetVmProcessorsAsync(string computerName, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<VmMemoryInfo>> GetVmMemoryAsync(string computerName, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<VmDiskInfo>> GetVmDisksAsync(string computerName,CancellationToken cancellationToken = default);
    Task<IReadOnlyList<VmNetworkVlanInfo>> GetVmNetworkVlansAsync(string computerName,CancellationToken cancellationToken = default);
    Task<IReadOnlyList<VmCheckpointInfo>> GetVmCheckpointsAsync(string computerName,CancellationToken cancellationToken = default);
    Task<IReadOnlyList<VmIntegrationServiceInfo>> GetVmIntegrationServicesAsync(string computerName,CancellationToken cancellationToken = default);
    Task<IReadOnlyList<VmStorageInfo>> GetVmStorageAsync(string computerName,CancellationToken cancellationToken = default);





}