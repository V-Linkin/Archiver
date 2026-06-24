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
        Timeout = TimeSpan.FromSeconds(60),
        DefaultRequestHeaders =
        {
            { "User-Agent", "Mozilla/5.0 (iPhone; CPU iPhone OS 16_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/16.0 Mobile/15E148 Safari/604.1" }
        }
    };

    private readonly MediaRepository _mediaRepo;

    public MediaDownloadService(MediaRepository mediaRepo)
    {
        _mediaRepo = mediaRepo;
    }

    /// <summary>
    /// 按平台 / 域名隔离设置 Referer，避免全局污染
    /// </summary>
    private static string? GetRefererForUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return null;
        var host = uri.Host;
        if (host.Contains("sinaimg.cn") || host.Contains("weibo.cn") || host.Contains("weibo.com"))
            return "https://weibo.com/";
        if (host.Contains("douyinpic.com") || host.Contains("douyin.com"))
            return "https://www.douyin.com/";
        if (host.Contains("xiaohongshu.com") || host.Contains("xhscdn.com"))
            return "https://www.xiaohongshu.com/";
        if (host.Contains("coolapk.com") || host.Contains("coolapk1s.com"))
            return "https://www.coolapk.com/";
        if (host.Contains("bilibili.com"))
            return "https://www.bilibili.com/";
        return null;
    }

    private static async Task<byte[]> DownloadBytesAsync(string url, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        var referer = GetRefererForUrl(url);
        if (referer != null)
            request.Headers.TryAddWithoutValidation("Referer", referer);
        var response = await SharedHttpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsByteArrayAsync(ct);
    }

    private static async Task<HttpResponseMessage> DownloadWithHeadersAsync(string url, HttpCompletionOption completionOption, CancellationToken ct)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        var referer = GetRefererForUrl(url);
        if (referer != null)
            request.Headers.TryAddWithoutValidation("Referer", referer);
        return await SharedHttpClient.SendAsync(request, completionOption, ct);
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
            var bytes = await DownloadBytesAsync(coverUrl, ct);
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

    /// <summary>
    /// 下载图片到本地并创建 media_asset 记录（用于图文笔记的图片列表）
    /// </summary>
    public async Task<MediaAsset?> DownloadImageAsync(Guid itemId, string imageUrl, int index, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(imageUrl))
            return null;

        try
        {
            var bytes = await DownloadBytesAsync(imageUrl, ct);
            if (bytes.Length == 0)
                return null;

            var ext = ".jpg";
            if (imageUrl.Contains(".png", StringComparison.OrdinalIgnoreCase))
                ext = ".png";
            else if (imageUrl.Contains(".webp", StringComparison.OrdinalIgnoreCase))
                ext = ".webp";

            var fileName = $"image_{index + 1}{ext}";
            var relativePath = $"{itemId}/{fileName}";
            var fullPath = Path.Combine(DatabasePaths.DataDirectory, "media", relativePath);

            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            await File.WriteAllBytesAsync(fullPath, bytes, ct);

            var asset = new MediaAsset
            {
                Id = Guid.NewGuid(),
                ItemId = itemId,
                Type = MediaType.image,
                LocalPath = relativePath,
                RemoteUrl = imageUrl,
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
            return null;
        }
    }

    /// <summary>
    /// 下载视频到本地并创建 media_asset 记录
    /// </summary>
    public async Task<MediaAsset?> DownloadVideoAsync(Guid itemId, string videoUrl, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(videoUrl))
            return null;

        try
        {
            var fileName = "video.mp4";
            var relativePath = $"{itemId}/{fileName}";
            var fullPath = Path.Combine(DatabasePaths.DataDirectory, "media", relativePath);

            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

            using var response = await DownloadWithHeadersAsync(videoUrl, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();

            long fileSize = 0;
            await using (var fs = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true))
            {
                await using var stream = await response.Content.ReadAsStreamAsync(ct);
                await stream.CopyToAsync(fs, ct);
                fileSize = fs.Length;
            }

            if (fileSize == 0)
            {
                File.Delete(fullPath);
                return null;
            }

            var asset = new MediaAsset
            {
                Id = Guid.NewGuid(),
                ItemId = itemId,
                Type = MediaType.video,
                LocalPath = relativePath,
                RemoteUrl = videoUrl,
                FileName = fileName,
                FileSize = fileSize,
                MimeType = "video/mp4",
                DownloadStatus = DownloadStatus.completed,
                CreatedAt = DateTimeOffset.UtcNow
            };

            await _mediaRepo.InsertAsync(asset);
            return asset;
        }
        catch
        {
            return null;
        }
    }
}
