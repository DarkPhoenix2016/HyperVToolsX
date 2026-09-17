using HyperVToolsX.App.Views;
using HyperVToolsX.Core.Collection;
using HyperVToolsX.Core.Models;
using HyperVToolsX.Core.Models.Details;
using HyperVToolsX.Infrastructure.Collection;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using HyperVToolsX.App.Converters;
using HyperVToolsX.Core.Enums;

namespace HyperVToolsX.App;

public partial class MainWindow : Window
{
    private readonly InventoryCache _inventoryCache;

    private readonly ObservableCollection<HyperVVirtualMachine> _virtualMachines = [];
    private readonly ObservableCollection<VmProcessorInfo> _processors = [];
    private readonly ObservableCollection<VmMemoryInfo> _memories = [];

    private readonly ObservableCollection<VmNetworkAdapter> _networkAdapters = [];
    private readonly ObservableCollection<VmNetworkVlanInfo> _networkVlans = [];
    private readonly ObservableCollection<VmCheckpointInfo> _checkpoints = [];
    private readonly ObservableCollection<VmIntegrationServiceInfo> _integrationServices = [];
    private readonly ObservableCollection<VmStorageInfo> _vmStorage = [];
    private readonly ObservableCollection<VmDiskInfo> _disks = [];
    private readonly ObservableCollection<VhdInfo> _vhds = [];
    private readonly ObservableCollection<VmReplicationInfo> _replications = [];
    private readonly ObservableCollection<VmDvdInfo> _dvds = [];
    private readonly ObservableCollection<ClusterInfo> _clusters = [];



    private readonly ObservableCollection<HostInventoryRow> _hostInventoryRows = [];
    private readonly ObservableCollection<HyperVHost> _hosts = [];


    private ICollectionView? _vmCollectionView;
    private ICollectionView? _networkCollectionView;


    private TargetManagerView? _targetManagerView;

    public MainWindow()
    {
        InitializeComponent();

        _inventoryCache = new InventoryCache();

        ByteSizeConverter.CurrentUnit = SizeUnit.GB;
        GbUnitMenuItem.IsChecked = true;

        Loaded += MainWindow_Loaded;
    }
    private void MainWindow_Loaded(object sender,RoutedEventArgs e)
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
    private void TargetManagerView_CollectionCompleted( object? sender,EventArgs e)
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
        LoadStorageInfo(snapshot);
        LoadVhds(snapshot);
        LoadReplication(snapshot);
        LoadDvds(snapshot);
        LoadClusters(snapshot);
        LoadHostInventory(snapshot);
  

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
    private void LoadStorageInfo(InventorySnapshot snapshot)
    {
        _vmStorage.Clear();
        foreach (var storage in snapshot.VmStorage)
        {
            _vmStorage.Add(storage);
        }
        StorageDataGrid.ItemsSource = _vmStorage;
    }
    private void LoadDisks(InventorySnapshot snapshot)
    {
        _disks.Clear();

        foreach (var disk in snapshot.Disks)
        {
            _disks.Add(disk);
        }

        DiskDataGrid.ItemsSource = _disks;
    }
    private void LoadVhds(InventorySnapshot snapshot)
    {
        _vhds.Clear();
        foreach (var vhd in snapshot.Vhds)
        {
            _vhds.Add(vhd);
        }
        VhdDataGrid.ItemsSource = _vhds;
    }
    private void LoadReplication(InventorySnapshot snapshot)
    {
        _replications.Clear();

        foreach (var replication in snapshot.Replication)
        {
            _replications.Add(replication);
        }

        System.Diagnostics.Debug.WriteLine(
            $"REPLICATION UI LOAD: snapshot={snapshot.Replication.Count}, collection={_replications.Count}");

        ReplicationDataGrid.ItemsSource = _replications;
    }
    private void LoadDvds(InventorySnapshot snapshot)
    {
        _dvds.Clear();

        foreach (var dvd in snapshot.Dvds)
        {
            _dvds.Add(dvd);
        }

        System.Diagnostics.Debug.WriteLine(
            $"DVD UI LOAD: snapshot={snapshot.Dvds.Count}, collection={_dvds.Count}");

        DvdDataGrid.ItemsSource = _dvds;
    }
    private void LoadClusters(InventorySnapshot snapshot)
    {
        _clusters.Clear();

        foreach (var cluster in snapshot.Clusters)
        {
            _clusters.Add(cluster);
        }

        System.Diagnostics.Debug.WriteLine(
            $"CLUSTER UI LOAD: snapshot={snapshot.Clusters.Count}, collection={_clusters.Count}");

        ClusterDataGrid.ItemsSource = _clusters;
    }

