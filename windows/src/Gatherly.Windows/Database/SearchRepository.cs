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
    /// 优先使用 FTS5，无结果时 fallback 到 LIKE。
    /// </summary>
    public async Task<List<Item>> SearchAsync(string query, int limit = 100)
    {
        var trimmed = query.Trim();
        if (string.IsNullOrEmpty(trimmed))
            return new List<Item>();

        // 尝试 FTS5 搜索
        var ftsResults = await SearchFtsAsync(trimmed, limit);
        if (ftsResults.Count > 0)
            return ftsResults;

        // FTS 无结果，fallback LIKE（支持中文）
        return await SearchLikeAsync(trimmed, limit);
    }

    private async Task<List<Item>> SearchFtsAsync(string query, int limit)
    {
        var keywords = query
            .Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(k => $"\"{k}\"")
            .ToArray();

        if (keywords.Length == 0)
            return new List<Item>();

        var ftsQuery = string.Join(" OR ", keywords);

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            SELECT items.id, items.title, items.body, items.original_url, items.platform,
                items.platform_content_id, items.normalized_url, items.author, items.author_id,
                items.publish_date, items.import_date, items.modify_date, items.content_status,
                items.archive_status, items.media_status, items.cover_asset_id, items.folder_id,
                items.remark, items.is_starred, items.version, items.deleted_at,
                items.custom_platform_id
            FROM items_fts
            JOIN items ON items.rowid = items_fts.rowid
            WHERE items_fts MATCH $query
              AND items.deleted_at IS NULL
            ORDER BY items_fts.rank
            LIMIT $limit";
        cmd.Parameters.AddWithValue("$query", ftsQuery);
        cmd.Parameters.AddWithValue("$limit", limit);

        return await ReadItemsAsync(cmd);
    }

    private async Task<List<Item>> SearchLikeAsync(string query, int limit)
    {
        var pattern = $"%{query}%";

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            SELECT id, title, body, original_url, platform,
                platform_content_id, normalized_url, author, author_id,
                publish_date, import_date, modify_date, content_status,
                archive_status, media_status, cover_asset_id, folder_id,
                remark, is_starred, version, deleted_at, custom_platform_id
            FROM items
            WHERE deleted_at IS NULL
              AND (title LIKE $pattern OR body LIKE $pattern)
            ORDER BY import_date DESC
            LIMIT $limit";
        cmd.Parameters.AddWithValue("$pattern", pattern);
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
