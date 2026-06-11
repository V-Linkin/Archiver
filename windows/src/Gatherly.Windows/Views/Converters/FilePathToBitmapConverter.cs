using System.Globalization;
using System.Net.Http;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;

namespace Gatherly.Windows.Views.Converters;

/// <summary>
/// 将文件路径或 URL 字符串转换为 Avalonia Bitmap
/// 支持本地文件和 HTTP URL
/// </summary>
public class FilePathToBitmapConverter : IValueConverter
{
    public static FilePathToBitmapConverter Instance { get; } = new();

    private static readonly HttpClient SharedHttpClient = new() { Timeout = TimeSpan.FromSeconds(10) };

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string path || string.IsNullOrWhiteSpace(path))
            return null;

        try
        {
            // HTTP URL: 下载后加载
            if (path.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                var bytes = SharedHttpClient.GetByteArrayAsync(path).Result;
                using var stream = new MemoryStream(bytes);
                return new Bitmap(stream);
            }

            // 本地文件
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
