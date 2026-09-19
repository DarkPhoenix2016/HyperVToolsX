using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using HyperVToolsX.Core.Templates;

namespace HyperVToolsX.App.Views;

/// <summary>Dialog for building or editing a custom tab from the available inventory fields.</summary>
public partial class TemplateEditorWindow : Window
{
    private const string AllSources = "All tabs";

    private readonly IReadOnlyList<string> _otherNames;
    private readonly ObservableCollection<ColumnItem> _selected = [];

    private bool _loading = true;
    private bool _updatingHeader;

    public TemplateEditorWindow(
        IEnumerable<string> otherTemplateNames,
        CustomTabTemplate? existing = null)
    {
        InitializeComponent();

        _otherNames = otherTemplateNames.ToList();

        Title = existing is null ? "New Custom Tab" : $"Edit Custom Tab - {existing.Name}";

        RowSourceComboBox.ItemsSource = InventoryCatalog.RowSources.Select(RowOption.For).ToList();
        SelectedListBox.ItemsSource = _selected;

        if (existing is null)
        {
            RowSourceComboBox.SelectedIndex = 0;
        }
        else
        {
            NameTextBox.Text = existing.Name;
            RowSourceComboBox.SelectedItem = RowSourceComboBox.Items
                .OfType<RowOption>()
                .FirstOrDefault(o => string.Equals(o.Source, existing.Source, StringComparison.OrdinalIgnoreCase))
                ?? RowSourceComboBox.Items[0];
        }

        LoadSourceFilter();

        if (existing is not null)
        {
            // Resolving drops columns that no longer exist, exactly as the tab itself would.
            foreach (var column in CustomTabBuilder.ResolveColumns(existing))
            {
                _selected.Add(new ColumnItem(column.Source.Name, column.Field, column.Header));
            }
        }

        _loading = false;
        RefreshAvailable();
        UpdateHint();
    }

    /// <summary>The saved template; set when the dialog closes with DialogResult true.</summary>
    public CustomTabTemplate? Result { get; private set; }

    private string RowSource => (RowSourceComboBox.SelectedItem as RowOption)?.Source ?? InventoryCatalog.VmRowSource;

    private void LoadSourceFilter()
    {
        var sources = new List<string> { AllSources };
        sources.AddRange(InventoryCatalog.AllowedSources(RowSource));

        SourceFilterComboBox.ItemsSource = sources;
        SourceFilterComboBox.SelectedIndex = 0;
    }

    // ---------------------------------------------------------

    private void RefreshAvailable()
    {
        var filter = FilterTextBox.Text.Trim();
        var sourceFilter = SourceFilterComboBox.SelectedItem as string ?? AllSources;

        var taken = _selected
            .Select(c => (c.SourceName, c.Field.Name))
            .ToHashSet();

        var items = new List<FieldItem>();

        foreach (var sourceName in InventoryCatalog.AllowedSources(RowSource))
        {
            if (sourceFilter != AllSources && !string.Equals(sourceFilter, sourceName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (InventoryCatalog.Find(sourceName) is not { } source)
            {
                continue;
            }

            items.AddRange(source.Fields
                .Where(f => !taken.Contains((source.Name, f.Name)))
                .Where(f => filter.Length == 0
                    || f.DefaultHeader.Contains(filter, StringComparison.OrdinalIgnoreCase)
                    || f.Name.Contains(filter, StringComparison.OrdinalIgnoreCase))
                .Select(f => new FieldItem(source.Name, f)));
        }

        AvailableListBox.ItemsSource = items;
    }

    private void UpdateHint()
    {
        HintText.Text = InventoryCatalog.IsVmRowSource(RowSource)
            ? "One row per virtual machine. You can mix fields from any tab. When a VM has several entries in a tab " +
              "(for example two network adapters or several disks), they are combined into one cell, separated by \"; \"."
            : "This tab lists the rows of the selected source only.";
    }

    private void RowSourceComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading)
        {
            return;
        }

        // Which tabs' fields are allowed depends on the row source, so start the column list over.
        _selected.Clear();
        LoadSourceFilter();
        RefreshAvailable();
        UpdateHint();
    }

