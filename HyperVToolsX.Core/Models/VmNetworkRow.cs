namespace HyperVToolsX.Core.Models;

public class VmNetworkRow
{
    public string VmName { get; set; } = string.Empty;

    public string HostName { get; set; } = string.Empty;

    public string ClusterName { get; set; } = string.Empty;

    public string State { get; set; } = string.Empty;

    public string AdapterName { get; set; } = string.Empty;

    public string SwitchName { get; set; } = string.Empty;

    public string MacAddress { get; set; } = string.Empty;

    public string IpAddress { get; set; } = string.Empty;

    public string VlanMode { get; set; } = string.Empty;

    public string VlanList { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;
}