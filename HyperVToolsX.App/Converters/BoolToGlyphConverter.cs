using System.Globalization;
using System.Windows.Data;

namespace HyperVToolsX.App.Converters;

/// <summary>
/// Renders a bool as a check/cross glyph for compact grid display.
/// Use ConverterParameter="Blank" to show an empty string instead of a cross for false
/// (useful for columns that haven't been evaluated yet, e.g. before validation runs).
/// </summary>
public class BoolToGlyphConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var boolValue = value is bool b && b;

        if (boolValue)
        {
            return "\u2714"; // ✔
        }

        return string.Equals(parameter as string, "Blank", StringComparison.OrdinalIgnoreCase)
            ? string.Empty
            : "\u2716"; // ✖
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}