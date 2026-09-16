using HyperVToolsX.App.Views;
using HyperVToolsX.Core.Collection;
using HyperVToolsX.Core.Models;
using HyperVToolsX.Core.Models.Details;
using HyperVToolsX.Infrastructure.Collection;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Data;

namespace HyperVToolsX.App;

public partial class MainWindow : Window
{
    private readonly InventoryCache _inventoryCache;

    private readonly ObservableCollection<HyperVVirtualMachine> _virtualMachines = [];
    private readonly ObservableCollection<VmProcessorInfo> _processors = [];
    private readonly ObservableCollection<VmMemoryInfo> _memories = [];
    private readonly ObservableCollection<VmDiskInfo> _disks = [];
    private readonly ObservableCollection<VmNetworkAdapter> _networkAdapters = [];
    private readonly ObservableCollection<VmNetworkVlanInfo> _networkVlans = [];
    private readonly ObservableCollection<VmCheckpointInfo> _checkpoints = [];
    private readonly ObservableCollection<VmIntegrationServiceInfo> _integrationServices = [];


    private readonly ObservableCollection<HyperVHost> _hosts = [];


    private ICollectionView? _vmCollectionView;
    private ICollectionView? _networkCollectionView;


    private TargetManagerView? _targetManagerView;

    public MainWindow()
    {
        InitializeComponent();

        _inventoryCache = new InventoryCache();

        Loaded += MainWindow_Loaded;
    }

    private void MainWindow_Loaded(
        object sender,
        RoutedEventArgs e)
    {
        OpenTargetManager();
    }

    private void OpenTargetManager()
    {
        var targetManagerView = new TargetManagerView(_inventoryCache);

        _targetManagerView = targetManagerView;

        targetManagerView.CollectionCompleted +=
            TargetManagerView_CollectionCompleted;

        var targetManagerWindow = new Window
        {
            Title = "HyperVToolsX - Target Manager",
            Width = 1100,
            Height = 700,
            MinWidth = 900,
            MinHeight = 600,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = this,
            Content = targetManagerView
        };

        targetManagerWindow.ShowDialog();
    }

    private void TargetManagerView_CollectionCompleted(
        object? sender,
        EventArgs e)
    {
        LoadCachedInventory();
    }

    private void LoadCachedInventory()
    {
        var snapshot = _inventoryCache.GetSnapshot();

        LoadVirtualMachines(snapshot);
        LoadProcessors(snapshot);
        LoadMemories(snapshot);
        LoadDisks(snapshot);
        LoadNetworkAdapters(snapshot);
        LoadNetworkVlans(snapshot);
        LoadCheckpoints(snapshot);
        LoadIntegrationServiceInfo(snapshot);


        LoadHosts(snapshot);

        PopulateFilters();

        UpdateSummary(snapshot);
    }

    private void LoadVirtualMachines( InventorySnapshot snapshot)
    {
        _virtualMachines.Clear();

        foreach (var vm in snapshot.VirtualMachines)
        {
            _virtualMachines.Add(vm);
        }

        _vmCollectionView =
            CollectionViewSource.GetDefaultView(_virtualMachines);

        _vmCollectionView.Filter = FilterVm;

        VmDataGrid.ItemsSource = _vmCollectionView;

        RefreshVmFilter();
    }

    private void LoadProcessors( InventorySnapshot snapshot)
    {
        _processors.Clear();

        foreach (var processor in snapshot.Processors)
        {
            _processors.Add(processor);
        }

        CpuDataGrid.ItemsSource = _processors;
    }

    private void LoadMemories( InventorySnapshot snapshot)
    {
        _memories.Clear();

        foreach (var memory in snapshot.Memories)
        {
            _memories.Add(memory);
        }

        MemoryDataGrid.ItemsSource = _memories;
    }

    private void LoadDisks( InventorySnapshot snapshot)
    {
        _disks.Clear();

        foreach (var disk in snapshot.Disks)
        {
            _disks.Add(disk);
        }

        DiskDataGrid.ItemsSource = _disks;
    }

