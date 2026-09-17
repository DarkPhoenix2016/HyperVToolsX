using HyperVToolsX.Core.Collection;
using HyperVToolsX.Core.Enums;
using HyperVToolsX.Core.Models;
using HyperVToolsX.Infrastructure.Collection;
using HyperVToolsX.Infrastructure.HyperV;
using HyperVToolsX.Infrastructure.PowerShellEngine;
using HyperVToolsX.Infrastructure.Validation;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace HyperVToolsX.App.Views;

public partial class TargetManagerView : UserControl
{
    private readonly TargetManager _targetManager;
    private readonly TargetValidator _targetValidator;
    private readonly CollectionOrchestrator _collectionOrchestrator;
    private readonly InventoryCache _inventoryCache;

    public event EventHandler? CollectionCompleted;

    private readonly ObservableCollection<TargetEntry> _targets = [];

    public TargetManagerView(InventoryCache inventoryCache)
    {
        InitializeComponent();

        var powerShell = new PowerShellExecutor();

        var hyperVProvider =
            new HyperVProvider(powerShell);

        var inventoryCollector =
            new BasicInventoryCollector(
                hyperVProvider);

        _targetManager =
            new TargetManager();

        _targetValidator =
            new TargetValidator(
                hyperVProvider);

        _inventoryCache =
            inventoryCache;

        _collectionOrchestrator =
            new CollectionOrchestrator(
                _targetValidator,
                inventoryCollector,
                _inventoryCache);

        TargetDataGrid.ItemsSource =
            _targets;

        UpdateTargetStatistics();
    }

    // =========================================================
    // COLLECTION
    // =========================================================

    private async void CollectButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        var readyEntries =
            _targets
                .Where(
                    target =>
                        target.ValidationStatus ==
                            TargetValidationStatus.Ready ||
                        target.ValidationStatus ==
                            TargetValidationStatus.ClusterReady)
                .ToList();

