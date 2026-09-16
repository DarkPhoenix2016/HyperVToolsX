using HyperVToolsX.Core.Enums;
using HyperVToolsX.Core.Models;
using HyperVToolsX.Core.Collection;
using HyperVToolsX.Infrastructure.Collection;
using HyperVToolsX.Infrastructure.HyperV;
using HyperVToolsX.Infrastructure.PowerShellEngine;
using HyperVToolsX.Infrastructure.Validation;

using Microsoft.Win32;
using System.Collections;
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

    private readonly ObservableCollection<TargetEntry>
        _targets = [];

    public TargetManagerView(InventoryCache inventoryCache)
    {
        InitializeComponent();

        
        var powerShell =  new PowerShellExecutor();
        var hyperVProvider =new HyperVProvider(powerShell);
        var inventoryCollector = new BasicInventoryCollector(hyperVProvider);
        _targetManager = new TargetManager();
        _targetValidator = new TargetValidator(hyperVProvider);
        _inventoryCache =inventoryCache;
        _collectionOrchestrator = new CollectionOrchestrator( _targetValidator,inventoryCollector, _inventoryCache);

        TargetDataGrid.ItemsSource =_targets;

        UpdateTargetStatistics();
    }
    // Event handler for the "Collect" button click event.
    private async void CollectButton_Click(object sender, RoutedEventArgs e)
    {
        var readyEntries =
            _targets
                .Where(target =>
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
                    .Select(entry =>
                        new HyperVTarget
                        {
                            Name = entry.Name,
                            Address = entry.Name,
                            Type = entry.Type,
                            Validation =
                                new TargetValidationResult
                                {
                                    TargetName = entry.Name,
                                    Status =
                                        entry.ValidationStatus,
                                    NameResolved =
                                        entry.NameResolved,
                                    PingSucceeded =
                                        entry.PingSucceeded,
                                    HyperVConnectionSucceeded =
                                        entry.HyperVConnectionSucceeded
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
                            $"{collectionProgress.TotalTargets} " +
                            $"— {collectionProgress.CurrentTarget}";
                    });


            StatusText.Text =
                $"Starting collection for " +
                $"{targets.Count} target(s)...";


            var result =
                await _collectionOrchestrator.CollectAsync(
                    targets,
                    request,
                    progress);


            // ---------------------------------------------------------
            // UPDATE TARGET STATUS
            // ---------------------------------------------------------

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
                    Core.Enums.ConnectionStatus.Connected)
                {
                    entry.Result =
                        "Collection Completed";

                    entry.ValidationStatus =
                        TargetValidationStatus.Completed;
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
    // Event handler for the "Validate" button click event.
    private async void ValidateButton_Click(object sender, RoutedEventArgs e)
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

        StatusText.Text = "Validation started...";

        try
        {
            // Create a snapshot of the targets currently in the UI.
            var targetEntries = _targets.ToList();

            // Automatically determine worker count.
            var workerCount =
                _targetManager.CalculateWorkerCount(
                    targetEntries.Count);

            StatusText.Text =
                $"Validating {targetEntries.Count} target(s) " +
                $"using {workerCount} worker(s)...";

            // Shared queue for the workers.
            var queue = new Queue<TargetEntry>(
                targetEntries);

            // Lock protecting the shared queue.
            var syncLock = new object();

            // Create the required number of workers.
            var workers =
                Enumerable
                    .Range(0, workerCount)
                    .Select(_ => ValidateWorkerAsync())
                    .ToArray();

            // Wait for every worker to finish.
            await Task.WhenAll(workers);

            var readyCount =
                _targets.Count(target =>
                    target.ValidationStatus ==
                    TargetValidationStatus.Ready);

            var failedCount =
                _targets.Count(target =>
                    target.ValidationStatus !=
                    TargetValidationStatus.Ready);

            StatusText.Text =
                $"Validation completed. " +
                $"{readyCount} ready, {failedCount} failed.";

            CollectButton.IsEnabled =
                readyCount > 0;


            // ---------------------------------------------------------
            // WORKER
            // ---------------------------------------------------------

            async Task ValidateWorkerAsync()
            {
                while (true)
                {
                    TargetEntry? entry = null;

                    lock (syncLock)
                    {
                        if (queue.Count > 0)
                        {
                            entry = queue.Dequeue();
                        }
                    }

                    if (entry == null)
                    {
                        return;
                    }

                    await ValidateTargetAsync(entry);
                }
            }


            // ---------------------------------------------------------
            // TARGET VALIDATION
            // ---------------------------------------------------------

            async Task ValidateTargetAsync(
                TargetEntry entry)
            {
                try
                {
                    entry.ValidationStatus =
                        TargetValidationStatus.ResolvingName;

                    entry.Result =
                        "Resolving name...";

                    await RefreshTargetGridAsync();


                    var target =
                        new HyperVToolsX.Core.Models.HyperVTarget
                        {
                            Name = entry.Name,
                            Address = entry.Name,
                            Type = entry.Type
                        };


                    var validation =
                        await _targetValidator.ValidateAsync(
                            target);


                    entry.ValidationStatus =
                        validation.Status;

                    entry.NameResolved =
                        validation.NameResolved;

                    entry.PingSucceeded =
                        validation.PingSucceeded;

                    entry.HyperVConnectionSucceeded =
                        validation.HyperVConnectionSucceeded;

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
        }
    }

    // Method to disconnect the selected target in the data grid.
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
        selected.PingSucceeded = false;
        selected.HyperVConnectionSucceeded = false;

        selected.Result =
            "Disconnected";

        selected.ErrorMessage =
            string.Empty;

        TargetDataGrid.Items.Refresh();

        StatusText.Text =
            $"Disconnected from {selected.Name}.";
    }
    // Method to disconnect all targets in the data grid.
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
        target.PingSucceeded = false;
        target.HyperVConnectionSucceeded = false;

        target.Result =
            "Disconnected";

        target.ErrorMessage =
            string.Empty;
    }

    TargetDataGrid.Items.Refresh();

    CollectButton.IsEnabled = false;

    StatusText.Text =
        "All targets disconnected.";
}


    // Event handler for the "Import" button click event.
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
    // Event handler for the "Add Targets" button click event.
    private void AddTargetsButton_Click(object sender,RoutedEventArgs e)
    {
        AddTargetsFromInput();
    }
    // Method to add targets from the input textbox.
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
            _targetManager.Normalize(lines);

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
                    Name = target.Name,
                    Type = target.Type
                });
        }

        TargetInputTextBox.Clear();

        UpdateTargetStatistics();

        StatusText.Text =
            $"Added {_targets.Count} unique target(s).";
    }
    // Event handler for the "Clear" button click event.
    private void ClearButton_Click(object sender,RoutedEventArgs e)
    {
        _targets.Clear();

        TargetInputTextBox.Clear();

        CollectButton.IsEnabled = false;

        UpdateTargetStatistics();

        StatusText.Text = "Targets cleared.";
    }
    //  Method to update the target statistics displayed in the UI.
    private void UpdateTargetStatistics()
    {
        var count = _targets.Count;

        var workers =
            _targetManager.CalculateWorkerCount(count);

        TargetCountText.Text =
            $"{count} target{(count == 1 ? "" : "s")}";

        WorkerCountText.Text =
            workers.ToString();
    }

    // Method to refresh the target data grid asynchronously.
    private async Task RefreshTargetGridAsync()
    {
        await Dispatcher.InvokeAsync(
            () =>
            {
                TargetDataGrid.Items.Refresh();
            });
    }

    // Method to get a user-friendly result text based on the target validation status.
    private static string GetResultText(TargetValidationStatus status)
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

            _ =>
                status.ToString()
        };
    }
   
    
    // Event handler for the "Use Current Windows Credentials" checkbox change event.
    private void UseCurrentWindowsCredentialsCheckBox_Changed(object sender,RoutedEventArgs e)
    {
        var useCurrentCredentials =
            UseCurrentWindowsCredentialsCheckBox.IsChecked == true;

        if (UsernameTextBox == null || PasswordBox == null)
        {
            return;
        }

        UsernameTextBox.IsEnabled = !useCurrentCredentials;
        PasswordBox.IsEnabled = !useCurrentCredentials;
    }
}