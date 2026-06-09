using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;

namespace Gatherly.Windows.Views.Converters;

/// <summary>
/// 将文件路径字符串转换为 Avalonia Bitmap
/// 文件不存在或路径为空时返回 null
/// </summary>
public class FilePathToBitmapConverter : IValueConverter
{
    public static FilePathToBitmapConverter Instance { get; } = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string path || string.IsNullOrWhiteSpace(path))
            return null;

        try
        {
            if (!File.Exists(path))
                return null;

            return new Bitmap(path);
        }
        catch
        {
            return null;
        }
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
