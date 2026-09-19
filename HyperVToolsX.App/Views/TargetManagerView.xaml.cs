using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using HyperVToolsX.Core.Collection;
using HyperVToolsX.Core.Enums;
using HyperVToolsX.Core.Interfaces;
using HyperVToolsX.Core.Models;
using HyperVToolsX.Infrastructure.Collection;
using Microsoft.Win32;

namespace HyperVToolsX.App.Views;

public partial class TargetManagerView : UserControl
{
    private readonly ITargetManager _targetManager;
    private readonly ITargetValidator _targetValidator;
    private readonly CollectionOrchestrator _collectionOrchestrator;
    private readonly IInventoryCache _inventoryCache;

    private readonly ObservableCollection<TargetEntry> _targets = [];
    private readonly ICollectionView _targetsView;

    private CancellationTokenSource? _operationCts;
    private bool _validationCompleted;
    private bool _collectionCompleted;

    public event EventHandler? CollectionCompleted;

    public TargetManagerView(
        ITargetManager targetManager,
        ITargetValidator targetValidator,
        CollectionOrchestrator collectionOrchestrator,
        IInventoryCache inventoryCache)
    {
        InitializeComponent();

        _targetManager = targetManager;
        _targetValidator = targetValidator;
        _collectionOrchestrator = collectionOrchestrator;
        _inventoryCache = inventoryCache;

        _targetsView = CollectionViewSource.GetDefaultView(_targets);
        TargetDataGrid.ItemsSource = _targetsView;

        UpdateTargetStatistics();
        UpdateActionButtons();
    }

    // =========================================================
    // FILTERING
    // =========================================================

    private void FilterTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var filter = FilterTextBox.Text?.Trim();

