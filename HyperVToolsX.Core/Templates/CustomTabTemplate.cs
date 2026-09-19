namespace HyperVToolsX.Core.Templates;

/// <summary>A user-defined tab: a chosen inventory source and an ordered set of its fields.</summary>
public class CustomTabTemplate
{
    /// <summary>Tab title, Excel sheet name and file name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The row source: "vInfo" means one row per virtual machine (columns may come
    /// from any per-VM tab); "vHost", "vCluster", "vHostStorage" and "vOS" list
    /// that tab's own rows.
    /// </summary>
    public string Source { get; set; } = string.Empty;

    public List<TemplateColumn> Columns { get; set; } = [];
}

public class TemplateColumn
{
    /// <summary>Tab the field comes from. Empty means the template's row source.</summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>Property name of the field within the source row type.</summary>
    public string Field { get; set; } = string.Empty;

    /// <summary>Column header shown in the grid and in Excel.</summary>
    public string Header { get; set; } = string.Empty;
}

public static class TemplateNameValidator
{
    public const int MaxLength = 31;

    private static readonly char[] InvalidChars = ['\\', '/', '?', '*', '[', ']', ':'];

    /// <summary>
    /// A template name doubles as a file name and an Excel sheet name, so it
    /// must satisfy both. Returns an error message, or null when valid.
    /// </summary>
    public static string? Validate(
        string? name,
        IEnumerable<string> otherTemplateNames)
    {
        name = name?.Trim();

        if (string.IsNullOrEmpty(name))
        {
            return "Enter a name for the tab.";
        }

        if (name.Length > MaxLength)
        {
            return $"The name can be at most {MaxLength} characters.";
        }

        if (name.IndexOfAny(InvalidChars) >= 0 || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            return "The name can't contain any of: \\ / ? * [ ] : \" < > |";
        }

        if (name.StartsWith('\'') || name.EndsWith('\'') || name.EndsWith('.'))
        {
            return "The name can't start or end with an apostrophe, or end with a period.";
        }

        if (string.Equals(name, "History", StringComparison.OrdinalIgnoreCase)
            || InventoryCatalog.IsSourceName(name))
        {
            return $"'{name}' is reserved. Choose a different name.";
        }

        if (otherTemplateNames.Any(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase)))
        {
            return $"A custom tab named '{name}' already exists.";
        }

        return null;
    }
}
