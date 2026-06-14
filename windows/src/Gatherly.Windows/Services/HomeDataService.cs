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

    /// <summary>
    /// 平台别名映射：canonicalKey → 标准平台
    /// </summary>
    private static readonly Dictionary<string, Platform> PlatformAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["youtube"] = Platform.youtube,
        ["youtu.be"] = Platform.youtube,
        ["bilibili"] = Platform.bilibili,
        ["b站"] = Platform.bilibili,
        ["哔哩哔哩"] = Platform.bilibili,
        ["github"] = Platform.github,
        ["douyin"] = Platform.douyin,
        ["抖音"] = Platform.douyin,
        ["xiaohongshu"] = Platform.xiaohongshu,
        ["小红书"] = Platform.xiaohongshu,
        ["coolapk"] = Platform.coolapk,
        ["酷安"] = Platform.coolapk,
        ["x"] = Platform.x,
        ["twitter"] = Platform.x,
        ["推特"] = Platform.x,
        ["weibo"] = Platform.weibo,
        ["微博"] = Platform.weibo,
        ["zhihu"] = Platform.zhihu,
        ["知乎"] = Platform.zhihu,
        ["douban"] = Platform.douban,
        ["豆瓣"] = Platform.douban,
    };

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
        if (!string.IsNullOrEmpty(first.LocalPath))
            return MediaPathHelper.ResolveFullPath(first.LocalPath);
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
    /// 判断平台是否支持合并查询（标准 + 自定义）
    /// </summary>
    private static bool SupportsMergedPlatform(Platform platform)
    {
        return platform == Platform.youtube || platform == Platform.bilibili;
    }

    /// <summary>
    /// 获取平台入口统计（合并标准平台 + custom_platforms + 未分类）
    /// 使用 canonical key 归一化，确保每个平台只有一个入口
    /// </summary>
    public async Task<List<PlatformEntryDisplay>> GetPlatformStatsAsync()
    {
        var result = new List<PlatformEntryDisplay>();

        // 收集所有 custom_platforms
        var customPlatforms = await _customPlatformRepo.GetAllAsync();

        // 建立 canonicalKey → custom platform ids 映射
        var canonicalToCustomIds = new Dictionary<string, List<Guid>>(StringComparer.OrdinalIgnoreCase);
        foreach (var cp in customPlatforms)
        {
            var key = GetCanonicalKey(cp.Name);
            if (!canonicalToCustomIds.ContainsKey(key))
                canonicalToCustomIds[key] = new List<Guid>();
            canonicalToCustomIds[key].Add(cp.Id);
        }

        // 遍历标准平台，合并对应 custom_platforms 的 count
        var standardPlatforms = new[]
        {
            Platform.github, Platform.bilibili, Platform.youtube,
            Platform.douyin, Platform.xiaohongshu, Platform.coolapk,
            Platform.x, Platform.weibo, Platform.zhihu, Platform.douban
        };

        var processedCanonicalKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var p in standardPlatforms)
        {
            var canonicalKey = p.ToRawValue();
            if (processedCanonicalKeys.Contains(canonicalKey))
                continue;

            // 获取匹配的 custom_platform ids
            var customIds = canonicalToCustomIds.GetValueOrDefault(canonicalKey) ?? new List<Guid>();

            // 根据平台类型选择 count 计算方式
            int totalCount;
            if (SupportsMergedPlatform(p) && customIds.Count > 0)
            {
                // YouTube/B站：合并标准 + 自定义
                totalCount = await GetPlatformItemCountAsync(p, customIds);
            }
            else if (customIds.Count > 0)
            {
                // 有自定义平台的标准平台：统计自定义平台 + 标准平台 items
                totalCount = await GetPlatformItemCountAsync(p, customIds);
            }
            else
            {
                // GitHub 等没有用户平台的标准平台：不显示在 sidebar
                // 其 items 将在未分类中显示
                totalCount = 0;
            }
            if (totalCount > 0)
            {
                result.Add(new PlatformEntryDisplay
                {
                    Name = p.GetDisplayName(),
                    Count = totalCount,
                    IsStandardPlatform = true,
                    StandardPlatform = p,
                    CustomPlatformIds = customIds
                });
            }

            processedCanonicalKeys.Add(canonicalKey);
        }

        // 添加没有匹配标准平台的 custom_platforms
        foreach (var cp in customPlatforms)
        {
            var key = GetCanonicalKey(cp.Name);
            if (processedCanonicalKeys.Contains(key))
                continue;

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
                LogoPath = logoFullPath,
                CustomPlatformIds = new List<Guid> { cp.Id }
            });

            processedCanonicalKeys.Add(key);
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

    /// <summary>
    /// 获取 canonical key（用于合并判断）
    /// </summary>
    private static string GetCanonicalKey(string name)
    {
        if (PlatformAliases.TryGetValue(name, out var platform))
            return platform.ToRawValue();
        return name.ToLowerInvariant();
    }

    private async Task<int> GetPlatformItemCountAsync(Platform platform, IEnumerable<Guid> customPlatformIds)
    {
        var ids = customPlatformIds.ToList();
        using var cmd = _connection.CreateCommand();

        if (ids.Count > 0)
        {
            var idConditions = string.Join(" OR ", ids.Select((_, i) => $"custom_platform_id COLLATE NOCASE = $cp{i}"));
            cmd.CommandText = $@"
                SELECT COUNT(*) FROM items
                WHERE deleted_at IS NULL
                  AND (platform=$platform OR {idConditions})";
            cmd.Parameters.AddWithValue("$platform", platform.ToRawValue());
            for (int i = 0; i < ids.Count; i++)
                cmd.Parameters.AddWithValue($"$cp{i}", ids[i].ToString("D"));
        }
        else
        {
            cmd.CommandText = "SELECT COUNT(*) FROM items WHERE deleted_at IS NULL AND platform=$platform";
            cmd.Parameters.AddWithValue("$platform", platform.ToRawValue());
        }

        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    private async Task<int> GetCustomPlatformItemCountAsync(Guid platformId)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM items WHERE custom_platform_id COLLATE NOCASE=$cpId AND deleted_at IS NULL";
        cmd.Parameters.AddWithValue("$cpId", platformId.ToString("D"));
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    private async Task<int> GetUncategorizedItemCountAsync()
    {
        using var cmd = _connection.CreateCommand();
        // 未分类 = 没有显式用户分类 AND 没有被可见平台入口认领
        cmd.CommandText = @"SELECT COUNT(*) FROM items 
            WHERE custom_platform_id IS NULL 
              AND deleted_at IS NULL
              AND lower(platform) NOT IN ('youtube', 'bilibili', 'github')";
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }
}