        _targetsView.Filter = string.IsNullOrWhiteSpace(filter)
            ? null
            : item =>
            {
                if (item is not TargetEntry entry)
                {
                    return false;
                }

                return entry.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)
                    || entry.Result.Contains(filter, StringComparison.OrdinalIgnoreCase)
                    || entry.ClusterName.Contains(filter, StringComparison.OrdinalIgnoreCase);
            };

        _targetsView.Refresh();
    }

    // =========================================================
    // COLLECTION
    // =========================================================

    private async void CollectButton_Click(object sender, RoutedEventArgs e)
    {
        var readyEntries = _targets.Where(IsReadyForCollection).ToList();

        if (readyEntries.Count == 0)
        {
            ShowInfo("There are no validated targets ready for collection.", "Start Collection");
            return;
        }

        using var cts = BeginOperation();

        try
        {
            var targets = readyEntries.Select(BuildTargetFromEntry).ToList();

            var request = new CollectionRequest
            {
                MaxConcurrentTargets = _targetManager.CalculateWorkerCount(targets.Count),
                CollectCpu = true,
                CollectMemory = true,
                CollectStorage = true,
                CollectNetwork = true,
                CollectCheckpoints = true,
                CollectIntegrationServices = true
            };

            var progress = new Progress<CollectionProgress>(p =>
            {
                SetProgress(p.CompletedTargets, p.TotalTargets);
                StatusText.Text = $"Collecting {p.CompletedTargets}/{p.TotalTargets}, {p.CurrentTarget}";
            });

            StatusText.Text = $"Starting collection for {targets.Count} target(s)...";

            var result = await _collectionOrchestrator.CollectAsync(
                targets,
                request,
                progress,
                cts.Token);

            ApplyCollectionResults(result);

            _collectionCompleted = result.SuccessfulTargets > 0;

            StatusText.Text = $"Collection completed. {result.SuccessfulTargets} successful, {result.FailedTargets} failed. Hosts: {result.TotalHosts}, VMs: {result.TotalVirtualMachines}";

            UpdateTargetStatistics();
            UpdateActionButtons();

            ShowInfo(
                $"Collection completed.\n\n" +
                $"Successful targets: {result.SuccessfulTargets}\n" +
                $"Failed targets: {result.FailedTargets}\n" +
                $"Hosts: {result.TotalHosts}\n" +
                $"Virtual machines: {result.TotalVirtualMachines}",
                "Collection Complete");
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = "Collection cancelled.";
            _collectionCompleted = false;
        }
        catch (Exception ex)
        {
            StatusText.Text = "Collection failed.";
            _collectionCompleted = false;
            ShowError(ex.Message, "Collection Error");
        }
        finally
        {
            EndOperation();
            UpdateActionButtons();
        }
    }

    private static HyperVTarget BuildTargetFromEntry(TargetEntry entry)
    {
        return new HyperVTarget
        {
            Name = entry.Name,
            Address = string.IsNullOrWhiteSpace(entry.ResolvedAddress) ? entry.Name : entry.ResolvedAddress,
            Type = entry.Type,
            Validation = new TargetValidationResult
            {
                TargetName = entry.Name,
                Status = entry.ValidationStatus,
                NameResolved = entry.NameResolved,
                ResolvedAddress = entry.ResolvedAddress,
                PingSucceeded = entry.PingSucceeded,
                HyperVConnectionSucceeded = entry.HyperVConnectionSucceeded,
                IsCluster = entry.IsCluster,
                ClusterName = string.IsNullOrWhiteSpace(entry.ClusterName) ? null : entry.ClusterName,
                ErrorMessage = string.IsNullOrWhiteSpace(entry.ErrorMessage) ? null : entry.ErrorMessage
            }
        };
    }

    private void ApplyCollectionResults(CollectionResult result)
    {
        foreach (var target in result.Targets)
        {
            var entry = _targets.FirstOrDefault(item =>
                item.Name.Equals(target.Name, StringComparison.OrdinalIgnoreCase));

            if (entry == null)
            {
                continue;
            }

            if (target.Status == ConnectionStatus.Connected)
            {
                entry.Result = "Collection Completed";
                entry.ValidationStatus = TargetValidationStatus.Completed;
                entry.ErrorMessage = string.Empty;
            }
            else
            {
                entry.Result = "Collection Failed";
                entry.ValidationStatus = TargetValidationStatus.Failed;
                entry.ErrorMessage = target.Validation.ErrorMessage ?? string.Empty;
            }
        }

        _targetsView.Refresh();
    }

    // =========================================================
    // VALIDATION
    // =========================================================

    private async void ValidateButton_Click(object sender, RoutedEventArgs e)
    {
        if (_targets.Count == 0)
        {
            ShowInfo("Please add at least one Hyper-V host or cluster.", "No Targets");
            return;
        }

        _validationCompleted = false;
        _collectionCompleted = false;

        using var cts = BeginOperation();

        try
        {
            var targetEntries = _targets.ToList();
            var workerCount = _targetManager.CalculateWorkerCount(targetEntries.Count);

            StatusText.Text = $"Validating {targetEntries.Count} target(s) using {workerCount} worker(s)...";

            var completed = 0;
            var total = targetEntries.Count;
            var queue = new ConcurrentQueue<TargetEntry>(targetEntries);

            var workers = Enumerable.Range(0, workerCount)
                .Select(_ => ValidateWorkerAsync(
                    queue,
                    () =>
                    {
                        var done = Interlocked.Increment(ref completed);
                        Dispatcher.Invoke(() => SetProgress(done, total));
                    },
                    cts.Token))
                .ToArray();

            await Task.WhenAll(workers);

            _validationCompleted = true;

            var readyCount = _targets.Count(IsReadyForCollection);
            var hostCount = _targets.Count(t => t.ValidationStatus == TargetValidationStatus.Ready);
            var clusterCount = _targets.Count(t => t.ValidationStatus == TargetValidationStatus.ClusterReady);
            var failedCount = _targets.Count - readyCount;

            StatusText.Text = $"Validation completed. {readyCount} ready, {failedCount} failed. {hostCount} host(s), {clusterCount} cluster(s).";

            UpdateTargetStatistics();
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = "Validation cancelled.";
            _validationCompleted = false;
        }
        catch (Exception ex)
        {
            StatusText.Text = "Validation failed.";
            _validationCompleted = false;
            ShowError(ex.Message, "Validation Error");
        }
        finally
        {
            EndOperation();
            UpdateActionButtons();
        }
    }

    private async Task ValidateWorkerAsync(
        ConcurrentQueue<TargetEntry> queue,
        Action onItemCompleted,
        CancellationToken cancellationToken)
    {
        while (queue.TryDequeue(out var entry))
        {
            cancellationToken.ThrowIfCancellationRequested();
            await ValidateTargetAsync(entry, cancellationToken);
            onItemCompleted();
        }
    }

    private async Task ValidateTargetAsync(TargetEntry entry, CancellationToken cancellationToken)
    {
        try
        {
            entry.ValidationStatus = TargetValidationStatus.ResolvingName;
            entry.Result = "Resolving name...";
            entry.ErrorMessage = string.Empty;

            _targetsView.Refresh();

            var target = new HyperVTarget
            {
                Name = entry.Name,
                Address = entry.Name,
                Type = entry.Type
            };

            var validation = await _targetValidator.ValidateAsync(target, cancellationToken);

            entry.ValidationStatus = validation.Status;
            entry.NameResolved = validation.NameResolved;
            entry.ResolvedAddress = validation.ResolvedAddress ?? string.Empty;
            entry.PingSucceeded = validation.PingSucceeded;
            entry.HyperVConnectionSucceeded = validation.HyperVConnectionSucceeded;
            entry.IsCluster = validation.IsCluster;
            entry.ClusterName = validation.ClusterName ?? string.Empty;
            entry.ErrorMessage = validation.ErrorMessage ?? string.Empty;
            entry.Result = GetResultText(validation.Status);

            _targetsView.Refresh();
        }
        catch (OperationCanceledException)
        {
            entry.Result = "Cancelled";
            _targetsView.Refresh();
            throw;
        }
        catch (Exception ex)
        {
            entry.ValidationStatus = TargetValidationStatus.Failed;
            entry.Result = "Failed";
            entry.ErrorMessage = ex.Message;
            _targetsView.Refresh();
        }
    }

    // =========================================================
    // GO TO MAIN
    // =========================================================

    private void GoToMainButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_collectionCompleted)
        {
            return;
        }

        CollectionCompleted?.Invoke(this, EventArgs.Empty);

        StatusText.Text = "Inventory loaded. Returning to main window...";

        var window = Window.GetWindow(this);
        window?.Close();
    }

    // =========================================================
    // DISCONNECT
    // =========================================================

    private void DisconnectSelectedMenuItem_Click(object sender, RoutedEventArgs e)
    {
        DisconnectSelected();
    }

    public void DisconnectSelected()
    {
        var selectedEntries = TargetDataGrid.SelectedItems
            .Cast<TargetEntry>()
            .ToList();

        if (selectedEntries.Count == 0)
        {
            ShowInfo("Please select at least one target first.", "Disconnect");
            return;
        }

        foreach (var entry in selectedEntries)
        {
            ResetEntry(entry);
        }

        _validationCompleted = false;
        _collectionCompleted = false;

        _targetsView.Refresh();
        UpdateTargetStatistics();
        UpdateActionButtons();

        StatusText.Text = selectedEntries.Count == 1
            ? $"Disconnected from {selectedEntries[0].Name}."
            : $"Disconnected {selectedEntries.Count} target(s).";
    }

    public void DisconnectAll()
    {
        if (_targets.Count == 0)
        {
            return;
        }

        foreach (var target in _targets)
        {
            ResetEntry(target);
        }

        _validationCompleted = false;
        _collectionCompleted = false;

        _targetsView.Refresh();
        UpdateTargetStatistics();
        UpdateActionButtons();

        StatusText.Text = "All targets disconnected.";
    }

    private static void ResetEntry(TargetEntry entry)
    {
        entry.ValidationStatus = TargetValidationStatus.Pending;
        entry.NameResolved = false;
        entry.ResolvedAddress = string.Empty;
        entry.PingSucceeded = false;
        entry.HyperVConnectionSucceeded = false;
        entry.IsCluster = false;
        entry.ClusterName = string.Empty;
        entry.Result = "Disconnected";
        entry.ErrorMessage = string.Empty;
    }

    // =========================================================
    // IMPORT
    // =========================================================

    private void ImportButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Import Hyper-V Target List",
            Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
            Multiselect = false
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            var lines = File.ReadAllLines(dialog.FileName);
            TargetInputTextBox.Text = string.Join(Environment.NewLine, lines);
            AddTargetsFromInput();

            StatusText.Text = $"Imported {lines.Length} line(s).";
        }
        catch (Exception ex)
        {
            ShowError(ex.Message, "Import Error");
        }
    }

    // =========================================================
    // ADD TARGETS
    // =========================================================

    private void TargetInputTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control)
        {
            AddTargetsFromInput();
            e.Handled = true;
        }
    }

    private void AddTargetsButton_Click(object sender, RoutedEventArgs e)
    {
        AddTargetsFromInput();
    }

    private void AddTargetsFromInput()
    {
        var lines = TargetInputTextBox.Text
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToArray();

        if (lines.Length == 0)
        {
            ShowInfo("Enter at least one hostname, FQDN, IP address, or cluster name.", "Add Targets");
            return;
        }

        var normalized = _targetManager.Normalize(lines);
        var addedCount = 0;

        foreach (var target in normalized)
        {
            var alreadyExists = _targets.Any(existing =>
                existing.Name.Equals(target.Name, StringComparison.OrdinalIgnoreCase));

            if (alreadyExists)
            {
                continue;
            }

            _targets.Add(new TargetEntry
            {
                Name = target.Name,
                Type = target.Type,
                ValidationStatus = TargetValidationStatus.Pending,
                Result = "Pending"
            });

            addedCount++;
        }

        _validationCompleted = false;
        _collectionCompleted = false;

        TargetInputTextBox.Clear();
        _targetsView.Refresh();

        UpdateTargetStatistics();
        UpdateActionButtons();

        StatusText.Text = addedCount > 0
            ? $"Added {addedCount} new target(s). {_targets.Count} total."
            : "No new targets added (duplicates skipped).";
    }

    // =========================================================
    // REMOVE / CLEAR
    // =========================================================

    private void RemoveSelectedButton_Click(object sender, RoutedEventArgs e)
    {
        var selectedEntries = TargetDataGrid.SelectedItems
            .Cast<TargetEntry>()
            .ToList();

        if (selectedEntries.Count == 0)
        {
            ShowInfo("Please select at least one target to remove.", "Remove Selected");
            return;
        }

        foreach (var entry in selectedEntries)
        {
            _targets.Remove(entry);
        }

        _validationCompleted = false;
        _collectionCompleted = false;

        _targetsView.Refresh();
        UpdateTargetStatistics();
        UpdateActionButtons();

        StatusText.Text = $"Removed {selectedEntries.Count} target(s).";
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        if (_targets.Count == 0)
        {
            return;
        }

        var confirm = MessageBox.Show(
            $"Remove all {_targets.Count} target(s)? This cannot be undone.",
            "Clear All Targets",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirm != MessageBoxResult.Yes)
        {
            return;
        }

        _targets.Clear();
        TargetInputTextBox.Clear();

        _validationCompleted = false;
        _collectionCompleted = false;

        _targetsView.Refresh();
        UpdateTargetStatistics();
        UpdateActionButtons();

        StatusText.Text = "Targets cleared.";
    }

    private void CopyNamesMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var names = TargetDataGrid.SelectedItems
            .Cast<TargetEntry>()
            .Select(entry => entry.Name)
            .ToList();

        if (names.Count == 0)
        {
            return;
        }

        Clipboard.SetText(string.Join(Environment.NewLine, names));
        StatusText.Text = $"Copied {names.Count} name(s) to clipboard.";
    }

    // =========================================================
    // OPERATION LIFECYCLE
    // =========================================================

    private CancellationTokenSource BeginOperation()
    {
        SetControlsEnabled(false);

        CancelButton.IsEnabled = true;
        OperationProgressBar.Visibility = Visibility.Visible;
        OperationProgressBar.IsIndeterminate = true;

        _operationCts = new CancellationTokenSource();
        return _operationCts;
    }

    private void EndOperation()
    {
        SetControlsEnabled(true);

        CancelButton.IsEnabled = false;
        OperationProgressBar.Visibility = Visibility.Collapsed;
        OperationProgressBar.IsIndeterminate = false;
        OperationProgressBar.Value = 0;

        _operationCts?.Dispose();
        _operationCts = null;
    }

    private void SetControlsEnabled(bool isEnabled)
    {
        ImportButton.IsEnabled = isEnabled;
        AddTargetsButton.IsEnabled = isEnabled;
        RemoveSelectedButton.IsEnabled = isEnabled;
        ClearButton.IsEnabled = isEnabled;
        ValidateButton.IsEnabled = isEnabled && _targets.Count > 0;
        CollectButton.IsEnabled = isEnabled && _validationCompleted && _targets.Any(IsReadyForCollection);
        GoToMainButton.IsEnabled = isEnabled && _collectionCompleted;
    }

    private void UpdateActionButtons()
    {
        var hasTargets = _targets.Count > 0;
        var hasReadyTargets = _targets.Any(IsReadyForCollection);

        ValidateButton.IsEnabled = hasTargets;
        CollectButton.IsEnabled = _validationCompleted && hasReadyTargets;
        GoToMainButton.IsEnabled = _collectionCompleted;
        CancelButton.IsEnabled = _operationCts != null;
    }

    private void SetProgress(int completed, int total)
    {
        if (total <= 0)
        {
            return;
        }

        OperationProgressBar.IsIndeterminate = false;
        OperationProgressBar.Maximum = total;
        OperationProgressBar.Value = completed;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        _operationCts?.Cancel();
        CancelButton.IsEnabled = false;
        StatusText.Text = "Cancelling...";
    }

    // =========================================================
    // STATISTICS
    // =========================================================

    private void UpdateTargetStatistics()
    {
        var count = _targets.Count;
        var workers = _targetManager.CalculateWorkerCount(count);
        var readyCount = _targets.Count(IsReadyForCollection);
        var failedCount = _targets.Count(t => t.ValidationStatus is
            TargetValidationStatus.Failed or
            TargetValidationStatus.NameResolutionFailed or
            TargetValidationStatus.PingFailed or
            TargetValidationStatus.ConnectionFailed or
            TargetValidationStatus.NotHyperV);

        TargetCountText.Text = $"{count} target{(count == 1 ? "" : "s")}";
        ReadyCountText.Text = readyCount > 0 ? $"{readyCount} ready" : string.Empty;
        FailedCountText.Text = failedCount > 0 ? $"{failedCount} failed" : string.Empty;
        WorkerCountText.Text = workers.ToString();
    }

    // =========================================================
    // HELPERS
    // =========================================================

    private static bool IsReadyForCollection(TargetEntry entry) =>
        entry.ValidationStatus is TargetValidationStatus.Ready or TargetValidationStatus.ClusterReady;

    private static string GetResultText(TargetValidationStatus status) =>
        status switch
        {
            TargetValidationStatus.Ready => "Ready",
            TargetValidationStatus.NameResolutionFailed => "DNS Failed",
            TargetValidationStatus.PingFailed => "Ping Failed",
            TargetValidationStatus.ConnectionFailed => "Hyper-V Connection Failed",
            TargetValidationStatus.NotHyperV => "Not Hyper-V",
            TargetValidationStatus.ClusterReady => "Cluster Ready",
            TargetValidationStatus.Completed => "Completed",
            TargetValidationStatus.Failed => "Failed",
            _ => status.ToString()
        };

    private void ShowInfo(string message, string title) =>
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);

    private void ShowError(string message, string title) =>
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);

    // =========================================================
    // WINDOWS CREDENTIALS
    // =========================================================

    private void UseCurrentWindowsCredentialsCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        var useCurrentCredentials = UseCurrentWindowsCredentialsCheckBox.IsChecked == true;

        if (UsernameTextBox == null || PasswordBox == null)
        {
            return;
        }

        UsernameTextBox.IsEnabled = !useCurrentCredentials;
        PasswordBox.IsEnabled = !useCurrentCredentials;
    }
}