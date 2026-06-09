using System.Globalization;
using Avalonia.Data.Converters;

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
