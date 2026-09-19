using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using HyperVToolsX.Core.Enums;

namespace HyperVToolsX.App.Converters;

/// <summary>
/// Maps a TargetValidationStatus to a brush for status-at-a-glance coloring
/// in the target grid (foreground text or a status dot — see XAML usage).
/// </summary>
public class StatusToBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush Success = new(Color.FromRgb(0x1E, 0x8E, 0x3E));
    private static readonly SolidColorBrush Warning = new(Color.FromRgb(0xB0, 0x6A, 0x00));
    private static readonly SolidColorBrush Failure = new(Color.FromRgb(0xC4, 0x2B, 0x2B));
    private static readonly SolidColorBrush Neutral = new(Color.FromRgb(0x66, 0x66, 0x66));
    private static readonly SolidColorBrush Info = new(Color.FromRgb(0x1E, 0x6F, 0xC4));

    static StatusToBrushConverter()
    {
        Success.Freeze();
        Warning.Freeze();
        Failure.Freeze();
        Neutral.Freeze();
        Info.Freeze();
    }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not TargetValidationStatus status)
        {
            return Neutral;
        }

        return status switch
        {
            TargetValidationStatus.Ready => Success,
            TargetValidationStatus.ClusterReady => Success,
            TargetValidationStatus.Completed => Success,

            TargetValidationStatus.ResolvingName => Info,
            TargetValidationStatus.Pending => Neutral,

            TargetValidationStatus.NameResolutionFailed => Failure,
            TargetValidationStatus.PingFailed => Warning,
            TargetValidationStatus.ConnectionFailed => Failure,
            TargetValidationStatus.NotHyperV => Failure,
            TargetValidationStatus.Failed => Failure,

            _ => Neutral
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}