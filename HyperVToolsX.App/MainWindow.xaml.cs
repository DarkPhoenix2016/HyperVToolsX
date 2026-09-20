using HyperVToolsX.App.Converters;
using HyperVToolsX.App.ViewModels;
using HyperVToolsX.App.Views;
using HyperVToolsX.Core.Collection;
using HyperVToolsX.Core.Enums;
using HyperVToolsX.Core.Interfaces;
using HyperVToolsX.Core.Logging;
using HyperVToolsX.Core.Models;
using HyperVToolsX.Core.Models.Details;
using HyperVToolsX.Core.Templates;
using HyperVToolsX.Export;
using HyperVToolsX.Infrastructure.Collection;
using HyperVToolsX.Infrastructure.HyperV;
using HyperVToolsX.Infrastructure.PowerShellEngine;
using HyperVToolsX.Infrastructure.Remoting;
using HyperVToolsX.Infrastructure.Templates;
using HyperVToolsX.Infrastructure.Validation;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Collections;
using System.Diagnostics;
using System.Reflection;
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

    // vInfo is shown one page at a time: _vmPage holds the visible rows, taken from the filtered and sorted list.
    private readonly ObservableCollection<HyperVVirtualMachine> _vmPage = [];
    private int _vmPageIndex;
    private int _vmFilteredCount;
    private string? _vmSortPath;
    private ListSortDirection _vmSortDirection = ListSortDirection.Ascending;
    private bool _pagingReady;

    private ICollectionView? _networkCollectionView;

    // vSummary rows, plus every view whose rows the global search box filters.
    private readonly ObservableCollection<VmSummaryItem> _summaryItems = [];
    private readonly List<ICollectionView> _searchViews = [];
    private readonly List<ICollectionView> _customSearchViews = [];
    private Dictionary<string, string> _vmIpv4ByName = new(StringComparer.OrdinalIgnoreCase);

    private readonly RemoteConnectionOptions _connectionOptions;
    private readonly LiveLog _liveLog;

    private TargetManagerView? _targetManagerView;

    private readonly TemplateStore _templateStore;
    private IReadOnlyList<CustomTabTemplate> _templates = [];
    private readonly List<CustomTabHost> _customTabs = [];

    private sealed record CustomTabHost(TabItem Tab, CustomTabTemplate Template, DataGrid Grid);

    public MainWindow()
    {
        InitializeComponent();

        foreach (var grid in FindGrids(InventoryTabControl))
        {
            SetupGridCopy(grid);
        }

        // Composition root: build the shared object graph once here instead of
        // letting each view construct its own copies of these collaborators.
        // Composition root: build the shared object graph once here.
        _liveLog = new LiveLog();
        var powerShell = new PowerShellExecutor(_liveLog);
        var hyperVProvider = new HyperVProvider(powerShell);
        _connectionOptions = new RemoteConnectionOptions();
        var remoteRunner = new RemoteScriptRunner(powerShell, _connectionOptions, log: _liveLog);
        var inventoryCollector = new RemoteInventoryCollector(remoteRunner, _liveLog);

        _inventoryCache = new InventoryCache();
        _targetManager = new TargetManager();
        _targetValidator = new TargetValidator(remoteRunner, _liveLog);

        _collectionOrchestrator = new CollectionOrchestrator(
            _targetValidator,
            inventoryCollector,
            _inventoryCache,
            _liveLog);

        ByteSizeConverter.CurrentUnit = SizeUnit.GB;
        GbUnitMenuItem.IsChecked = true;

        // Custom tabs live as XML files in a Templates folder beside the exe;
        // the folder is created on first run and read on every start.
        _templateStore = new TemplateStore(TemplateStore.DefaultFolder, _liveLog);
        _templates = _templateStore.LoadAll();
        RebuildCustomTabs();

        Loaded += MainWindow_Loaded;
        PreviewKeyDown += MainWindow_PreviewKeyDown;

        VmDataGrid.ItemsSource = _vmPage;

        PopulateFilters();

        _pagingReady = true;
        RebuildVmPage();
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
        // One view instance is kept for the session so entered targets survive
        // closing and reopening the Target Manager.
        var targetManagerView = _targetManagerView ??= new TargetManagerView(
            _targetManager,
            _targetValidator,
            _collectionOrchestrator,
            _inventoryCache,
            _connectionOptions,
            _liveLog);

        var targetManagerWindow = new Window
        {
            Title = "HyperVToolsX - Target Manager",
            Width = 1000,
            Height = 780,
            MinWidth = 1000,
            MinHeight = 780,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = this,
            Icon = Icon,
            Content = targetManagerView,
            ResizeMode = ResizeMode.NoResize
        };

        targetManagerWindow.ShowDialog();

        // Detach so the view can be hosted again by the next window.
        targetManagerWindow.Content = null;

        // Closing the Target Manager after a successful collection loads the
        // collected inventory into the main window.
        if (targetManagerView.HasCollectedInventory)
        {
            LoadCachedInventory();
        }
    }

    private void ConnectMenuItem_Click(object sender, RoutedEventArgs e)
    {
        OpenTargetManager();
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
            LoadSummary(snapshot);
            LoadCollection(_networkVlans, snapshot.NetworkVlans, VlanDataGrid);
            LoadCollection(_checkpoints, snapshot.Checkpoints, CheckpointDataGrid);
            LoadCollection(_integrationServices, snapshot.IntegrationServices, IntegrationDataGrid);
            LoadCollection(_vmStorage, snapshot.VmStorage, StorageDataGrid);
            LoadCollection(_vhds, snapshot.Vhds, VhdDataGrid);
            LoadCollection(_replications, snapshot.Replication, ReplicationDataGrid);
            LoadCollection(_dvds, snapshot.Dvds, DvdDataGrid);
            LoadCollection(_clusters, snapshot.Clusters, ClusterDataGrid);
            LoadHostInventory(snapshot);
            RefreshCustomTabs(snapshot);
            AttachSearchFilters();

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

        VmDataGrid.ItemsSource = _vmPage;

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

    private void LoadSummary(InventorySnapshot snapshot)
    {
        _summaryItems.Clear();

        foreach (var item in VmSummaryItem.Build(snapshot))
        {
            _summaryItems.Add(item);
        }

        _vmIpv4ByName = _summaryItems
            .Where(item => item.IPv4Addresses.Length > 0)
            .GroupBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => string.Join(", ", g.Select(item => item.IPv4Addresses)),
                StringComparer.OrdinalIgnoreCase);

        SummaryDataGrid.ItemsSource = _summaryItems;
    }

    private void LoadHostInventory(InventorySnapshot snapshot)
    {
        _hostInventoryRows.Clear();

        foreach (var row in HostInventoryBuilder.Build(snapshot))
        {
            _hostInventoryRows.Add(row);
        }

        HostDataGrid.ItemsSource = _hostInventoryRows;
    }

    // =========================================================
    // CUSTOM TABS
    // =========================================================

    private void NewCustomTabMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var editor = new TemplateEditorWindow(_templates.Select(t => t.Name)) { Owner = this };

        if (editor.ShowDialog() != true || editor.Result is not { } created)
        {
            return;
        }

        try
        {
            _templateStore.Save(created);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                $"Could not save the custom tab to {_templateStore.Folder}:{Environment.NewLine}{ex.Message}",
                "Custom Tabs",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return;
        }

        _templates = [.. _templates, created];
        RebuildCustomTabs();
        RefreshCustomTabs(_inventoryCache.GetSnapshot());

        InventoryTabControl.SelectedItem =
            _customTabs.FirstOrDefault(t => Equals(t.Tab.Header, created.Name))?.Tab;

        StatusText.Text = $"Custom tab '{created.Name}' saved.";
    }

    private void ManageCustomTabsMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var manager = new TemplateManagerWindow(_templateStore, _templates) { Owner = this };
        manager.ShowDialog();

        if (manager.Changed)
        {
            _templates = manager.Templates.ToList();
            RebuildCustomTabs();
            RefreshCustomTabs(_inventoryCache.GetSnapshot());
        }
    }

    /// <summary>Recreates one tab per template after the last built-in tab.</summary>
    private void RebuildCustomTabs()
    {
        var selectedHeader = (InventoryTabControl.SelectedItem as TabItem)?.Header as string;

        foreach (var custom in _customTabs)
        {
            InventoryTabControl.Items.Remove(custom.Tab);
        }

        _customTabs.Clear();

        foreach (var template in _templates)
        {
            var columns = CustomTabBuilder.ResolveColumns(template);

            if (columns.Count == 0)
            {
                continue;
            }

            var grid = BuildCustomGrid(columns);
            SetupGridCopy(grid);

            var tab = new TabItem
            {
                Header = template.Name,
                Content = grid,
                ToolTip = InventoryCatalog.IsVmRowSource(template.Source)
                    ? "Custom tab (one row per VM)"
                    : $"Custom tab ({template.Source})"
            };

            InventoryTabControl.Items.Add(tab);
            _customTabs.Add(new CustomTabHost(tab, template, grid));

            if (template.Name == selectedHeader)
            {
                InventoryTabControl.SelectedItem = tab;
            }
        }
    }

    private static DataGrid BuildCustomGrid(IReadOnlyList<ResolvedColumn> columns)
    {
        var grid = new DataGrid
        {
            AutoGenerateColumns = false,
            IsReadOnly = true,
            CanUserAddRows = false,
            CanUserDeleteRows = false,
            SelectionMode = DataGridSelectionMode.Single,
            SelectionUnit = DataGridSelectionUnit.FullRow,
            EnableRowVirtualization = true,
            EnableColumnVirtualization = true
        };

        for (var i = 0; i < columns.Count; i++)
        {
            grid.Columns.Add(new DataGridTextColumn
            {
                Header = columns[i].Header,
                Binding = new Binding($"Values[{i}]")
                {
                    Mode = BindingMode.OneWay,
                    Converter = new CustomCellConverter(columns[i].Field.SizeSourceUnit)
                },
                SortMemberPath = $"SortKeys[{i}]",
                Width = DataGridLength.Auto
            });
        }

        return grid;
    }

    private void RefreshCustomTabs(InventorySnapshot snapshot)
    {
        _customSearchViews.Clear();

        foreach (var custom in _customTabs)
        {
            var rows = CustomTabBuilder.Build(snapshot, custom.Template).Rows;
            custom.Grid.ItemsSource = rows;

            var view = CollectionViewSource.GetDefaultView(rows);
            view.Filter = MatchesSearch;
            _customSearchViews.Add(view);
        }
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
            if (!MatchesSearch(vm))
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
                MatchesSearch(adapter) ||
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

    private bool FilterSummary(object obj)
    {
        return obj is VmSummaryItem item
               && MatchesSearch(item)
               && MatchesComboFilter(HostFilterComboBox, item.HostName)
               && MatchesComboFilter(StateFilterComboBox, item.State)
               && MatchesComboFilter(ClusterFilterComboBox, item.ClusterName);
    }

    // Columns the global search looks at, on whichever row type a tab holds.
    private static readonly string[] SearchPropertyNames =
        ["VmName", "Name", "HostName", "ComputerName", "ClusterName", "VMId", "IPv4Addresses"];

    private static readonly Dictionary<Type, PropertyInfo[]> SearchProperties = [];

    /// <summary>
    /// True when the global search text matches the row VM name, host, cluster, VM ID or IPv4 address.
    /// Rows without an address of their own (vCPU, vDisk, ...) match through their VM addresses.
    /// </summary>
    private bool MatchesSearch(object item)
    {
        var search = VmSearchTextBox.Text.Trim();

        if (search.Length == 0)
        {
            return true;
        }

        if (item is CompositeRow row)
        {
            return row.Values.Any(value => ValueMatches(value, search));
        }

        var type = item.GetType();

        if (!SearchProperties.TryGetValue(type, out var properties))
        {
            properties = SearchPropertyNames
                .Select(name => type.GetProperty(name))
                .OfType<PropertyInfo>()
                .ToArray();
            SearchProperties[type] = properties;
        }

        string? vmName = null;

        foreach (var property in properties)
        {
            var value = property.GetValue(item);

            if (ValueMatches(value, search))
            {
                return true;
            }

            if (property.Name == "VmName" || (property.Name == "Name" && item is HyperVVirtualMachine or VmSummaryItem))
            {
                vmName = value as string;
            }
        }

        return vmName is not null
               && _vmIpv4ByName.TryGetValue(vmName, out var ips)
               && ips.Contains(search, StringComparison.OrdinalIgnoreCase);
    }

    private static bool ValueMatches(object? value, string search) => value switch
    {
        null => false,
        string text => text.Contains(search, StringComparison.OrdinalIgnoreCase),
        CollapsedValue collapsed => collapsed.Items.Any(v => ValueMatches(v, search)),
        IEnumerable list => list.Cast<object?>().Any(v => ValueMatches(v, search)),
        _ => value.ToString()?.Contains(search, StringComparison.OrdinalIgnoreCase) == true
    };

    /// <summary>Points every plain tab grid at the global search filter.</summary>
    private void AttachSearchFilters()
    {
        _searchViews.Clear();

        IEnumerable[] sources =
        [
            _processors, _memories, _disks, _networkVlans, _checkpoints, _integrationServices,
            _vmStorage, _vhds, _replications, _dvds, _clusters, _hostInventoryRows
        ];

        foreach (var source in sources)
        {
            var view = CollectionViewSource.GetDefaultView(source);
            view.Filter = MatchesSearch;
            _searchViews.Add(view);
        }

        var summaryView = CollectionViewSource.GetDefaultView(_summaryItems);
        summaryView.Filter = FilterSummary;
        _searchViews.Add(summaryView);
    }

    private static bool MatchesComboFilter(ComboBox comboBox, string value)
    {
        // The first entry ("All Clusters", "All Hosts", ...) means no filter.
        var selected = comboBox.SelectedItem as string;

        return comboBox.SelectedIndex <= 0 ||
               string.IsNullOrWhiteSpace(selected) ||
               value.Equals(selected, StringComparison.OrdinalIgnoreCase);
    }

    private void PopulateFilters()
    {
        SetComboBoxItems(HostFilterComboBox, _virtualMachines.Select(vm => vm.HostName), "All Hosts");
        SetComboBoxItems(StateFilterComboBox, _virtualMachines.Select(vm => vm.State), "All States");
        SetComboBoxItems(ClusterFilterComboBox, _virtualMachines.Select(vm => vm.ClusterName), "All Clusters");
    }

    private static void SetComboBoxItems(ComboBox comboBox, IEnumerable<string> values, string allLabel)
    {
        var distinctValues = values
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(v => v)
            .ToList();

        comboBox.ItemsSource = new[] { allLabel }.Concat(distinctValues).ToList();
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
        // A new filter starts again from the first page.
        _vmPageIndex = 0;
        RebuildVmPage();

        _networkCollectionView?.Refresh();

        foreach (var view in _searchViews.Concat(_customSearchViews))
        {
            view.Refresh();
        }

        UpdateVisibleCounts();
    }

    // =========================================================
    // VM PAGING AND SORTING
    // =========================================================

    private int VmPageSize =>
        int.TryParse((PageSizeComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString(), out var size) && size > 0
            ? size
            : 50;

    /// <summary>
    /// Filters, sorts and pages the VMs. Sorting is done over the whole filtered list (not just the visible
    /// page), so clicking a column header orders every row, then the page is cut from the result.
    /// </summary>
    private void RebuildVmPage()
    {
        if (!_pagingReady)
        {
            return;
        }

        var filtered = _virtualMachines.Where(vm => FilterVm(vm)).ToList();

        if (_vmSortPath is not null
            && typeof(HyperVVirtualMachine).GetProperty(_vmSortPath) is { } property)
        {
            filtered = (_vmSortDirection == ListSortDirection.Ascending
                    ? filtered.OrderBy(vm => property.GetValue(vm))
                    : filtered.OrderByDescending(vm => property.GetValue(vm)))
                .ToList();
        }

        _vmFilteredCount = filtered.Count;

        var pageSize = VmPageSize;
        var pageCount = Math.Max(1, (int)Math.Ceiling(filtered.Count / (double)pageSize));
        _vmPageIndex = Math.Clamp(_vmPageIndex, 0, pageCount - 1);

        _vmPage.Clear();

        foreach (var vm in filtered.Skip(_vmPageIndex * pageSize).Take(pageSize))
        {
            _vmPage.Add(vm);
        }

        PrevPageButton.IsEnabled = _vmPageIndex > 0;
        NextPageButton.IsEnabled = _vmPageIndex < pageCount - 1;

        UpdateVisibleCounts();
    }

    private void VmDataGrid_Sorting(object sender, DataGridSortingEventArgs e)
    {
        e.Handled = true;

        var path = e.Column.SortMemberPath;

        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        _vmSortDirection = _vmSortPath == path && _vmSortDirection == ListSortDirection.Ascending
            ? ListSortDirection.Descending
            : ListSortDirection.Ascending;
        _vmSortPath = path;

        foreach (var column in VmDataGrid.Columns)
        {
            column.SortDirection = null;
        }

        e.Column.SortDirection = _vmSortDirection;

        _vmPageIndex = 0;
        RebuildVmPage();
    }

    private void PageSizeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _vmPageIndex = 0;
        RebuildVmPage();
    }

    private void PrevPageButton_Click(object sender, RoutedEventArgs e)
    {
        _vmPageIndex--;
        RebuildVmPage();
    }

    private void NextPageButton_Click(object sender, RoutedEventArgs e)
    {
        _vmPageIndex++;
        RebuildVmPage();
    }

    private void SettingsHeaderButton_Click(object sender, RoutedEventArgs e)
    {
        // The gear opens the Preferences menu (data size unit).
        PreferencesMenuItem.IsSubmenuOpen = true;
    }

    private void RefreshNetworkFilter()
    {
        _networkCollectionView?.Refresh();
        UpdateVisibleCounts();
    }

    private void UpdateVisibleCounts()
    {
        if (_pagingReady)
        {
            var pageSize = VmPageSize;
            var first = _vmFilteredCount == 0 ? 0 : _vmPageIndex * pageSize + 1;
            var last = Math.Min(_vmFilteredCount, (_vmPageIndex + 1) * pageSize);

            VmSummaryCountText.Text = _vmFilteredCount == 0
                ? $"Showing 0 of {_virtualMachines.Count} VMs"
                : _vmFilteredCount == _virtualMachines.Count
                    ? $"Showing {first}-{last} of {_vmFilteredCount} VMs"
                    : $"Showing {first}-{last} of {_vmFilteredCount} VMs (filtered from {_virtualMachines.Count})";

            RowCountText.Text = $"{_vmFilteredCount} rows";
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

    private async void ExportToExcelMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var snapshot = _inventoryCache.GetSnapshot();

        if (snapshot.VirtualMachines.Count == 0 && snapshot.Hosts.Count == 0)
        {
            ShowInfo("There's no inventory to export yet. Run a collection first.", "Export to Excel");
            return;
        }

        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Export to Excel",
            Filter = "Excel Workbook (*.xlsx)|*.xlsx",
            DefaultExt = ".xlsx",
            FileName = $"HyperVToolsX-{DateTime.Now:yyyyMMdd-HHmmss}.xlsx"
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        StatusText.Text = "Exporting inventory to Excel...";
        Mouse.OverrideCursor = Cursors.Wait;

        try
        {
            var sheetCount = await new ExcelInventoryExporter()
                .ExportAsync(snapshot, dialog.FileName, ByteSizeConverter.CurrentUnit, _templates);

            StatusText.Text = $"Exported {sheetCount} sheets to {dialog.FileName}";
        }
        catch (Exception ex)
        {
            StatusText.Text = "Export failed.";
            MessageBox.Show(
                this,
                $"The export failed:{Environment.NewLine}{ex.Message}",
                "Export to Excel",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            Mouse.OverrideCursor = null;
        }
    }

    private void ExitMenuItem_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void AboutMenuItem_Click(
        object sender,
        RoutedEventArgs e)
    {
        var aboutWindow = new AboutWindow
        {
            Owner = this
        };

        aboutWindow.ShowDialog();
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
        SummaryDataGrid.Items.Refresh();
        MemoryDataGrid.Items.Refresh();
        StorageDataGrid.Items.Refresh();
        DiskDataGrid.Items.Refresh();
        VhdDataGrid.Items.Refresh();
        CheckpointDataGrid.Items.Refresh();
        HostDataGrid.Items.Refresh();
        ClusterDataGrid.Items.Refresh();

        foreach (var custom in _customTabs)
        {
            custom.Grid.Items.Refresh();
        }

        UpdateSummary(_inventoryCache.GetSnapshot());
    }

    // =========================================================
    // COPYING FROM GRIDS
    // =========================================================

    private static IEnumerable<DataGrid> FindGrids(DependencyObject parent)
    {
        foreach (var child in LogicalTreeHelper.GetChildren(parent).OfType<DependencyObject>())
        {
            if (child is DataGrid grid)
            {
                yield return grid;
            }
            else
            {
                foreach (var nested in FindGrids(child))
                {
                    yield return nested;
                }
            }
        }
    }

    private static T? FindParent<T>(DependencyObject? element) where T : DependencyObject
    {
        while (element is not null && element is not T)
        {
            element = element is System.Windows.Media.Visual or System.Windows.Media.Media3D.Visual3D
                ? System.Windows.Media.VisualTreeHelper.GetParent(element)
                : LogicalTreeHelper.GetParent(element);
        }

        return element as T;
    }

    /// <summary>
    /// Lets any grid select several rows, copy them with Ctrl+C, and copy a single cell or the
    /// selected rows (optionally with headers) from the right-click menu.
    /// </summary>
    private static void SetupGridCopy(DataGrid grid)
    {
        grid.SelectionMode = DataGridSelectionMode.Extended;
        grid.SelectionUnit = DataGridSelectionUnit.FullRow;
        grid.ClipboardCopyMode = DataGridClipboardCopyMode.ExcludeHeader;

        DataGridCell? clickedCell = null;

        grid.PreviewMouseDown += (_, e) =>
        {
            clickedCell = FindParent<DataGridCell>(e.OriginalSource as DependencyObject);

            // A right-click on an unselected row selects it, so Copy Row(s) has something to copy.
            if (e.ChangedButton == MouseButton.Right
                && FindParent<DataGridRow>(e.OriginalSource as DependencyObject) is { IsSelected: false } row)
            {
                grid.SelectedItems.Clear();
                row.IsSelected = true;
            }
        };

        void CopyRows(DataGridClipboardCopyMode mode)
        {
            var previous = grid.ClipboardCopyMode;
            grid.ClipboardCopyMode = mode;
            ApplicationCommands.Copy.Execute(null, grid);
            grid.ClipboardCopyMode = previous;
        }

        var copyCell = new MenuItem { Header = "Copy Cell" };
        copyCell.Click += (_, _) =>
        {
            if (clickedCell?.Column is null)
            {
                return;
            }

            var text = clickedCell.Column.OnCopyingCellClipboardContent(clickedCell.DataContext)?.ToString()
                       ?? (clickedCell.Content as TextBlock)?.Text
                       ?? string.Empty;

            Clipboard.SetText(text);
        };

        var copyRows = new MenuItem { Header = "Copy Row(s)", InputGestureText = "Ctrl+C" };
        copyRows.Click += (_, _) => CopyRows(DataGridClipboardCopyMode.ExcludeHeader);

        var copyRowsWithHeaders = new MenuItem { Header = "Copy Row(s) with Headers" };
        copyRowsWithHeaders.Click += (_, _) => CopyRows(DataGridClipboardCopyMode.IncludeHeader);

        grid.ContextMenu = new ContextMenu { Items = { copyCell, copyRows, copyRowsWithHeaders } };
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