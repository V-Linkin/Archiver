using Gatherly.Windows.Database;

namespace Gatherly.Windows.Services;

/// <summary>
/// 媒体文件路径解析工具
/// </summary>
public static class MediaPathHelper
{
    /// <summary>
    /// 将 media_assets.local_path（相对路径）转换为本地完整路径
    /// local_path 格式：{item-uuid}/image_001.jpg 或 {item-uuid}/video.mp4
    /// </summary>
    public static string ResolveFullPath(string localPath)
    {
        return Path.Combine(DatabasePaths.DataDirectory, "media", localPath);
    }

    /// <summary>
    /// 检查媒体文件是否真实存在
    /// </summary>
    public static bool FileExists(string? localPath)
    {
        if (string.IsNullOrEmpty(localPath)) return false;
        return File.Exists(ResolveFullPath(localPath));
    }
}
