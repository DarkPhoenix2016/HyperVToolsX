using System.Diagnostics;
using System.Reflection;
using System.Windows;

namespace HyperVToolsX.App.Views;

public partial class AboutWindow : Window
{
    private const string GitHubUrl =
        "https://github.com/DarkPhoenix2016/HyperVToolsX";

    private const string ReleasesUrl =
        "https://github.com/DarkPhoenix2016/HyperVToolsX/releases";

    private const string IssuesUrl =
        "https://github.com/DarkPhoenix2016/HyperVToolsX/issues";

    public AboutWindow()
    {
        InitializeComponent();

        var assembly = Assembly.GetExecutingAssembly();

        var version =
            assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion;

        if (string.IsNullOrWhiteSpace(version))
        {
            version = assembly.GetName().Version?.ToString();
        }

        VersionText.Text = version ?? "1.0.0";
    }

    private void GitHubTextBlock_MouseLeftButtonUp(
        object sender,
        System.Windows.Input.MouseButtonEventArgs e)
    {
        OpenUrl(GitHubUrl);
    }

    private void ReleasesTextBlock_MouseLeftButtonUp(
        object sender,
        System.Windows.Input.MouseButtonEventArgs e)
    {
        OpenUrl(ReleasesUrl);
    }

    private void IssuesTextBlock_MouseLeftButtonUp(
        object sender,
        System.Windows.Input.MouseButtonEventArgs e)
    {
        OpenUrl(IssuesUrl);
    }

    private void OkButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        Close();
    }

    private static void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Unable to open the link.\n\n{ex.Message}",
                "HyperVToolsX",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }
}