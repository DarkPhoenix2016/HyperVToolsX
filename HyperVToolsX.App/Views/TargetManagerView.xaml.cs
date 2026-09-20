using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using HyperVToolsX.Core.Collection;
using HyperVToolsX.Core.Enums;
using HyperVToolsX.Core.Interfaces;
using HyperVToolsX.Core.Logging;
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
    private readonly RemoteConnectionOptions _connectionOptions;
    private readonly LiveLog _liveLog;

    private const int MaxLogEntries = 10000;

    private readonly ObservableCollection<LiveLogEntry> _logEntries = [];
    private readonly DispatcherTimer _elapsedTimer;
    private readonly System.Diagnostics.Stopwatch _operationStopwatch = new();
    private string _operationName = string.Empty;

    private readonly ObservableCollection<TargetEntry> _targets = [];
    private readonly ICollectionView _targetsView;

    private CancellationTokenSource? _operationCts;
    private bool _validationCompleted;
    private bool _collectionCompleted;

    /// <summary>
    /// True once a collection has succeeded. The main window loads the cached
    /// inventory when this window is closed.
    /// </summary>
    public bool HasCollectedInventory => _collectionCompleted;

    private int _workerLimit = 1;
    private int _activeWorkers;

    public TargetManagerView(
        ITargetManager targetManager,
        ITargetValidator targetValidator,
        CollectionOrchestrator collectionOrchestrator,
        IInventoryCache inventoryCache,
        RemoteConnectionOptions connectionOptions,
        LiveLog liveLog)
    {
        InitializeComponent();

        _connectionOptions = connectionOptions;
        _liveLog = liveLog;

        LogDataGrid.ItemsSource = _logEntries;
        UpdateLogCount();

        Loaded += (_, _) => _liveLog.EntryWritten += LiveLog_EntryWritten;
        Unloaded += (_, _) =>
        {
            _liveLog.EntryWritten -= LiveLog_EntryWritten;

            // Closing the window mid-run must not leave workers running.
            _operationCts?.Cancel();

            // Don't keep the password in memory once the window is gone.
            _connectionOptions.Password = string.Empty;
            PasswordBox.Clear();
        };

        _elapsedTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _elapsedTimer.Tick += (_, _) => UpdateElapsedText();

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

        if (!TryApplyConnectionOptions())
        {
            return;
        }

        using var cts = BeginOperation("Collection");

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

            _workerLimit = request.MaxConcurrentTargets;
            SetWorkers(Math.Min(_workerLimit, targets.Count));

            var progress = new Progress<CollectionProgress>(p =>
            {
                SetProgress(p.CompletedTargets, p.TotalTargets);
                SetWorkers(Math.Min(_workerLimit, Math.Max(0, p.TotalTargets - p.CompletedTargets)));
                StatusText.Text = $"Collecting {p.CompletedTargets}/{p.TotalTargets}, {p.CurrentTarget}";
            });

            StatusText.Text = $"Starting collection for {targets.Count} target(s)...";

            _liveLog.Info(
                "UI",
                $"Start Collection clicked: {targets.Count} target(s), {request.MaxConcurrentTargets} worker(s) (auto, max {WorkerConfiguration.MaximumWorkers})");

            var token = cts.Token;

            var result = await Task.Run(
                () => _collectionOrchestrator.CollectAsync(
                    targets,
                    request,
                    progress,
                    token),
                token);

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
                $"Virtual machines: {result.TotalVirtualMachines}\n\n" +
                (result.SuccessfulTargets > 0
                    ? "Close the Target Manager to load the inventory into the main window."
                    : "No inventory was collected."),
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

        if (!TryApplyConnectionOptions())
        {
            return;
        }

        using var cts = BeginOperation("Validation");

        try
        {
            var targetEntries = _targets.ToList();
            var workerCount = _targetManager.CalculateWorkerCount(targetEntries.Count);
            _workerLimit = workerCount;
            SetWorkers(0);

            StatusText.Text = $"Validating {targetEntries.Count} target(s) using {workerCount} worker(s)...";

            _liveLog.Info(
                "UI",
                $"Validate clicked: {targetEntries.Count} target(s), {workerCount} worker(s) (auto, max {WorkerConfiguration.MaximumWorkers})");

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

            Dispatcher.Invoke(() => SetWorkers(Interlocked.Increment(ref _activeWorkers)));

            try
            {
                await ValidateTargetAsync(entry, cancellationToken);
            }
            finally
            {
                Dispatcher.Invoke(() => SetWorkers(Interlocked.Decrement(ref _activeWorkers)));
            }

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

            entry.Type = target.Type;
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
    // IMPORT
    // =========================================================

    // ---------------------------------------------------------
    // Select-all checkbox in the target grid header
    // ---------------------------------------------------------

    private void SelectAllCheckBox_Click(object sender, RoutedEventArgs e)
    {
        if (SelectAllCheckBox.IsChecked == true)
        {
            TargetDataGrid.SelectAll();
        }
        else
        {
            TargetDataGrid.UnselectAll();
        }
    }

    private void TargetDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var total = TargetDataGrid.Items.Count;
        var selected = TargetDataGrid.SelectedItems.Count;

        SelectAllCheckBox.IsChecked = total > 0 && selected == total
            ? true
            : selected == 0 ? false : null;
    }

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

    private CancellationTokenSource BeginOperation(string operationName)
    {
        SetControlsEnabled(false);

        _operationName = operationName;
        _operationStopwatch.Restart();
        ElapsedText.Visibility = Visibility.Visible;
        UpdateElapsedText();
        _elapsedTimer.Start();

        OperationProgressBar.Visibility = Visibility.Visible;
        OperationProgressBar.IsIndeterminate = true;

        _operationCts = new CancellationTokenSource();
        return _operationCts;
    }

    private void EndOperation()
    {
        _elapsedTimer.Stop();
        _operationStopwatch.Stop();
        ElapsedText.Text = $"{_operationName} finished in {FormatElapsed(_operationStopwatch.Elapsed)}";

        _liveLog.Info("UI", ElapsedText.Text);

        SetControlsEnabled(true);

        _activeWorkers = 0;
        SetWorkers(0);

        OperationProgressBar.Visibility = Visibility.Collapsed;
        OperationProgressBar.IsIndeterminate = false;
        OperationProgressBar.Value = 0;

        _operationCts?.Dispose();
        _operationCts = null;
    }

    private void SetWorkers(int active) =>
        WorkerCountText.Text = $"Workers: {active} active / {_workerLimit}";

    private void UpdateElapsedText() =>
        ElapsedText.Text = $"{_operationName}: {FormatElapsed(_operationStopwatch.Elapsed)}";

    private static string FormatElapsed(TimeSpan elapsed) =>
        elapsed.ToString(elapsed.TotalHours >= 1 ? @"hh\:mm\:ss" : @"mm\:ss");

    private void SetControlsEnabled(bool isEnabled)
    {
        ImportButton.IsEnabled = isEnabled;
        AddTargetsButton.IsEnabled = isEnabled;
        RemoveSelectedButton.IsEnabled = isEnabled;
        ClearButton.IsEnabled = isEnabled;
        ValidateButton.IsEnabled = isEnabled && _targets.Count > 0;
        CollectButton.IsEnabled = isEnabled && _validationCompleted && _targets.Any(IsReadyForCollection);
    }

    private void UpdateActionButtons()
    {
        var hasTargets = _targets.Count > 0;
        var hasReadyTargets = _targets.Any(IsReadyForCollection);

        ValidateButton.IsEnabled = hasTargets;
        CollectButton.IsEnabled = _validationCompleted && hasReadyTargets;
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
        if (_operationCts == null)
        {
            StatusText.Text = "Nothing is running.";
            return;
        }

        _liveLog.Warn("UI", $"Cancel requested for {_operationName.ToLowerInvariant()}");

        _operationCts.Cancel();
        StatusText.Text = "Cancelling...";
    }

    // =========================================================
    // LIVE LOG
    // =========================================================

    private void LiveLog_EntryWritten(object? sender, LiveLogEntry entry)
    {
        // Raised from worker threads; marshal to the UI thread.
        Dispatcher.BeginInvoke(DispatcherPriority.Background, () => AppendLog(entry));
    }

    private void AppendLog(LiveLogEntry entry)
    {
        _logEntries.Add(entry);

        if (_logEntries.Count > MaxLogEntries)
        {
            for (var i = 0; i < MaxLogEntries / 10; i++)
            {
                _logEntries.RemoveAt(0);
            }
        }

        UpdateLogCount();

        if (LogAutoScrollCheckBox.IsChecked == true)
        {
            LogDataGrid.ScrollIntoView(entry);
        }
    }

    private void UpdateLogCount() =>
        LogCountText.Text = $"{_logEntries.Count:N0} entr{(_logEntries.Count == 1 ? "y" : "ies")}";

    private static string FormatLogEntry(LiveLogEntry entry) =>
        $"{entry.Timestamp:yyyy-MM-dd HH:mm:ss.fff}\t{entry.Level}\t{entry.Source}\t{entry.Target}\t{entry.Message}";

    private void LogClearButton_Click(object sender, RoutedEventArgs e)
    {
        _logEntries.Clear();
        UpdateLogCount();
    }

    private void LogCopyButton_Click(object sender, RoutedEventArgs e)
    {
        var rows = LogDataGrid.SelectedItems.Count > 0
            ? LogDataGrid.SelectedItems.Cast<LiveLogEntry>()
            : _logEntries;

        var text = string.Join(Environment.NewLine, rows.Select(FormatLogEntry));

        if (text.Length > 0)
        {
            Clipboard.SetText(text);
        }
    }

    private void LogSaveButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Title = "Save Live Log",
            Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
            FileName = $"HyperVToolsX-log-{DateTime.Now:yyyyMMdd-HHmmss}.txt"
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            File.WriteAllLines(
                dialog.FileName,
                _logEntries.Select(FormatLogEntry),
                new UTF8Encoding(false));
        }
        catch (Exception ex)
        {
            ShowError(ex.Message, "Save Log");
        }
    }

    // =========================================================
    // STATISTICS
    // =========================================================

    private void UpdateTargetStatistics()
    {
        var count = _targets.Count;
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

        if (_operationCts == null)
        {
            _workerLimit = _targetManager.CalculateWorkerCount(count);
            SetWorkers(0);
        }
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

    /// <summary>
    /// Copies the Connection Settings tab into the shared options instance used
    /// by validation and collection. Returns false (after telling the user) if
    /// a field is invalid, so nothing runs with silently defaulted settings.
    /// </summary>
    private bool TryApplyConnectionOptions()
    {
        if (!int.TryParse(PortTextBox.Text, out var port) || port < 0 || port > 65535)
        {
            ShowError("Port must be a number from 0 to 65535 (0 = automatic).", "Connection Settings");
            return false;
        }

        if (!int.TryParse(TimeoutTextBox.Text, out var timeout) || timeout < 0)
        {
            ShowError("Timeout must be a non-negative number of seconds.", "Connection Settings");
            return false;
        }

        var useCurrent = UseCurrentWindowsCredentialsCheckBox.IsChecked == true;

        if (!useCurrent && string.IsNullOrWhiteSpace(UsernameTextBox.Text))
        {
            ShowError("Enter a username, or use the current Windows credentials.", "Connection Settings");
            return false;
        }

        // The password is wiped whenever the Target Manager closes, so it has to be re-entered.
        if (!useCurrent && PasswordBox.Password.Length == 0)
        {
            ShowError("Enter the password (it is cleared whenever this window is closed), or use the current Windows credentials.", "Connection Settings");
            return false;
        }

        _connectionOptions.UseCurrentCredentials = useCurrent;
        _connectionOptions.Username = useCurrent ? string.Empty : UsernameTextBox.Text.Trim();
        _connectionOptions.Password = useCurrent ? string.Empty : PasswordBox.Password;
        _connectionOptions.Authentication =
            (AuthenticationComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString()
            ?? RemoteConnectionOptions.DefaultAuthentication;
        _connectionOptions.UseSsl = UseSslCheckBox.IsChecked == true;
        _connectionOptions.Port = port;
        _connectionOptions.TimeoutSeconds = timeout;
        _connectionOptions.SkipCaCertificateCheck = SkipCaCertificateCheckBox.IsChecked == true;
        _connectionOptions.SkipCnCheck = SkipCnHostnameCheckBox.IsChecked == true;
        _connectionOptions.AllowTrustedHostsChange = AllowTrustedHostsCheckBox.IsChecked == true;

        return true;
    }

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