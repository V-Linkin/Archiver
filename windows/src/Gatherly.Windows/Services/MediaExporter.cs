using System.Text.RegularExpressions;
using Gatherly.Windows.Database;
using Gatherly.Windows.Models;
using Gatherly.Windows.Models.Enums;
using Gatherly.Windows.ViewModels;

namespace Gatherly.Windows.Services;

/// <summary>
/// 媒体导出器 — 对齐 macOS MediaExporter.swift
/// </summary>
public static class MediaExporter
{
    /// <summary>
    /// 生成导出文件名
    /// 格式: {平台名}_{文件夹}_{作者}_{序号}_{日期}.{扩展名}
    /// </summary>
    public static string GenerateExportName(
        string? platformName,
        string? folderName,
        string? author,
        int index,
        string fileExtension,
        DateTime? date = null)
    {
        var parts = new List<string>();
        var d = date ?? DateTime.Now;

        if (!string.IsNullOrEmpty(platformName))
            parts.Add(SanitizeFileName(platformName));
        if (!string.IsNullOrEmpty(folderName))
            parts.Add(SanitizeFileName(folderName));
        if (!string.IsNullOrEmpty(author))
            parts.Add(SanitizeFileName(author));

        parts.Add(index.ToString());
        parts.Add(d.ToString("yyyyMMdd"));

        var baseName = string.Join("_", parts);
        var ext = fileExtension.TrimStart('.');
        return $"{baseName}.{ext}";
    }

    /// <summary>
    /// 替换文件名中的非法字符为下划线
    /// </summary>
    public static string SanitizeFileName(string name)
    {
        var sanitized = Regex.Replace(name, @"[/\\:?*""<>|]", "_").Trim();
        return string.IsNullOrEmpty(sanitized) ? "unnamed" : sanitized;
    }

    /// <summary>
    /// 获取不重复的文件路径，已存在同名文件则追加 _1, _2 ...
    /// </summary>
    public static string GetUniqueFilePath(string directory, string fileName)
    {
        var ext = Path.GetExtension(fileName);
        var baseName = Path.GetFileNameWithoutExtension(fileName);
        var candidate = Path.Combine(directory, fileName);
        var counter = 1;

        while (File.Exists(candidate))
        {
            candidate = Path.Combine(directory, $"{baseName}_{counter}{ext}");
            counter++;
        }

        return candidate;
    }

    /// <summary>
    /// 批量导出媒体资产到用户选择的文件夹
    /// </summary>
    public static ExportResult ExportBatch(
        IReadOnlyList<MediaAssetDisplay> assets,
        string? platformName,
        string? folderName,
        string? author,
        string destDir)
    {
        if (assets.Count == 0)
            return new ExportResult(0, 0, "无可导出的媒体文件");

        try
        {
            Directory.CreateDirectory(destDir);
        }
        catch
        {
            return new ExportResult(0, assets.Count, "无法创建目标文件夹");
        }

        var successCount = 0;
        var failCount = 0;
        var index = 1;

        var sorted = assets
            .OrderBy(a => a.IsVideo ? 1 : 0)
            .ToList();

        foreach (var asset in sorted)
        {
            if (string.IsNullOrEmpty(asset.FullPath) || !asset.FileExists)
            {
                failCount++;
                continue;
            }

            var ext = Path.GetExtension(asset.FullPath);
            if (string.IsNullOrEmpty(ext))
                ext = ".bin";

            var fileName = GenerateExportName(platformName, folderName, author, index, ext.TrimStart('.'));
            var destPath = GetUniqueFilePath(destDir, fileName);

            try
            {
                File.Copy(asset.FullPath, destPath, false);
                successCount++;
                index++;
            }
            catch
            {
                failCount++;
            }
        }

        return new ExportResult(successCount, failCount,
            failCount == 0
                ? $"成功导出 {successCount} 个文件"
                : $"已导出 {successCount} 个文件，失败 {failCount} 个");
    }

    /// <summary>
    /// 从 Item 获取平台名
    /// </summary>
    public static string? GetPlatformName(Item item)
    {
        return item.DisplayPlatform;
    }

    /// <summary>
    /// 从 Item 获取文件夹名
    /// </summary>
    public static string? GetFolderName(Item item)
    {
        if (item.FolderId == null) return null;

        try
        {
            using var connection = new Microsoft.Data.Sqlite.SqliteConnection(
                $"Data Source={DatabasePaths.DatabaseFile}");
            connection.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT name FROM folders WHERE id = $id";
            cmd.Parameters.AddWithValue("$id", item.FolderId.Value);
            var result = cmd.ExecuteScalar();
            return result?.ToString();
        }
        catch
        {
            return null;
        }
    }
}

/// <summary>
/// 导出结果
/// </summary>
public record ExportResult(int SuccessCount, int FailCount, string Message);
