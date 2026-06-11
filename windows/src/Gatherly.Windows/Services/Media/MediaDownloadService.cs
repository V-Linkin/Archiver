using System.Net.Http;
using Gatherly.Windows.Database;
using Gatherly.Windows.Models;
using Gatherly.Windows.Models.Enums;

namespace Gatherly.Windows.Services.Media;

/// <summary>
/// 媒体下载服务 — 下载封面图片到本地
/// </summary>
public class MediaDownloadService
{
    private static readonly HttpClient SharedHttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(15),
        DefaultRequestHeaders =
        {
            { "User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36" },
            { "Referer", "https://www.bilibili.com/" }
        }
    };

    private readonly MediaRepository _mediaRepo;

    public MediaDownloadService(MediaRepository mediaRepo)
    {
        _mediaRepo = mediaRepo;
    }

    /// <summary>
    /// 下载封面图片到本地并创建 media_asset 记录
    /// 失败时不抛异常，返回 null
    /// </summary>
    public async Task<MediaAsset?> DownloadCoverAsync(Guid itemId, string coverUrl, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(coverUrl))
            return null;

        try
        {
            var bytes = await SharedHttpClient.GetByteArrayAsync(coverUrl, ct);
            if (bytes.Length == 0)
                return null;

            // 推断文件扩展名
            var ext = ".jpg";
            if (coverUrl.Contains(".png", StringComparison.OrdinalIgnoreCase))
                ext = ".png";
            else if (coverUrl.Contains(".webp", StringComparison.OrdinalIgnoreCase))
                ext = ".webp";

            var fileName = $"cover{ext}";
            var relativePath = $"{itemId}/{fileName}";
            var fullPath = Path.Combine(DatabasePaths.DataDirectory, "media", relativePath);

            // 确保目录存在
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

            // 写入文件
            await File.WriteAllBytesAsync(fullPath, bytes, ct);

            // 创建 media_asset 记录
            var asset = new MediaAsset
            {
                Id = Guid.NewGuid(),
                ItemId = itemId,
                Type = MediaType.cover,
                LocalPath = relativePath,
                RemoteUrl = coverUrl,
                FileName = fileName,
                FileSize = bytes.Length,
                MimeType = ext == ".png" ? "image/png" : ext == ".webp" ? "image/webp" : "image/jpeg",
                DownloadStatus = DownloadStatus.completed,
                CreatedAt = DateTimeOffset.UtcNow
            };

            await _mediaRepo.InsertAsync(asset);
            return asset;
        }
        catch
        {
            // 下载失败不抛异常
            return null;
        }
    }
}