    private void LoadNetworkAdapters(InventorySnapshot snapshot)
    {
        _networkAdapters.Clear();

        foreach (var adapter in snapshot.NetworkAdapters)
        {
            _networkAdapters.Add(adapter);
        }

        _networkCollectionView =
            CollectionViewSource.GetDefaultView(_networkAdapters);

        _networkCollectionView.Filter = FilterNetwork;

        NetworkDataGrid.ItemsSource =
            _networkCollectionView;

        RefreshNetworkFilter();
    }

    private void LoadNetworkVlans(InventorySnapshot snapshot)
    {
        _networkVlans.Clear();

        foreach (var vlan in snapshot.NetworkVlans)
        {
            _networkVlans.Add(vlan);
        }

        VlanDataGrid.ItemsSource = _networkVlans;
    }

    private void LoadHosts(  InventorySnapshot snapshot)
    {
        _hosts.Clear();

        foreach (var host in snapshot.Hosts)
        {
            _hosts.Add(host);
        }

        HostDataGrid.ItemsSource = _hosts;
    }
    private void LoadCheckpoints(InventorySnapshot snapshot)
    {
        _checkpoints.Clear();

        foreach (var checkpoint in snapshot.Checkpoints)
        {
            _checkpoints.Add(checkpoint);
        }

        CheckpointDataGrid.ItemsSource = _checkpoints;
    }
    private void LoadIntegrationServiceInfo(InventorySnapshot snapshot)
    {
        _integrationServices.Clear();

        foreach (var service in snapshot.IntegrationServices)
        {
            _integrationServices.Add(service);
        }

        IntegrationDataGrid.ItemsSource = _integrationServices;
    }


    // Ui Functions
    private bool FilterVm(object obj)
    {
        if (obj is not HyperVVirtualMachine vm)
        {
            return false;
        }

        var search = VmSearchTextBox.Text.Trim();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var matches =
                vm.Name.Contains(
                    search,
                    StringComparison.OrdinalIgnoreCase)
                || vm.HostName.Contains(
                    search,
                    StringComparison.OrdinalIgnoreCase)
                || vm.VMId.ToString().Contains(
                    search,
                    StringComparison.OrdinalIgnoreCase)
                || vm.ClusterName.Contains(
                    search,
                    StringComparison.OrdinalIgnoreCase);

            if (!matches)
            {
                return false;
            }
        }

        var selectedHost =
            HostFilterComboBox.SelectedItem as string;

