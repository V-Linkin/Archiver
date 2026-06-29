using System.Globalization;
using Avalonia.Data.Converters;

namespace Gatherly.Windows.Views.Converters;

public class SortLabelConverter : IValueConverter
{
    public static readonly SortLabelConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is true ? "最新" : "最早";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

public class ViewModeIconConverter : IValueConverter
{
    public static readonly ViewModeIconConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is true ? "▦" : "☰";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
