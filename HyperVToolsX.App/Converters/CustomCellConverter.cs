using System.Globalization;
using System.Windows.Data;
using HyperVToolsX.Core.Enums;
using HyperVToolsX.Core.Templates;

namespace HyperVToolsX.App.Converters;

/// <summary>
/// Formats a custom-tab cell: lists and multi-row (collapsed) values become
/// "a; b", and size fields follow the current Data Size Unit.
/// </summary>
public class CustomCellConverter : IValueConverter
{
    private readonly SizeUnit? _sizeSourceUnit;

    public CustomCellConverter(SizeUnit? sizeSourceUnit = null)
    {
        _sizeSourceUnit = sizeSourceUnit;
    }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        CustomTabBuilder.JoinText(value, Scalar);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Binding.DoNothing;

    private string Scalar(object? item)
    {
        if (item is null)
        {
            return string.Empty;
        }

        if (_sizeSourceUnit is { } unit && item is IConvertible)
        {
            var bytes = SizeUnitMath.ToBytes(
                System.Convert.ToDouble(item, CultureInfo.InvariantCulture),
                unit);

            return ByteSizeConverter.FormatBytes(bytes);
        }

        return item.ToString() ?? string.Empty;
    }
}