        if (!string.IsNullOrWhiteSpace(selectedHost) &&
            selectedHost != "All" &&
            !vm.HostName.Equals(
                selectedHost,
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var selectedState =
            StateFilterComboBox.SelectedItem as string;

        if (!string.IsNullOrWhiteSpace(selectedState) &&
            selectedState != "All" &&
            !vm.State.Equals(
                selectedState,
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var selectedCluster =
            ClusterFilterComboBox.SelectedItem as string;

        if (!string.IsNullOrWhiteSpace(selectedCluster) &&
            selectedCluster != "All" &&
            !vm.ClusterName.Equals(
                selectedCluster,
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    private bool FilterNetwork(object obj)
    {
        if (obj is not VmNetworkAdapter adapter)
        {
            return false;
        }

        var search = VmSearchTextBox.Text.Trim();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var matches =
                adapter.VmName.Contains(
                    search,
                    StringComparison.OrdinalIgnoreCase)
                || adapter.HostName.Contains(
                    search,
                    StringComparison.OrdinalIgnoreCase)
                || adapter.Name.Contains(
                    search,
                    StringComparison.OrdinalIgnoreCase)
                || adapter.SwitchName.Contains(
                    search,
                    StringComparison.OrdinalIgnoreCase)
                || adapter.MacAddress.Contains(
                    search,
                    StringComparison.OrdinalIgnoreCase)
                || adapter.IPv4AddressDisplay.Contains(
                    search,
                    StringComparison.OrdinalIgnoreCase)
                || adapter.IPv6AddressDisplay.Contains(
                    search,
                    StringComparison.OrdinalIgnoreCase);

            if (!matches)
            {
                return false;
            }
        }

        var selectedHost =
            HostFilterComboBox.SelectedItem as string;

        if (!string.IsNullOrWhiteSpace(selectedHost) &&
            selectedHost != "All" &&
            !adapter.HostName.Equals(
                selectedHost,
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    private void PopulateFilters()
    {
        var hosts = _virtualMachines
            .Select(vm => vm.HostName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name)
            .ToList();

        HostFilterComboBox.ItemsSource =
            new[] { "All" }.Concat(hosts).ToList();

        var states = _virtualMachines
            .Select(vm => vm.State)
            .Where(state => !string.IsNullOrWhiteSpace(state))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(state => state)
            .ToList();

        StateFilterComboBox.ItemsSource =
            new[] { "All" }.Concat(states).ToList();

        var clusters = _virtualMachines
            .Select(vm => vm.ClusterName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name)
            .ToList();

        ClusterFilterComboBox.ItemsSource =
            new[] { "All" }.Concat(clusters).ToList();

        HostFilterComboBox.SelectedIndex = 0;
        StateFilterComboBox.SelectedIndex = 0;
        ClusterFilterComboBox.SelectedIndex = 0;
    }

    private void VmSearchTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        VmSearchPlaceholder.Visibility =
            string.IsNullOrWhiteSpace(VmSearchTextBox.Text)
                ? Visibility.Visible
                : Visibility.Collapsed;

        RefreshVmFilter();
    }

    private void HostFilterComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        RefreshVmFilter();
    }

    private void StateFilterComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        RefreshVmFilter();
    }

    private void ClusterFilterComboBox_SelectionChanged( object sender,System.Windows.Controls.SelectionChangedEventArgs e)
    {
        RefreshVmFilter();
    }

    private void RefreshVmFilter()
    {
        _vmCollectionView?.Refresh();
        _networkCollectionView?.Refresh();

        if (_vmCollectionView != null)
        {
            VmSummaryCountText.Text =
                $"Showing {_vmCollectionView.Cast<HyperVVirtualMachine>().Count()} " +
                $"of {_virtualMachines.Count} VMs";
        }

        if (_networkCollectionView != null)
        {
            NetworkSummaryCountText.Text =
                $"Showing {_networkCollectionView.Cast<VmNetworkAdapter>().Count()} " +
                $"of {_networkAdapters.Count} network adapters";
        }
    }

    private void RefreshNetworkFilter()
    {
        _networkCollectionView?.Refresh();

        if (_networkCollectionView != null)
        {
            NetworkSummaryCountText.Text =
                $"Showing {_networkCollectionView.Cast<VmNetworkAdapter>().Count()} " +
                $"of {_networkAdapters.Count} network adapters";
        }
    }

    private void UpdateSummary(InventorySnapshot snapshot)
    {
        var virtualMachines = snapshot.VirtualMachines;

        var totalVms = virtualMachines.Count;

        var runningVms = virtualMachines.Count(vm =>
            vm.State.Equals(
                "Running",
                StringComparison.OrdinalIgnoreCase));

        var poweredOffVms = virtualMachines.Count(vm =>
            vm.State.Equals(
                "Off",
                StringComparison.OrdinalIgnoreCase));

        var totalCpus =
            virtualMachines.Sum(vm => vm.ProcessorCount);

        var assignedMemoryGb =
            virtualMachines.Sum(vm => vm.MemoryAssigned)
            / 1024.0
            / 1024.0
            / 1024.0;

        TotalVmText.Text = totalVms.ToString();
        RunningVmText.Text = runningVms.ToString();
        PoweredOffVmText.Text = poweredOffVms.ToString();
        TotalCpuText.Text = totalCpus.ToString();
        AssignedMemoryText.Text =
            $"{assignedMemoryGb:0.##} GB";

        NodesQueriedText.Text =
            $"{snapshot.HostCount}/{snapshot.HostCount}";

        CheckpointText.Text =
            snapshot.CheckpointCount.ToString();

        LastUpdatedText.Text =
            $"Last updated: {snapshot.CreatedAt:MM/dd/yyyy HH:mm:ss}";

        StatusText.Text =
            $"Inventory loaded — {snapshot.VirtualMachineCount} VM(s)";

        RowCountText.Text =
            $"{totalVms} rows";
    }

    private void DisconnectSelectedMenuItem_Click(object sender, RoutedEventArgs e)
    {
        _targetManagerView?.DisconnectSelected();
    }

    private void DisconnectAllMenuItem_Click(object sender, RoutedEventArgs e)
    {
        _targetManagerView?.DisconnectAll();
    }

    private void ConnectMenuItem_Click(object sender,RoutedEventArgs e)
    {
        OpenTargetManager();
    }
}