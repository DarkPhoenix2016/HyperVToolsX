namespace HyperVToolsX.Core.Models.Details;

public class VmNetworkVlanInfo
{
    public string VmName { get; set; } = string.Empty;

    public string AdapterName { get; set; } = string.Empty;

    public string OperationMode { get; set; } = string.Empty;

    public int AccessVlanId { get; set; }

    public int NativeVlanId { get; set; }

    public List<int> AllowedVlanIdList { get; set; } = [];

    public string AllowedVlanIdListString { get; set; } = string.Empty;

    public int PrivateVlanMode { get; set; }

    public int PrimaryVlanId { get; set; }

    public int SecondaryVlanId { get; set; }

    public List<int> SecondaryVlanIdList { get; set; } = [];

    public string SecondaryVlanIdListString { get; set; } = string.Empty;


    public bool IsTemplate { get; set; }
}