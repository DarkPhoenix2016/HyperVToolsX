namespace HyperVToolsX.Core.Models.Details;

public class VmNetworkAdapter
{
    public string VmName { get; set; } = string.Empty;
    public string HostName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string SwitchName { get; set; } = string.Empty;
    public string MacAddress { get; set; } = string.Empty;

    public List<string> IPv4Addresses { get; set; } = [];
    public List<string> IPv6Addresses { get; set; } = [];

    public string Status { get; set; } = string.Empty;

    public string IPv4AddressDisplay =>
        string.Join(", ", IPv4Addresses);

    public string IPv6AddressDisplay =>
        string.Join(", ", IPv6Addresses);
}