using HyperVToolsX.App.Converters;
using HyperVToolsX.App.Views;
using HyperVToolsX.Core.Collection;
using HyperVToolsX.Core.Enums;
using HyperVToolsX.Core.Interfaces;
using HyperVToolsX.Core.Models;
using HyperVToolsX.Core.Models.Details;
using HyperVToolsX.Infrastructure.Collection;
using HyperVToolsX.Infrastructure.HyperV;
using HyperVToolsX.Infrastructure.PowerShellEngine;
using HyperVToolsX.Infrastructure.Validation;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace HyperVToolsX.App;

public partial class MainWindow : Window
{
    private readonly InventoryCache _inventoryCache;
    private readonly ITargetManager _targetManager;
    private readonly ITargetValidator _targetValidator;
    private readonly CollectionOrchestrator _collectionOrchestrator;

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

        // Composition root: build the shared object graph once here instead of
        // letting each view construct its own copies of these collaborators.
        // Composition root: build the shared object graph once here.
        var powerShell = new PowerShellExecutor();
        var hyperVProvider = new HyperVProvider(powerShell);
        var inventoryCollector = new RemoteInventoryCollector(powerShell);

        _inventoryCache = new InventoryCache();
        _targetManager = new TargetManager();
        _targetValidator = new TargetValidator(powerShell);

        _collectionOrchestrator = new CollectionOrchestrator(
            _targetValidator,
            inventoryCollector,
            _inventoryCache);

        ByteSizeConverter.CurrentUnit = SizeUnit.GB;
        GbUnitMenuItem.IsChecked = true;

