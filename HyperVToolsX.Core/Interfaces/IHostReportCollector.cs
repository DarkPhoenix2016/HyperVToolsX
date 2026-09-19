using HyperVToolsX.Core.Models.Reports;

namespace HyperVToolsX.Core.Interfaces;

public interface IHostReportCollector
{
    Task<HostReport> CollectAsync(
        string computerName,
        string clusterName = "",
        CancellationToken cancellationToken = default);
}