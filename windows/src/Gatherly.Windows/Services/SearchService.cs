using Gatherly.Windows.Database;
using Gatherly.Windows.Models;

namespace Gatherly.Windows.Services;

/// <summary>
/// 搜索服务 — 封装 SearchRepository 调用
/// </summary>
public class SearchService
{
    private readonly SearchRepository _searchRepo;

    public SearchService(SearchRepository searchRepo)
    {
        _searchRepo = searchRepo;
    }

    /// <summary>
    /// 搜索内容，返回匹配的 Item 列表
    /// </summary>
    public async Task<List<Item>> SearchAsync(string query, int limit = 100)
    {
        return await _searchRepo.SearchAsync(query, limit);
    }
}
