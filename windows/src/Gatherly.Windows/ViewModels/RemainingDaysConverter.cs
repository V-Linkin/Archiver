using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Gatherly.Windows.ViewModels;

/// <summary>
/// 将 DeletedAt (DateTimeOffset?) 转换为剩余天数 (int)
/// </summary>
public class RemainingDaysConverter : IValueConverter
{
    public static RemainingDaysConverter Instance { get; } = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is DateTimeOffset deletedAt)
        {
            var autoDeleteAt = deletedAt.AddDays(30);
            var remaining = autoDeleteAt - DateTimeOffset.UtcNow;
            var days = (int)remaining.TotalDays;
            return days < 0 ? 0 : days;
        }
        return null;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

/// <summary>
/// 将 DeletedAt 转换为剩余天数的颜色 Brush（<= 7 天为红色）
/// </summary>
public class RemainingDaysBrushConverter : IValueConverter
{
    public static RemainingDaysBrushConverter Instance { get; } = new();

    private static readonly SolidColorBrush RedBrush = new(Color.Parse("#D32F2F"));
    private static readonly SolidColorBrush SecondaryBrush = new(Color.Parse("#999999"));

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is DateTimeOffset deletedAt)
        {
            var autoDeleteAt = deletedAt.AddDays(30);
            var remaining = autoDeleteAt - DateTimeOffset.UtcNow;
            var days = (int)remaining.TotalDays;
            return days <= 7 ? RedBrush : SecondaryBrush;
        }
        return SecondaryBrush;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

/// <summary>
/// 将 DeletedAt 转换为剩余天数的 FontWeight
/// </summary>
public class RemainingDaysWeightConverter : IValueConverter
{
    public static RemainingDaysWeightConverter Instance { get; } = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is DateTimeOffset deletedAt)
        {
            var autoDeleteAt = deletedAt.AddDays(30);
            var remaining = autoDeleteAt - DateTimeOffset.UtcNow;
            var days = (int)remaining.TotalDays;
            return days <= 7 ? FontWeight.SemiBold : FontWeight.Normal;
        }
        return FontWeight.Normal;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
