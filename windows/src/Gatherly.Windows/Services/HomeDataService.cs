using Gatherly.Windows.Database;
using Gatherly.Windows.Models;
using Gatherly.Windows.Models.Enums;
using Gatherly.Windows.ViewModels;
using Microsoft.Data.Sqlite;

namespace Gatherly.Windows.Services;

/// <summary>
/// 首页数据服务 — 读取最近内容、首图路径、平台统计
/// </summary>
public class HomeDataService
{
    private readonly ItemRepository _itemRepo;
    private readonly MediaRepository _mediaRepo;
    private readonly CustomPlatformRepository _customPlatformRepo;
    private readonly SqliteConnection _connection;

    public HomeDataService(ItemRepository itemRepo, MediaRepository mediaRepo,
        CustomPlatformRepository customPlatformRepo, SqliteConnection connection)
    {
        _itemRepo = itemRepo;
        _mediaRepo = mediaRepo;
        _customPlatformRepo = customPlatformRepo;
        _connection = connection;
    }

    /// <summary>
    /// 获取最近导入的内容，按 importDate 倒序
    /// </summary>
    public async Task<List<Item>> GetRecentItemsAsync(int limit = 20)
    {
        return await _itemRepo.GetRecentAsync(limit);
    }

    /// <summary>
    /// 获取指定 item 的首张图片路径（优先本地，fallback 到远程 URL）
    /// </summary>
    public async Task<string?> GetFirstImagePathAsync(Guid itemId)
    {
        var assets = await _mediaRepo.GetByItemIdAsync(itemId);
        var first = assets.FirstOrDefault(a =>
            a.Type == MediaType.cover || a.Type == MediaType.image);
        if (first == null) return null;
        // 优先本地路径
        if (!string.IsNullOrEmpty(first.LocalPath))
            return MediaPathHelper.ResolveFullPath(first.LocalPath);
        // fallback 到远程 URL
        if (!string.IsNullOrEmpty(first.RemoteUrl))
            return first.RemoteUrl;
        return null;
    }

    /// <summary>
    /// 批量获取多个 item 的首图路径
    /// </summary>
    public async Task<Dictionary<Guid, string>> GetFirstImagePathsAsync(IEnumerable<Guid> itemIds)
    {
        var result = new Dictionary<Guid, string>();
        foreach (var id in itemIds)
        {
            var path = await GetFirstImagePathAsync(id);
            if (path != null)
                result[id] = path;
        }
        return result;
    }

    /// <summary>
    /// 批量填充 item 的自定义平台名称
    /// </summary>
    public async Task FillCustomPlatformNamesAsync(List<Item> items)
    {
        var customPlatforms = await _customPlatformRepo.GetAllAsync();
        var platformDict = customPlatforms.ToDictionary(cp => cp.Id, cp => cp.Name);

        foreach (var item in items)
        {
            if (item.Platform == Platform.custom && item.CustomPlatformId != null)
            {
                if (platformDict.TryGetValue(item.CustomPlatformId.Value, out var name))
                    item.CustomPlatformName = name;
            }
        }
    }

    /// <summary>
    /// 获取平台入口统计（自定义平台 + 内置平台 + 全部）
    /// </summary>
    public async Task<List<PlatformEntryDisplay>> GetPlatformStatsAsync()
    {
        var result = new List<PlatformEntryDisplay>();

        // 自定义平台
        var customPlatforms = await _customPlatformRepo.GetAllAsync();
        foreach (var cp in customPlatforms)
        {
            var count = await GetCustomPlatformItemCountAsync(cp.Id);
            string? logoFullPath = null;
            if (!string.IsNullOrEmpty(cp.LogoPath))
            {
                logoFullPath = Path.Combine(
                    Gatherly.Windows.Database.DatabasePaths.DataDirectory,
                    "platform_logos", cp.LogoPath);
                if (!File.Exists(logoFullPath)) logoFullPath = null;
            }
            result.Add(new PlatformEntryDisplay
            {
                Id = cp.Id,
                Name = cp.Name,
                Count = count,
                LogoPath = logoFullPath
            });
        }

        // 未分类内容（始终显示）
        var uncategorizedCount = await GetUncategorizedItemCountAsync();
        result.Add(new PlatformEntryDisplay
        {
            Name = "未分类内容",
            Count = uncategorizedCount,
            IsUncategorized = true
        });

        return result;
    }

    private async Task<int> GetTotalItemCountAsync()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM items WHERE deleted_at IS NULL";
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    private async Task<int> GetCustomPlatformItemCountAsync(Guid platformId)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM items WHERE custom_platform_id COLLATE NOCASE=$cpId AND deleted_at IS NULL";
        cmd.Parameters.AddWithValue("$cpId", platformId.ToString("D"));
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    private async Task<int> GetPlatformItemCountAsync(Platform platform)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM items WHERE platform=$platform AND deleted_at IS NULL AND custom_platform_id IS NULL";
        cmd.Parameters.AddWithValue("$platform", platform.ToRawValue());
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    private async Task<int> GetUncategorizedItemCountAsync()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM items WHERE platform='custom' AND custom_platform_id IS NULL AND deleted_at IS NULL";
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }
}
