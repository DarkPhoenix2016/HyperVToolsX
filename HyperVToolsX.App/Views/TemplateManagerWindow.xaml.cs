using System.Windows;
using System.Windows.Input;
using HyperVToolsX.Core.Templates;
using HyperVToolsX.Infrastructure.Templates;

namespace HyperVToolsX.App.Views;

/// <summary>Lists, creates, edits and deletes the templates stored in the Templates folder.</summary>
public partial class TemplateManagerWindow : Window
{
    private readonly TemplateStore _store;
    private readonly List<CustomTabTemplate> _templates;

    public TemplateManagerWindow(TemplateStore store, IEnumerable<CustomTabTemplate> templates)
    {
        InitializeComponent();

        _store = store;
        _templates = templates.ToList();

        FolderText.Text = $"Templates are saved as XML files in: {store.Folder}";
        RefreshList();
    }

    /// <summary>True when any template was created, changed or deleted.</summary>
    public bool Changed { get; private set; }

    public IReadOnlyList<CustomTabTemplate> Templates => _templates;

    private void RefreshList()
    {
        TemplateListView.ItemsSource = null;
        TemplateListView.ItemsSource = _templates
            .Select(t => new Row(
                t.Name,
                InventoryCatalog.IsVmRowSource(t.Source) ? "One per VM" : t.Source,
                t.Columns.Count,
                t))
            .ToList();
    }

    private CustomTabTemplate? SelectedTemplate =>
        (TemplateListView.SelectedItem as Row)?.Template;

    private void NewButton_Click(object sender, RoutedEventArgs e)
    {
        var editor = new TemplateEditorWindow(_templates.Select(t => t.Name)) { Owner = this };

        if (editor.ShowDialog() == true && editor.Result is { } created && Persist(created, previousName: null))
        {
            _templates.Add(created);
            Changed = true;
            RefreshList();
        }
    }

    private void EditButton_Click(object sender, RoutedEventArgs e) => EditSelected();

    private void TemplateListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is FrameworkElement { DataContext: Row })
        {
            EditSelected();
        }
    }

    private void EditSelected()
    {
        if (SelectedTemplate is not { } current)
        {
            return;
        }

        var others = _templates.Where(t => t != current).Select(t => t.Name);
        var editor = new TemplateEditorWindow(others, current) { Owner = this };

        if (editor.ShowDialog() == true && editor.Result is { } updated && Persist(updated, previousName: current.Name))
        {
            _templates[_templates.IndexOf(current)] = updated;
            Changed = true;
            RefreshList();
        }
    }

    private void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedTemplate is not { } current)
        {
            return;
        }

        var confirm = MessageBox.Show(
            this,
            $"Delete the custom tab '{current.Name}'?\n\nIts XML file will be removed from the Templates folder.",
            "Delete Custom Tab",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirm != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            _store.Delete(current.Name);
            _templates.Remove(current);
            Changed = true;
            RefreshList();
        }
        catch (Exception ex)
        {
            ShowError($"Could not delete the template:\n{ex.Message}");
        }
    }

    private bool Persist(CustomTabTemplate template, string? previousName)
    {
        try
        {
            _store.Save(template, previousName);
            return true;
        }
        catch (Exception ex)
        {
            ShowError($"Could not save the template to {_store.Folder}:\n{ex.Message}");
            return false;
        }
    }

    private void ShowError(string message) =>
        MessageBox.Show(this, message, "Custom Tabs", MessageBoxButton.OK, MessageBoxImage.Error);

    private sealed record Row(string Name, string Rows, int ColumnCount, CustomTabTemplate Template);
}