        if (readyEntries.Count == 0)
        {
            MessageBox.Show(
                "There are no validated targets ready for collection.",
                "Start Collection",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            return;
        }

        CollectButton.IsEnabled = false;
        ValidateButton.IsEnabled = false;
        ImportButton.IsEnabled = false;
        AddTargetsButton.IsEnabled = false;
        ClearButton.IsEnabled = false;

        try
        {
            var targets =
                readyEntries
                    .Select(
                        entry =>
                            new HyperVTarget
                            {
                                Name = entry.Name,
                                Address = entry.Name,
                                Type = entry.Type,

                                Validation =
                                    new TargetValidationResult
                                    {
                                        TargetName =
                                            entry.Name,

                                        Status =
                                            entry.ValidationStatus,

                                        NameResolved =
                                            entry.NameResolved,

                                        ResolvedAddress =
                                            entry.ResolvedAddress,

                                        PingSucceeded =
                                            entry.PingSucceeded,

                                        HyperVConnectionSucceeded =
                                            entry.HyperVConnectionSucceeded,

                                        IsCluster =
                                            entry.IsCluster,

                                        ClusterName =
                                            string.IsNullOrWhiteSpace(
                                                entry.ClusterName)
                                                ? null
                                                : entry.ClusterName,

                                        ErrorMessage =
                                            string.IsNullOrWhiteSpace(
                                                entry.ErrorMessage)
                                                ? null
                                                : entry.ErrorMessage
                                    }
                            })
                    .ToList();

            var request =
                new CollectionRequest
                {
                    MaxConcurrentTargets =
                        _targetManager.CalculateWorkerCount(
                            targets.Count),

                    CollectCpu = true,
                    CollectMemory = true,
                    CollectStorage = true,
                    CollectNetwork = true,
                    CollectCheckpoints = true,
                    CollectIntegrationServices = true
                };

            var progress =
                new Progress<CollectionProgress>(
                    collectionProgress =>
                    {
                        StatusText.Text =
                            $"Collecting " +
                            $"{collectionProgress.CompletedTargets}/" +
                            $"{collectionProgress.TotalTargets}" +
                            $" — {collectionProgress.CurrentTarget}";
                    });

            StatusText.Text =
                $"Starting collection for " +
                $"{targets.Count} target(s)...";

            var result =
                await _collectionOrchestrator.CollectAsync(
                    targets,
                    request,
                    progress);

            // -----------------------------------------------------
            // UPDATE TARGET STATUS
            // -----------------------------------------------------

            foreach (var target in result.Targets)
            {
                var entry =
                    _targets.FirstOrDefault(
                        item =>
                            item.Name.Equals(
                                target.Name,
                                StringComparison.OrdinalIgnoreCase));

                if (entry == null)
                {
                    continue;
                }

                if (target.Status ==
                    ConnectionStatus.Connected)
                {
                    entry.Result =
                        "Collection Completed";

                    entry.ValidationStatus =
                        TargetValidationStatus.Completed;

                    entry.ErrorMessage =
                        string.Empty;
                }
                else
                {
                    entry.Result =
                        "Collection Failed";

                    entry.ValidationStatus =
                        TargetValidationStatus.Failed;

                    entry.ErrorMessage =
                        target.Validation.ErrorMessage
                        ?? string.Empty;
                }
            }

            TargetDataGrid.Items.Refresh();

            CollectionCompleted?.Invoke(
                this,
                EventArgs.Empty);

            StatusText.Text =
                $"Collection completed. " +
                $"{result.SuccessfulTargets} successful, " +
                $"{result.FailedTargets} failed. " +
                $"Hosts: {result.TotalHosts}, " +
                $"VMs: {result.TotalVirtualMachines}";

            MessageBox.Show(
                $"Collection completed.\n\n" +
                $"Successful targets: {result.SuccessfulTargets}\n" +
                $"Failed targets: {result.FailedTargets}\n" +
                $"Hosts: {result.TotalHosts}\n" +
                $"Virtual machines: {result.TotalVirtualMachines}",
                "Collection Complete",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (OperationCanceledException)
        {
            StatusText.Text =
                "Collection cancelled.";
        }
        catch (Exception ex)
        {
            StatusText.Text =
                "Collection failed.";

            MessageBox.Show(
                ex.Message,
                "Collection Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            ValidateButton.IsEnabled = true;
            ImportButton.IsEnabled = true;
            AddTargetsButton.IsEnabled = true;
            ClearButton.IsEnabled = true;

            CollectButton.IsEnabled =
                _targets.Any(
                    target =>
                        target.ValidationStatus ==
                            TargetValidationStatus.Ready ||
                        target.ValidationStatus ==
                            TargetValidationStatus.ClusterReady);
        }
    }

    // =========================================================
    // VALIDATION
    // =========================================================

    private async void ValidateButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_targets.Count == 0)
        {
            MessageBox.Show(
                "Please add at least one Hyper-V host or cluster.",
                "No Targets",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            return;
        }

        ValidateButton.IsEnabled = false;
        CollectButton.IsEnabled = false;
        ImportButton.IsEnabled = false;
        AddTargetsButton.IsEnabled = false;
        ClearButton.IsEnabled = false;

        StatusText.Text =
            "Validation started...";

        try
        {
            var targetEntries =
                _targets.ToList();

            var workerCount =
                _targetManager.CalculateWorkerCount(
                    targetEntries.Count);

            StatusText.Text =
                $"Validating {targetEntries.Count} target(s) " +
                $"using {workerCount} worker(s)...";

            var queue =
                new Queue<TargetEntry>(
                    targetEntries);

            var syncLock =
                new object();

            var workers =
                Enumerable
                    .Range(
                        0,
                        workerCount)
                    .Select(
                        _ =>
                            ValidateWorkerAsync(
                                queue,
                                syncLock))
                    .ToArray();

            await Task.WhenAll(workers);

            // -----------------------------------------------------
            // VALIDATION SUMMARY
            // -----------------------------------------------------

            var readyCount =
                _targets.Count(
                    target =>
                        target.ValidationStatus ==
                            TargetValidationStatus.Ready ||
                        target.ValidationStatus ==
                            TargetValidationStatus.ClusterReady);

            var hostCount =
                _targets.Count(
                    target =>
                        target.ValidationStatus ==
                            TargetValidationStatus.Ready);

            var clusterCount =
                _targets.Count(
                    target =>
                        target.ValidationStatus ==
                            TargetValidationStatus.ClusterReady);

            var failedCount =
                _targets.Count(
                    target =>
                        target.ValidationStatus !=
                            TargetValidationStatus.Ready &&
                        target.ValidationStatus !=
                            TargetValidationStatus.ClusterReady);

            StatusText.Text =
                $"Validation completed. " +
                $"{readyCount} ready, " +
                $"{failedCount} failed. " +
                $"{hostCount} host(s), " +
                $"{clusterCount} cluster(s).";

            CollectButton.IsEnabled =
                readyCount > 0;
        }
        catch (OperationCanceledException)
        {
            StatusText.Text =
                "Validation cancelled.";
        }
        catch (Exception ex)
        {
            StatusText.Text =
                "Validation failed.";

            MessageBox.Show(
                ex.Message,
                "Validation Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            ValidateButton.IsEnabled = true;
            ImportButton.IsEnabled = true;
            AddTargetsButton.IsEnabled = true;
            ClearButton.IsEnabled = true;

            CollectButton.IsEnabled =
                _targets.Any(
                    target =>
                        target.ValidationStatus ==
                            TargetValidationStatus.Ready ||
                        target.ValidationStatus ==
                            TargetValidationStatus.ClusterReady);
        }
    }

    // =========================================================
    // VALIDATION WORKER
    // =========================================================

    private async Task ValidateWorkerAsync(
        Queue<TargetEntry> queue,
        object syncLock)
    {
        while (true)
        {
            TargetEntry? entry = null;

            lock (syncLock)
            {
                if (queue.Count > 0)
                {
                    entry =
                        queue.Dequeue();
                }
            }

            if (entry == null)
            {
                return;
            }

            await ValidateTargetAsync(
                entry);
        }
    }

    // =========================================================
    // TARGET VALIDATION
    // =========================================================

    private async Task ValidateTargetAsync(
        TargetEntry entry)
    {
        try
        {
            entry.ValidationStatus =
                TargetValidationStatus.ResolvingName;

            entry.Result =
                "Resolving name...";

            entry.ErrorMessage =
                string.Empty;

            await RefreshTargetGridAsync();

            var target =
                new HyperVTarget
                {
                    Name =
                        entry.Name,

                    Address =
                        entry.Name,

                    Type =
                        entry.Type
                };

            var validation =
                await _targetValidator.ValidateAsync(
                    target);

            // -----------------------------------------------------
            // COPY VALIDATION RESULT
            // -----------------------------------------------------

            entry.ValidationStatus =
                validation.Status;

            entry.NameResolved =
                validation.NameResolved;

            entry.ResolvedAddress =
                validation.ResolvedAddress
                ?? string.Empty;

            entry.PingSucceeded =
                validation.PingSucceeded;

            entry.HyperVConnectionSucceeded =
                validation.HyperVConnectionSucceeded;

            entry.IsCluster =
                validation.IsCluster;

            entry.ClusterName =
                validation.ClusterName
                ?? string.Empty;

            entry.ErrorMessage =
                validation.ErrorMessage
                ?? string.Empty;

            entry.Result =
                GetResultText(
                    validation.Status);

            await RefreshTargetGridAsync();
        }
        catch (Exception ex)
        {
            entry.ValidationStatus =
                TargetValidationStatus.Failed;

            entry.Result =
                "Failed";

            entry.ErrorMessage =
                ex.Message;

            await RefreshTargetGridAsync();
        }
    }

    // =========================================================
    // DISCONNECT SELECTED
    // =========================================================

    public void DisconnectSelected()
    {
        var selected =
            TargetDataGrid.SelectedItem as TargetEntry;

        if (selected == null)
        {
            MessageBox.Show(
                "Please select a target first.",
                "Disconnect",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            return;
        }

        selected.ValidationStatus =
            TargetValidationStatus.Pending;

        selected.NameResolved = false;

        selected.ResolvedAddress =
            string.Empty;

        selected.PingSucceeded = false;

        selected.HyperVConnectionSucceeded =
            false;

        selected.IsCluster = false;

        selected.ClusterName =
            string.Empty;

        selected.Result =
            "Disconnected";

        selected.ErrorMessage =
            string.Empty;

        TargetDataGrid.Items.Refresh();

        UpdateTargetStatistics();

        StatusText.Text =
            $"Disconnected from {selected.Name}.";
    }

    // =========================================================
    // DISCONNECT ALL
    // =========================================================

    public void DisconnectAll()
    {
        if (_targets.Count == 0)
        {
            return;
        }

        foreach (var target in _targets)
        {
            target.ValidationStatus =
                TargetValidationStatus.Pending;

            target.NameResolved = false;

            target.ResolvedAddress =
                string.Empty;

            target.PingSucceeded =
                false;

            target.HyperVConnectionSucceeded =
                false;

            target.IsCluster =
                false;

            target.ClusterName =
                string.Empty;

            target.Result =
                "Disconnected";

            target.ErrorMessage =
                string.Empty;
        }

        TargetDataGrid.Items.Refresh();

        UpdateTargetStatistics();

        CollectButton.IsEnabled =
            false;

        StatusText.Text =
            "All targets disconnected.";
    }

    // =========================================================
    // IMPORT
    // =========================================================

    private void ImportButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        var dialog =
            new OpenFileDialog
            {
                Title =
                    "Import Hyper-V Target List",

                Filter =
                    "Text files (*.txt)|*.txt|All files (*.*)|*.*",

                Multiselect = false
            };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            var lines =
                File.ReadAllLines(
                    dialog.FileName);

            TargetInputTextBox.Text =
                string.Join(
                    Environment.NewLine,
                    lines);

            AddTargetsFromInput();

            StatusText.Text =
                $"Imported {lines.Length} lines.";
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.Message,
                "Import Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    // =========================================================
    // ADD TARGETS
    // =========================================================

    private void AddTargetsButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        AddTargetsFromInput();
    }

    private void AddTargetsFromInput()
    {
        var lines =
            TargetInputTextBox.Text
                .Split(
                    new[]
                    {
                        '\r',
                        '\n'
                    },
                    StringSplitOptions.RemoveEmptyEntries);

        var normalized =
            _targetManager.Normalize(
                lines);

        foreach (var target in normalized)
        {
            if (_targets.Any(
                existing =>
                    existing.Name.Equals(
                        target.Name,
                        StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            _targets.Add(
                new TargetEntry
                {
                    Name =
                        target.Name,

                    Type =
                        target.Type
                });
        }

        TargetInputTextBox.Clear();

        UpdateTargetStatistics();

        StatusText.Text =
            $"Added {_targets.Count} unique target(s).";
    }

    // =========================================================
    // CLEAR
    // =========================================================

    private void ClearButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        _targets.Clear();

        TargetInputTextBox.Clear();

        CollectButton.IsEnabled =
            false;

        UpdateTargetStatistics();

        StatusText.Text =
            "Targets cleared.";
    }

    // =========================================================
    // TARGET STATISTICS
    // =========================================================

    private void UpdateTargetStatistics()
    {
        var count =
            _targets.Count;

        var workers =
            _targetManager.CalculateWorkerCount(
                count);

        TargetCountText.Text =
            $"{count} target{(count == 1 ? "" : "s")}";

        WorkerCountText.Text =
            workers.ToString();
    }

    // =========================================================
    // GRID REFRESH
    // =========================================================

    private async Task RefreshTargetGridAsync()
    {
        await Dispatcher.InvokeAsync(
            () =>
            {
                TargetDataGrid.Items.Refresh();
            });
    }

    // =========================================================
    // VALIDATION RESULT TEXT
    // =========================================================

    private static string GetResultText(
        TargetValidationStatus status)
    {
        return status switch
        {
            TargetValidationStatus.Ready =>
                "Ready",

            TargetValidationStatus.NameResolutionFailed =>
                "DNS Failed",

            TargetValidationStatus.PingFailed =>
                "Ping Failed",

            TargetValidationStatus.ConnectionFailed =>
                "Hyper-V Connection Failed",

            TargetValidationStatus.NotHyperV =>
                "Not Hyper-V",

            TargetValidationStatus.ClusterReady =>
                "Cluster Ready",

            TargetValidationStatus.Completed =>
                "Completed",

            TargetValidationStatus.Failed =>
                "Failed",

            _ =>
                status.ToString()
        };
    }

    // =========================================================
    // WINDOWS CREDENTIALS
    // =========================================================

    private void UseCurrentWindowsCredentialsCheckBox_Changed(
        object sender,
        RoutedEventArgs e)
    {
        var useCurrentCredentials =
            UseCurrentWindowsCredentialsCheckBox.IsChecked ==
            true;

        if (UsernameTextBox == null ||
            PasswordBox == null)
        {
            return;
        }

        UsernameTextBox.IsEnabled =
            !useCurrentCredentials;

        PasswordBox.IsEnabled =
            !useCurrentCredentials;
    }
}