    private void LoadHostInventory(InventorySnapshot snapshot)
    {
        _hostInventoryRows.Clear();

        foreach (var host in snapshot.Hosts)
        {
            var storage = snapshot.HostStorage
                .FirstOrDefault(x =>
                    string.Equals(
                        x.HostName,
                        host.Name,
                        StringComparison.OrdinalIgnoreCase)
                    ||
                    string.Equals(
                        x.ComputerName,
                        host.Name,
                        StringComparison.OrdinalIgnoreCase));

            var operatingSystem = snapshot.OperatingSystems
                .FirstOrDefault(x =>
                    string.Equals(
                        x.ComputerName,
                        host.Name,
                        StringComparison.OrdinalIgnoreCase));

            var row = new HostInventoryRow
            {
                // Host
                HostName = host.Name,
                Fqdn = host.Fqdn,
                ClusterName = host.ClusterName,
                IsClusterNode = host.IsClusterNode,
                IsConnected = host.IsConnected,

                // Hyper-V
                HyperVVersion = host.HyperVVersion,
                LogicalProcessorCount = host.LogicalProcessorCount,
                VirtualMachineCount = host.VirtualMachineCount,

                // Memory
                TotalMemoryBytes = host.TotalMemoryBytes,
                UsedMemoryBytes = host.UsedMemoryBytes,

                // Operating System
                OperatingSystem = operatingSystem?.Caption
                    ?? host.OperatingSystem,

                OSVersion = operatingSystem?.Version
                    ?? string.Empty,

                OSBuildNumber = operatingSystem?.BuildNumber
                    ?? string.Empty,

                OSArchitecture = operatingSystem?.OSArchitecture
                    ?? string.Empty,

                LastBootUpTime = operatingSystem?.LastBootUpTime,

                // OS Memory
                TotalVisibleMemorySizeKb =
                    operatingSystem?.TotalVisibleMemorySizeKb ?? 0,

                FreePhysicalMemoryKb =
                    operatingSystem?.FreePhysicalMemoryKb ?? 0,

                // Hyper-V Storage
                VirtualHardDiskPath =
                    storage?.VirtualHardDiskPath ?? string.Empty,

                VirtualMachinePath =
                    storage?.VirtualMachinePath ?? string.Empty,

                ParentSnapshotPath =
                    storage?.ParentSnapshotPath ?? string.Empty,

                // VM Migration
                MaximumStorageMigrations =
                    storage?.MaximumStorageMigrations ?? 0,

                MaximumVirtualMachineMigrations =
                    storage?.MaximumVirtualMachineMigrations ?? 0,

                VirtualMachineMigrationEnabled =
                    storage?.VirtualMachineMigrationEnabled ?? false,

                VirtualMachineMigrationAuthenticationType =
                    storage?.VirtualMachineMigrationAuthenticationType
                    ?? string.Empty,

                VirtualMachineMigrationPerformanceOption =
                    storage?.VirtualMachineMigrationPerformanceOption
                    ?? string.Empty,

                UseAnyNetworkForMigration =
                    storage?.UseAnyNetworkForMigration ?? false,

                // Hyper-V Settings
                EnableEnhancedSessionMode =
                    storage?.EnableEnhancedSessionMode ?? false,

                // Status
                IsDeleted =
                    storage?.IsDeleted ?? false
            };

            _hostInventoryRows.Add(row);
        }

        HostDataGrid.ItemsSource = _hostInventoryRows;

        System.Diagnostics.Debug.WriteLine(
            $"HOST UI LOAD: snapshot={snapshot.Hosts.Count}, rows={_hostInventoryRows.Count}");
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

        var assignedMemoryBytes = virtualMachines.Sum( vm => vm.MemoryAssigned);

        AssignedMemoryText.Text =ByteSizeConverter.FormatBytes(assignedMemoryBytes);

        TotalVmText.Text = totalVms.ToString();
        RunningVmText.Text = runningVms.ToString();
        PoweredOffVmText.Text = poweredOffVms.ToString();
        TotalCpuText.Text = totalCpus.ToString();
     

        NodesQueriedText.Text = $"{snapshot.HostCount}/{snapshot.HostCount}";

        CheckpointText.Text =snapshot.CheckpointCount.ToString();

        LastUpdatedText.Text = $"Last updated: {snapshot.CreatedAt:MM/dd/yyyy HH:mm:ss}";

        StatusText.Text =$"Inventory loaded — {snapshot.VirtualMachineCount} VM(s)";

        RowCountText.Text =$"{totalVms} rows";
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

    private void SizeUnitMenuItem_Click(object sender,RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.MenuItem menuItem)
        {
            return;
        }

        var selectedUnit = menuItem.Name switch
        {
            "BytesUnitMenuItem" => SizeUnit.Bytes,
            "KbUnitMenuItem" => SizeUnit.KB,
            "MbUnitMenuItem" => SizeUnit.MB,
            "GbUnitMenuItem" => SizeUnit.GB,
            "TbUnitMenuItem" => SizeUnit.TB,
            "PbUnitMenuItem" => SizeUnit.PB,
            _ => SizeUnit.GB
        };

        ByteSizeConverter.CurrentUnit = selectedUnit;

        BytesUnitMenuItem.IsChecked =
            selectedUnit == SizeUnit.Bytes;

        KbUnitMenuItem.IsChecked =
            selectedUnit == SizeUnit.KB;

        MbUnitMenuItem.IsChecked =
            selectedUnit == SizeUnit.MB;

        GbUnitMenuItem.IsChecked =
            selectedUnit == SizeUnit.GB;

        TbUnitMenuItem.IsChecked =
            selectedUnit == SizeUnit.TB;

        PbUnitMenuItem.IsChecked =
            selectedUnit == SizeUnit.PB;

        RefreshSizeDisplays();
    }
    private void RefreshSizeDisplays()
    {
        VmDataGrid.Items.Refresh();
        MemoryDataGrid.Items.Refresh();
        StorageDataGrid.Items.Refresh();
        DiskDataGrid.Items.Refresh();
        VhdDataGrid.Items.Refresh();
        CheckpointDataGrid.Items.Refresh();
        HostDataGrid.Items.Refresh();
        ClusterDataGrid.Items.Refresh();

        UpdateSummary(_inventoryCache.GetSnapshot());
    }



}