    private void SourceFilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_loading)
        {
            RefreshAvailable();
        }
    }

    private void FilterTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_loading)
        {
            RefreshAvailable();
        }
    }

    // ---------------------------------------------------------
    // Add / remove / reorder
    // ---------------------------------------------------------

    private void AddButton_Click(object sender, RoutedEventArgs e) => AddSelectedFields();

    private void AvailableListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is FrameworkElement { DataContext: FieldItem })
        {
            AddSelectedFields();
        }
    }

    private void AddSelectedFields()
    {
        var items = AvailableListBox.SelectedItems.OfType<FieldItem>().ToList();

        foreach (var item in items)
        {
            _selected.Add(new ColumnItem(
                item.SourceName,
                item.Field,
                InventoryCatalog.DefaultColumnHeader(RowSource, item.SourceName, item.Field)));
        }

        RefreshAvailable();
    }

    private void RemoveButton_Click(object sender, RoutedEventArgs e) => RemoveSelectedColumns();

    private void SelectedListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is FrameworkElement { DataContext: ColumnItem })
        {
            RemoveSelectedColumns();
        }
    }

    private void RemoveSelectedColumns()
    {
        foreach (var item in SelectedListBox.SelectedItems.OfType<ColumnItem>().ToList())
        {
            _selected.Remove(item);
        }

        RefreshAvailable();
    }

    private void MoveUpButton_Click(object sender, RoutedEventArgs e) => Move(-1);

    private void MoveDownButton_Click(object sender, RoutedEventArgs e) => Move(1);

    private void Move(int direction)
    {
        var items = SelectedListBox.SelectedItems.OfType<ColumnItem>()
            .OrderBy(_selected.IndexOf)
            .ToList();

        if (direction > 0)
        {
            items.Reverse();
        }

        foreach (var item in items)
        {
            var index = _selected.IndexOf(item);
            var target = index + direction;

            if (target < 0 || target >= _selected.Count || items.Contains(_selected[target]))
            {
                continue;
            }

            _selected.Move(index, target);
        }

        // Moving re-creates the visual selection; restore it.
        SelectedListBox.SelectedItems.Clear();

        foreach (var item in items)
        {
            SelectedListBox.SelectedItems.Add(item);
        }
    }

    // ---------------------------------------------------------
    // Header editing
    // ---------------------------------------------------------

    private void SelectedListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var single = SelectedListBox.SelectedItems.Count == 1
            ? SelectedListBox.SelectedItem as ColumnItem
            : null;

        _updatingHeader = true;
        HeaderTextBox.Text = single?.Header ?? string.Empty;
        HeaderTextBox.IsEnabled = single is not null;
        _updatingHeader = false;
    }

    private void HeaderTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_updatingHeader && SelectedListBox.SelectedItem is ColumnItem item)
        {
            item.Header = HeaderTextBox.Text;
        }
    }

    // ---------------------------------------------------------
    // Save
    // ---------------------------------------------------------

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        var error = TemplateNameValidator.Validate(NameTextBox.Text, _otherNames);

        if (error is null && _selected.Count == 0)
        {
            error = "Add at least one column to the tab.";
        }

        if (error is not null)
        {
            ErrorText.Text = error;
            ErrorText.Visibility = Visibility.Visible;
            return;
        }

        Result = new CustomTabTemplate
        {
            Name = NameTextBox.Text.Trim(),
            Source = RowSource,
            Columns = _selected
                .Select(c => new TemplateColumn
                {
                    Source = c.SourceName,
                    Field = c.Field.Name,
                    Header = string.IsNullOrWhiteSpace(c.Header)
                        ? InventoryCatalog.DefaultColumnHeader(RowSource, c.SourceName, c.Field)
                        : c.Header.Trim()
                })
                .ToList()
        };

        DialogResult = true;
    }

    // ---------------------------------------------------------

    private sealed record RowOption(string Source, string Display)
    {
        public static RowOption For(string source) => source switch
        {
            "vInfo" => new RowOption(source, "Virtual machines (one row per VM)"),
            "vHost" => new RowOption(source, "Hosts (vHost)"),
            "vCluster" => new RowOption(source, "Clusters (vCluster)"),
            "vHostStorage" => new RowOption(source, "Host storage (vHostStorage)"),
            "vOS" => new RowOption(source, "Operating systems (vOS)"),
            _ => new RowOption(source, source)
        };
    }

    private sealed record FieldItem(string SourceName, InventoryField Field)
    {
        public string Display => $"{Field.DefaultHeader}   [{SourceName}]";
    }

    private sealed class ColumnItem : INotifyPropertyChanged
    {
        private string _header;

        public ColumnItem(string sourceName, InventoryField field, string header)
        {
            SourceName = sourceName;
            Field = field;
            _header = header;
        }

        public string SourceName { get; }

        public InventoryField Field { get; }

        public string Display => $"{_header}   [{SourceName}]";

        public string Header
        {
            get => _header;
            set
            {
                _header = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Header)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Display)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
