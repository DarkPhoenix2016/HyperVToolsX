using System.Globalization;
using System.Windows.Data;
using HyperVToolsX.Core.Enums;

namespace HyperVToolsX.App.Converters;

public class ByteSizeConverter : IValueConverter
{
    public static SizeUnit CurrentUnit { get; set; } = SizeUnit.GB;

    public object Convert(
        object value,
        Type targetType,
        object parameter,
        CultureInfo culture)
    {
        if (value == null)
        {
            return string.Empty;
        }

        if (!TryGetNumber(value, out var number))
        {
            return value.ToString() ?? string.Empty;
        }

        // Values are normally stored in Bytes.
        // ConverterParameter can specify the source unit.
        var sourceUnit = parameter?.ToString() ?? "Bytes";

        var bytes = ConvertToBytes(number, sourceUnit);

        return FormatBytes(bytes);
    }

    public object ConvertBack(
        object value,
        Type targetType,
        object parameter,
        CultureInfo culture)
    {
        return Binding.DoNothing;
    }

    private static double ConvertToBytes(
        double value,
        string sourceUnit)
    {
        return sourceUnit.ToUpperInvariant() switch
        {
            "KB" => value * 1024d,

            "MB" => value * 1024d * 1024d,

            "GB" => value * 1024d * 1024d * 1024d,

            "TB" => value * 1024d * 1024d * 1024d * 1024d,

            "PB" => value * 1024d * 1024d * 1024d * 1024d * 1024d,

            _ => value
        };
    }

    public static string FormatBytes(double bytes)
    {
        return CurrentUnit switch
        {
            SizeUnit.Bytes =>
                $"{bytes:N0} Bytes",

            SizeUnit.KB =>
                $"{bytes / 1024d:0.##} KB",

            SizeUnit.MB =>
                $"{bytes / (1024d * 1024d):0.##} MB",

            SizeUnit.GB =>
                $"{bytes / (1024d * 1024d * 1024d):0.##} GB",

            SizeUnit.TB =>
                $"{bytes / (1024d * 1024d * 1024d * 1024d):0.##} TB",

            SizeUnit.PB =>
                $"{bytes / (1024d * 1024d * 1024d * 1024d * 1024d):0.##} PB",

            _ =>
                $"{bytes:N0} Bytes"
        };
    }

    private static bool TryGetNumber(
        object value,
        out double number)
    {
        switch (value)
        {
            case byte v:
                number = v;
                return true;

            case short v:
                number = v;
                return true;

            case int v:
                number = v;
                return true;

            case long v:
                number = v;
                return true;

            case float v:
                number = v;
                return true;

            case double v:
                number = v;
                return true;

            case decimal v:
                number = (double)v;
                return true;

            default:
                number = 0;
                return false;
        }
    }
}