        Loaded += MainWindow_Loaded;
        PreviewKeyDown += MainWindow_PreviewKeyDown;
    }

    // =========================================================
    // KEYBOARD SHORTCUTS
    // =========================================================

    private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.F5:
                RefreshMenuItem_Click(sender, e);
                e.Handled = true;
                break;

            case Key.N when Keyboard.Modifiers == ModifierKeys.Control:
                ConnectMenuItem_Click(sender, e);
                e.Handled = true;
                break;

            case Key.E when Keyboard.Modifiers == ModifierKeys.Control:
                ExportToExcelMenuItem_Click(sender, e);
                e.Handled = true;
                break;

            case Key.F when Keyboard.Modifiers == ModifierKeys.Control:
                VmSearchTextBox.Focus();
                VmSearchTextBox.SelectAll();
                e.Handled = true;
                break;
        }
    }

    private void VmSearchTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            VmSearchTextBox.Clear();
            e.Handled = true;
        }
    }

    private void ClearSearchButton_Click(object sender, RoutedEventArgs e)
    {
        VmSearchTextBox.Clear();
        VmSearchTextBox.Focus();
    }

    // =========================================================
    // TARGET MANAGER
    // =========================================================

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        OpenTargetManager();
    }

    private void OpenTargetManager()
    {
        var targetManagerView = new TargetManagerView(
            _targetManager,
            _targetValidator,
            _collectionOrchestrator,
            _inventoryCache);

        _targetManagerView = targetManagerView;
        targetManagerView.CollectionCompleted += TargetManagerView_CollectionCompleted;

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

    private void TargetManagerView_CollectionCompleted(object? sender, EventArgs e)
    {
        LoadCachedInventory();
    }

    private void ConnectMenuItem_Click(object sender, RoutedEventArgs e)
    {
        OpenTargetManager();
    }

    private void DisconnectSelectedMenuItem_Click(object sender, RoutedEventArgs e)
    {
        _targetManagerView?.DisconnectSelected();
    }

    private void DisconnectAllMenuItem_Click(object sender, RoutedEventArgs e)
    {
        _targetManagerView?.DisconnectAll();
    }

    // =========================================================
    // INVENTORY LOADING
    // =========================================================

    private void LoadCachedInventory()
    {
        SetBusy(true, "Loading inventory...");

        try
        {
            var snapshot = _inventoryCache.GetSnapshot();

            LoadVirtualMachines(snapshot);
            LoadCollection(_processors, snapshot.Processors, CpuDataGrid);
            LoadCollection(_memories, snapshot.Memories, MemoryDataGrid);
            LoadCollection(_disks, snapshot.Disks, DiskDataGrid);
            LoadNetworkAdapters(snapshot);
            LoadCollection(_networkVlans, snapshot.NetworkVlans, VlanDataGrid);
            LoadCollection(_checkpoints, snapshot.Checkpoints, CheckpointDataGrid);
            LoadCollection(_integrationServices, snapshot.IntegrationServices, IntegrationDataGrid);
            LoadCollection(_vmStorage, snapshot.VmStorage, StorageDataGrid);
            LoadCollection(_vhds, snapshot.Vhds, VhdDataGrid);
            LoadCollection(_replications, snapshot.Replication, ReplicationDataGrid);
            LoadCollection(_dvds, snapshot.Dvds, DvdDataGrid);
            LoadCollection(_clusters, snapshot.Clusters, ClusterDataGrid);
            LoadHostInventory(snapshot);

            PopulateFilters();
            UpdateSummary(snapshot);
        }
        finally
        {
            SetBusy(false);
        }
    }

    /// <summary>
    /// Shared implementation for the tabs that just mirror a snapshot list
    /// straight into a grid with no extra joining/filtering logic. Replaces
    /// what used to be a dozen near-identical Load*() methods.
    /// </summary>
    private static void LoadCollection<T>(
        ObservableCollection<T> target,
        IEnumerable<T> source,
        DataGrid grid)
    {
        target.Clear();

        foreach (var item in source)
        {
            target.Add(item);
        }

        grid.ItemsSource = target;
    }

    private void LoadVirtualMachines(InventorySnapshot snapshot)
    {
        _virtualMachines.Clear();

        foreach (var vm in snapshot.VirtualMachines)
        {
            _virtualMachines.Add(vm);
        }

        _vmCollectionView = CollectionViewSource.GetDefaultView(_virtualMachines);
        _vmCollectionView.Filter = FilterVm;

        VmDataGrid.ItemsSource = _vmCollectionView;

        RefreshVmFilter();
    }

    private void LoadNetworkAdapters(InventorySnapshot snapshot)
    {
        _networkAdapters.Clear();

        foreach (var adapter in snapshot.NetworkAdapters)
        {
            _networkAdapters.Add(adapter);
        }

        _networkCollectionView = CollectionViewSource.GetDefaultView(_networkAdapters);
        _networkCollectionView.Filter = FilterNetwork;

        NetworkDataGrid.ItemsSource = _networkCollectionView;

        RefreshNetworkFilter();
    }

    private void LoadHostInventory(InventorySnapshot snapshot)
    {
        _hostInventoryRows.Clear();

        foreach (var host in snapshot.Hosts)
        {
            var storage = snapshot.HostStorage.FirstOrDefault(x =>
                string.Equals(x.HostName, host.Name, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(x.ComputerName, host.Name, StringComparison.OrdinalIgnoreCase));

            var operatingSystem = snapshot.OperatingSystems.FirstOrDefault(x =>
                string.Equals(x.ComputerName, host.Name, StringComparison.OrdinalIgnoreCase));

            _hostInventoryRows.Add(new HostInventoryRow
            {
                HostName = host.Name,
                Fqdn = host.Fqdn,
                ClusterName = host.ClusterName,
                IsClusterNode = host.IsClusterNode,
                IsConnected = host.IsConnected,

                HyperVVersion = host.HyperVVersion,
                LogicalProcessorCount = host.LogicalProcessorCount,
                VirtualMachineCount = host.VirtualMachineCount,

                TotalMemoryBytes = host.TotalMemoryBytes,
                UsedMemoryBytes = host.UsedMemoryBytes,

                OperatingSystem = operatingSystem?.Caption ?? host.OperatingSystem,
                OSVersion = operatingSystem?.Version ?? string.Empty,
                OSBuildNumber = operatingSystem?.BuildNumber ?? string.Empty,
                OSArchitecture = operatingSystem?.OSArchitecture ?? string.Empty,
                LastBootUpTime = operatingSystem?.LastBootUpTime,

                TotalVisibleMemorySizeKb = operatingSystem?.TotalVisibleMemorySizeKb ?? 0,
                FreePhysicalMemoryKb = operatingSystem?.FreePhysicalMemoryKb ?? 0,

                VirtualHardDiskPath = storage?.VirtualHardDiskPath ?? string.Empty,
                VirtualMachinePath = storage?.VirtualMachinePath ?? string.Empty,
                ParentSnapshotPath = storage?.ParentSnapshotPath ?? string.Empty,

                MaximumStorageMigrations = storage?.MaximumStorageMigrations ?? 0,
                MaximumVirtualMachineMigrations = storage?.MaximumVirtualMachineMigrations ?? 0,
                VirtualMachineMigrationEnabled = storage?.VirtualMachineMigrationEnabled ?? false,
                VirtualMachineMigrationAuthenticationType =
                    storage?.VirtualMachineMigrationAuthenticationType ?? string.Empty,
                VirtualMachineMigrationPerformanceOption =
                    storage?.VirtualMachineMigrationPerformanceOption ?? string.Empty,
                UseAnyNetworkForMigration = storage?.UseAnyNetworkForMigration ?? false,

                EnableEnhancedSessionMode = storage?.EnableEnhancedSessionMode ?? false,
                IsDeleted = storage?.IsDeleted ?? false
            });
        }

        HostDataGrid.ItemsSource = _hostInventoryRows;
    }

    // =========================================================
    // FILTERING
    // =========================================================

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
                vm.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                vm.HostName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                vm.VMId.ToString().Contains(search, StringComparison.OrdinalIgnoreCase) ||
                vm.ClusterName.Contains(search, StringComparison.OrdinalIgnoreCase);

            if (!matches)
            {
                return false;
            }
        }

        if (!MatchesComboFilter(HostFilterComboBox, vm.HostName))
        {
            return false;
        }

        if (!MatchesComboFilter(StateFilterComboBox, vm.State))
        {
            return false;
        }

        if (!MatchesComboFilter(ClusterFilterComboBox, vm.ClusterName))
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
                adapter.VmName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                adapter.HostName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                adapter.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                adapter.SwitchName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                adapter.MacAddress.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                adapter.IPv4AddressDisplay.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                adapter.IPv6AddressDisplay.Contains(search, StringComparison.OrdinalIgnoreCase);

            if (!matches)
            {
                return false;
            }
        }

        return MatchesComboFilter(HostFilterComboBox, adapter.HostName);
    }

    private static bool MatchesComboFilter(ComboBox comboBox, string value)
    {
        var selected = comboBox.SelectedItem as string;

        return string.IsNullOrWhiteSpace(selected) ||
               selected == "All" ||
               value.Equals(selected, StringComparison.OrdinalIgnoreCase);
    }

    private void PopulateFilters()
    {
        SetComboBoxItems(HostFilterComboBox, _virtualMachines.Select(vm => vm.HostName));
        SetComboBoxItems(StateFilterComboBox, _virtualMachines.Select(vm => vm.State));
        SetComboBoxItems(ClusterFilterComboBox, _virtualMachines.Select(vm => vm.ClusterName));
    }

    private static void SetComboBoxItems(ComboBox comboBox, IEnumerable<string> values)
    {
        var distinctValues = values
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(v => v)
            .ToList();

        comboBox.ItemsSource = new[] { "All" }.Concat(distinctValues).ToList();
        comboBox.SelectedIndex = 0;
    }

    private void VmSearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var hasText = !string.IsNullOrWhiteSpace(VmSearchTextBox.Text);

        VmSearchPlaceholder.Visibility = hasText ? Visibility.Collapsed : Visibility.Visible;
        ClearSearchButton.Visibility = hasText ? Visibility.Visible : Visibility.Collapsed;

        RefreshVmFilter();
    }

    private void HostFilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) => RefreshVmFilter();

    private void StateFilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) => RefreshVmFilter();

    private void ClusterFilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) => RefreshVmFilter();

    private void RefreshVmFilter()
    {
        _vmCollectionView?.Refresh();
        _networkCollectionView?.Refresh();

        UpdateVisibleCounts();
    }

    private void RefreshNetworkFilter()
    {
        _networkCollectionView?.Refresh();
        UpdateVisibleCounts();
    }

    private void UpdateVisibleCounts()
    {
        if (_vmCollectionView != null)
        {
            var visible = _vmCollectionView.Cast<object>().Count();
            VmSummaryCountText.Text = $"Showing {visible} of {_virtualMachines.Count} VMs";
            RowCountText.Text = $"{visible} rows";
        }

        if (_networkCollectionView != null)
        {
            var visible = _networkCollectionView.Cast<object>().Count();
            NetworkSummaryCountText.Text = $"Showing {visible} of {_networkAdapters.Count} network adapters";
        }
    }

    // =========================================================
    // SUMMARY
    // =========================================================

    private void UpdateSummary(InventorySnapshot snapshot)
    {
        var virtualMachines = snapshot.VirtualMachines;

        var totalVms = virtualMachines.Count;
        var runningVms = virtualMachines.Count(vm => vm.State.Equals("Running", StringComparison.OrdinalIgnoreCase));
        var poweredOffVms = virtualMachines.Count(vm => vm.State.Equals("Off", StringComparison.OrdinalIgnoreCase));
        var totalCpus = virtualMachines.Sum(vm => vm.ProcessorCount);
        var assignedMemoryBytes = virtualMachines.Sum(vm => vm.MemoryAssigned);

        AssignedMemoryText.Text = ByteSizeConverter.FormatBytes(assignedMemoryBytes);
        TotalVmText.Text = totalVms.ToString();
        RunningVmText.Text = runningVms.ToString();
        PoweredOffVmText.Text = poweredOffVms.ToString();
        TotalCpuText.Text = totalCpus.ToString();
        NodesQueriedText.Text = $"{snapshot.HostCount}/{snapshot.HostCount}";
        CheckpointText.Text = snapshot.CheckpointCount.ToString();
        LastUpdatedText.Text = $"Last updated: {snapshot.CreatedAt:MM/dd/yyyy HH:mm:ss}";
        StatusText.Text = $"Inventory loaded — {snapshot.VirtualMachineCount} VM(s)";
        RowCountText.Text = $"{totalVms} rows";
    }

    // =========================================================
    // TOOLBAR ACTIONS
    // =========================================================

    private void RefreshMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (_inventoryCache.GetSnapshot().VirtualMachines.Count == 0)
        {
            ShowInfo(
                "There's no inventory loaded yet. Use Connect to add targets and run a collection first.",
                "Nothing to Refresh");
            return;
        }

        LoadCachedInventory();
        StatusText.Text = "Inventory view refreshed from the last collection.";
    }

    private void ExportToExcelMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (_virtualMachines.Count == 0)
        {
            ShowInfo("There's no inventory to export yet. Run a collection first.", "Export to Excel");
            return;
        }

        // NOTE: wire this to HyperVToolsX.Export once its public API is available here —
        // e.g. an IInventoryExporter.ExportAsync(_inventoryCache.GetSnapshot(), path).
        // Left as an explicit TODO rather than a silent no-op so it's obvious in the UI
        // that export isn't wired up yet, instead of a menu item that does nothing.
        ShowInfo(
            "Excel export isn't wired up yet in this build.\n\n" +
            "This should call into HyperVToolsX.Export with the current snapshot once " +
            "that project's exporter interface is available here.",
            "Export to Excel");
    }

    private void ExitMenuItem_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void AboutMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;

        ShowInfo(
            $"HyperVToolsX\nVersion {version}\n\n" +
            "A Hyper-V inventory and documentation tool.",
            "About HyperVToolsX");
    }

    private void SizeUnitMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem)
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

        BytesUnitMenuItem.IsChecked = selectedUnit == SizeUnit.Bytes;
        KbUnitMenuItem.IsChecked = selectedUnit == SizeUnit.KB;
        MbUnitMenuItem.IsChecked = selectedUnit == SizeUnit.MB;
        GbUnitMenuItem.IsChecked = selectedUnit == SizeUnit.GB;
        TbUnitMenuItem.IsChecked = selectedUnit == SizeUnit.TB;
        PbUnitMenuItem.IsChecked = selectedUnit == SizeUnit.PB;

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

    // =========================================================
    // BUSY STATE
    // =========================================================

    private void SetBusy(bool isBusy, string? message = null)
    {
        BusyIndicator.Visibility = isBusy ? Visibility.Visible : Visibility.Collapsed;
        Mouse.OverrideCursor = isBusy ? Cursors.Wait : null;

        if (message != null)
        {
            StatusText.Text = message;
        }
    }

    // =========================================================
    // MESSAGE HELPERS
    // =========================================================

    private void ShowInfo(string message, string title) =>
        MessageBox.Show(this, message, title, MessageBoxButton.OK, MessageBoxImage.Information);
}