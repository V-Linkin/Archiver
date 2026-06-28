using System.Globalization;
using Avalonia.Data.Converters;
using Gatherly.Windows.Services;

namespace Gatherly.Windows.Views.Converters;

/// <summary>
/// 视频缩略图转换器 — 使用 Windows Shell 从系统缩略图缓存提取视频首帧
/// 带内存缓存避免重复提取
/// </summary>
public class VideoThumbnailConverter : IValueConverter
{
    public static readonly VideoThumbnailConverter Instance = new();

    private static readonly Dictionary<string, Avalonia.Media.Imaging.Bitmap?> _cache = new();
    private static readonly object _lock = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string path || string.IsNullOrEmpty(path))
            return null;

        lock (_lock)
        {
            if (_cache.TryGetValue(path, out var cached))
                return cached;
        }

        var bitmap = VideoThumbnailProvider.TryGetThumbnail(path);

        lock (_lock)
        {
            _cache[path] = bitmap;
        }

        return bitmap;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
