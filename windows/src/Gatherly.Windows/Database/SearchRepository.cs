using Gatherly.Windows.Models;
using Gatherly.Windows.Models.Enums;
using Microsoft.Data.Sqlite;

namespace Gatherly.Windows.Database;

/// <summary>
/// 搜索数据访问层 — FTS5 + LIKE fallback
/// 注意: FTS5 unicode61 tokenizer 对中文分词为单字粒度，英文搜索正常。
/// </summary>
public class SearchRepository
{
    private readonly SqliteConnection _connection;

    public SearchRepository(SqliteConnection connection)
    {
        _connection = connection;
    }

    /// <summary>
    /// 搜索内容，返回匹配的 Item 列表。
    /// 使用 LIKE 搜索所有字段，确保连续子串匹配。
    /// </summary>
    public async Task<List<Item>> SearchAsync(string query, int limit = 100)
    {
        var trimmed = query.Trim();
        if (string.IsNullOrEmpty(trimmed))
            return new List<Item>();

        // 使用 LIKE 搜索所有字段，确保连续子串匹配
        return await SearchLikeAsync(trimmed, limit);
    }

    private async Task<List<Item>> SearchLikeAsync(string query, int limit)
    {
        // Split query into keywords for OR matching
        var keywords = query
            .Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(k => $"%{k}%")
            .ToArray();

        if (keywords.Length == 0)
            return new List<Item>();

        // Build OR conditions for each keyword
        var conditions = string.Join(" OR ",
            keywords.Select((_, i) => $"(title LIKE $kw{i} OR body LIKE $kw{i} OR original_url LIKE $kw{i} OR normalized_url LIKE $kw{i} OR author LIKE $kw{i} OR platform_content_id LIKE $kw{i})"));

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = $@"
            SELECT id, title, body, original_url, platform,
                platform_content_id, normalized_url, author, author_id,
                publish_date, import_date, modify_date, content_status,
                archive_status, media_status, cover_asset_id, folder_id,
                remark, is_starred, version, deleted_at, custom_platform_id
            FROM items
            WHERE deleted_at IS NULL
              AND ({conditions})
            ORDER BY import_date DESC
            LIMIT $limit";

        for (int i = 0; i < keywords.Length; i++)
            cmd.Parameters.AddWithValue($"$kw{i}", keywords[i]);
        cmd.Parameters.AddWithValue("$limit", limit);

        return await ReadItemsAsync(cmd);
    }

    private async Task<List<Item>> ReadItemsAsync(SqliteCommand cmd)
    {
        using var reader = await cmd.ExecuteReaderAsync();
        var items = new List<Item>();
        while (await reader.ReadAsync())
        {
            items.Add(SqliteRowMapper.ReadItem(reader));
        }
        return items;
    }
}
