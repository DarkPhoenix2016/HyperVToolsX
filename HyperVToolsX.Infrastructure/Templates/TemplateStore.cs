using System.Xml;
using System.Xml.Linq;
using HyperVToolsX.Core.Logging;
using HyperVToolsX.Core.Templates;

namespace HyperVToolsX.Infrastructure.Templates;

/// <summary>
/// Persists custom tab templates as one XML file per template inside a
/// "Templates" folder next to the application:
/// <code>
/// &lt;CustomTab Name="Capacity" Source="vInfo"&gt;
///   &lt;DataGrid&gt;
///     &lt;Column Source="vInfo" Field="Name" Header="VM Name" /&gt;
///     &lt;Column Source="vNetwork" Field="MacAddress" Header="MAC" /&gt;
///   &lt;/DataGrid&gt;
/// &lt;/CustomTab&gt;
/// </code>
/// <c>Source</c> on the root is the row source ("vInfo" = one row per VM).
/// A column without <c>Source</c> takes the root's, which keeps files written
/// by the first, single-source version loading unchanged.
/// </summary>
public class TemplateStore
{
    public const string FolderName = "Templates";

    private readonly ILiveLog _log;

    public TemplateStore(string folder, ILiveLog? log = null)
    {
        Folder = folder ?? throw new ArgumentNullException(nameof(folder));
        _log = log ?? NullLiveLog.Instance;
    }

    public string Folder { get; }

    /// <summary>The Templates folder beside the running executable.</summary>
    public static string DefaultFolder
    {
        get
        {
            var exeFolder = Path.GetDirectoryName(Environment.ProcessPath);

            return Path.Combine(
                string.IsNullOrEmpty(exeFolder) ? AppContext.BaseDirectory : exeFolder,
                FolderName);
        }
    }

    /// <summary>
    /// Creates the folder if it doesn't exist yet and loads every valid
    /// template in it. Unreadable files are logged and skipped, never fatal.
    /// </summary>
    public IReadOnlyList<CustomTabTemplate> LoadAll()
    {
        var templates = new List<CustomTabTemplate>();

        try
        {
            if (!Directory.Exists(Folder))
            {
                Directory.CreateDirectory(Folder);
                _log.Info("Templates", $"Created templates folder {Folder}");
                return templates;
            }

            foreach (var file in Directory.EnumerateFiles(Folder, "*.xml").Order(StringComparer.OrdinalIgnoreCase))
            {
                var template = TryLoad(file);

                if (template is null)
                {
                    continue;
                }

                if (templates.Any(t => string.Equals(t.Name, template.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    _log.Warn("Templates", $"Skipped {Path.GetFileName(file)}: a template named '{template.Name}' is already loaded");
                    continue;
                }

                templates.Add(template);
            }

            _log.Info("Templates", $"Loaded {templates.Count} custom tab template(s) from {Folder}");
        }
        catch (Exception ex)
        {
            _log.Error("Templates", $"Could not read templates folder {Folder}: {ex.Message}");
        }

        return templates;
    }

    /// <summary>
    /// Writes the template, replacing the file of <paramref name="previousName"/>
    /// when it was renamed.
    /// </summary>
    public void Save(CustomTabTemplate template, string? previousName = null)
    {
        ArgumentNullException.ThrowIfNull(template);

        Directory.CreateDirectory(Folder);

        var document = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XElement("CustomTab",
                new XAttribute("Name", template.Name),
                new XAttribute("Source", template.Source),
                new XElement("DataGrid",
                    template.Columns.Select(c => new XElement("Column",
                        new XAttribute("Source", string.IsNullOrWhiteSpace(c.Source) ? template.Source : c.Source),
                        new XAttribute("Field", c.Field),
                        new XAttribute("Header", c.Header))))));

        var path = PathFor(template.Name);
        var temp = path + ".tmp";

        document.Save(temp);
        File.Move(temp, path, overwrite: true);

        if (!string.IsNullOrEmpty(previousName)
            && !string.Equals(previousName, template.Name, StringComparison.OrdinalIgnoreCase))
        {
            Delete(previousName);
        }

        _log.Info("Templates", $"Saved custom tab '{template.Name}' to {path}");
    }

    public void Delete(string name)
    {
        var path = PathFor(name);

        if (File.Exists(path))
        {
            File.Delete(path);
            _log.Info("Templates", $"Deleted custom tab '{name}'");
        }
    }

    private string PathFor(string name) => Path.Combine(Folder, name.Trim() + ".xml");

    private CustomTabTemplate? TryLoad(string file)
    {
        var fileName = Path.GetFileName(file);

        try
        {
            var root = XDocument.Load(file).Root;

            if (root is null || root.Name.LocalName != "CustomTab")
            {
                _log.Warn("Templates", $"Skipped {fileName}: root element must be <CustomTab>");
                return null;
            }

            var name = ((string?)root.Attribute("Name"))?.Trim();
            var sourceName = (string?)root.Attribute("Source");

            var nameError = TemplateNameValidator.Validate(name, []);

            if (nameError is not null)
            {
                _log.Warn("Templates", $"Skipped {fileName}: {nameError}");
                return null;
            }

            var declared = InventoryCatalog.Find(sourceName);

            if (declared is null)
            {
                _log.Warn("Templates", $"Skipped {fileName}: unknown source '{sourceName}'");
                return null;
            }

            // The first version stored one source per tab, and a tab based on e.g.
            // vNetwork listed one row per adapter. Such files now become a
            // one-row-per-VM tab whose columns come from that source.
            var rowSource = InventoryCatalog.RowSources.Contains(declared.Name, StringComparer.OrdinalIgnoreCase)
                ? declared.Name
                : InventoryCatalog.VmRowSource;

            var template = new CustomTabTemplate { Name = name!, Source = rowSource };
            var allowed = InventoryCatalog.AllowedSources(rowSource);
            var seen = new HashSet<(string, string)>();

            foreach (var column in root.Element("DataGrid")?.Elements("Column") ?? [])
            {
                var columnSourceName = ((string?)column.Attribute("Source"))?.Trim();
                var columnSource = InventoryCatalog.Find(string.IsNullOrEmpty(columnSourceName) ? declared.Name : columnSourceName);
                var field = ((string?)column.Attribute("Field"))?.Trim() ?? string.Empty;

                if (columnSource is null || !allowed.Contains(columnSource.Name, StringComparer.OrdinalIgnoreCase))
                {
                    _log.Warn("Templates", $"{fileName}: ignoring column '{field}' from source '{columnSourceName}' (not available for this tab)");
                    continue;
                }

                var info = columnSource.Fields.FirstOrDefault(f => f.Name == field);

                if (info is null)
                {
                    _log.Warn("Templates", $"{fileName}: ignoring unknown field '{field}' of {columnSource.Name}");
                    continue;
                }

                if (!seen.Add((columnSource.Name, info.Name)))
                {
                    continue;
                }

                var header = ((string?)column.Attribute("Header"))?.Trim();

                template.Columns.Add(new TemplateColumn
                {
                    Source = columnSource.Name,
                    Field = info.Name,
                    Header = string.IsNullOrEmpty(header)
                        ? InventoryCatalog.DefaultColumnHeader(rowSource, columnSource.Name, info)
                        : header
                });
            }

            if (template.Columns.Count == 0)
            {
                _log.Warn("Templates", $"Skipped {fileName}: no valid columns");
                return null;
            }

            return template;
        }
        catch (Exception ex) when (ex is XmlException or IOException or UnauthorizedAccessException)
        {
            _log.Warn("Templates", $"Skipped {fileName}: {ex.Message}");
            return null;
        }
    }
}
