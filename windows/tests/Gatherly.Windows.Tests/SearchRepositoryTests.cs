using Gatherly.Windows.Database;
using Gatherly.Windows.Models;
using Gatherly.Windows.Models.Enums;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Gatherly.Windows.Tests;

public class SearchRepositoryTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public SearchRepositoryTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        MigrationRunner.RunAll(_connection);
        // 插入 FTS 索引触发器需要的数据
    }

    public void Dispose()
    {
        _connection.Close();
        _connection.Dispose();
    }

    private void InsertItemWithFts(
        string id, string title, string body,
        string platform = "bilibili", string? deletedAt = null)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO items (id, title, body, original_url, platform, normalized_url,
                import_date, modify_date, content_status, archive_status, media_status, deleted_at)
            VALUES ($id, $title, $body, 'https://example.com', $platform,
                'https://example.com', 1700000000, 1700000000, 'normal', 'pending', 'textOnly', $deletedAt)";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.Parameters.AddWithValue("$title", title);
        cmd.Parameters.AddWithValue("$body", body);
        cmd.Parameters.AddWithValue("$platform", platform);
        cmd.Parameters.AddWithValue("$deletedAt", (object?)deletedAt ?? DBNull.Value);
        cmd.ExecuteNonQuery();

        // 同步 FTS 索引
        using var ftsCmd = _connection.CreateCommand();
        ftsCmd.CommandText = "INSERT INTO items_fts (rowid, title, body) VALUES ((SELECT rowid FROM items WHERE id=$id), $title, $body)";
        ftsCmd.Parameters.AddWithValue("$id", id);
        ftsCmd.Parameters.AddWithValue("$title", title);
        ftsCmd.Parameters.AddWithValue("$body", body);
        ftsCmd.ExecuteNonQuery();
    }

    [Fact]
    public async Task SearchAsync_EmptyQuery_ReturnsEmpty()
    {
        var repo = new SearchRepository(_connection);
        var results = await repo.SearchAsync("");
        Assert.Empty(results);
    }

    [Fact]
    public async Task SearchAsync_WhitespaceQuery_ReturnsEmpty()
    {
        var repo = new SearchRepository(_connection);
        var results = await repo.SearchAsync("   ");
        Assert.Empty(results);
    }

    [Fact]
    public async Task SearchAsync_FtsMatch_ReturnsItem()
    {
        InsertItemWithFts("00000000-0000-0000-0000-000000000001", "Bilibili Video", "Some video content");
        InsertItemWithFts("00000000-0000-0000-0000-000000000002", "YouTube Tutorial", "Another video");

        var repo = new SearchRepository(_connection);
        var results = await repo.SearchAsync("Bilibili");

        Assert.Single(results);
        Assert.Equal("00000000-0000-0000-0000-000000000001", results[0].Id.ToString());
    }

    [Fact]
    public async Task SearchAsync_FtsExcludesDeletedItems()
    {
        InsertItemWithFts("00000000-0000-0000-0000-000000000001", "Active Item", "Active content");
        InsertItemWithFts("00000000-0000-0000-0000-000000000002", "Deleted Item", "Deleted content",
            deletedAt: "1700000000");

        var repo = new SearchRepository(_connection);
        var results = await repo.SearchAsync("Item");

        // FTS5 index still contains the deleted item's rowid
        // but the JOIN condition filters it out
        Assert.Single(results);
        Assert.Equal("00000000-0000-0000-0000-000000000001", results[0].Id.ToString());
    }

    [Fact]
    public async Task SearchAsync_FtsNoResult_FallsBackToLike()
    {
        // FTS5 unicode61 splits Chinese into single characters,
        // so a multi-char Chinese phrase may not match via FTS.
        // LIKE fallback should catch it.
        InsertItemWithFts("00000000-0000-0000-0000-000000000001",
            "哔哩哔哩", "Bilibili platform");

        var repo = new SearchRepository(_connection);
        // Try a keyword that FTS might not match due to tokenization
        var results = await repo.SearchAsync("哔哩哔哩");

        // At minimum, LIKE fallback should return the item
        Assert.NotEmpty(results);
        Assert.Equal("00000000-0000-0000-0000-000000000001", results[0].Id.ToString());
    }

    [Fact]
    public async Task SearchAsync_LikeFallback_MatchesTitle()
    {
        // Insert item with a title unlikely to match via FTS tokenization
        // but should match via LIKE
        InsertItemWithFts("00000000-0000-0000-0000-000000000001",
            "UniqueXYZTitle", "Some body");

        var repo = new SearchRepository(_connection);
        var results = await repo.SearchAsync("UniqueXYZ");

        Assert.NotEmpty(results);
        Assert.Equal("00000000-0000-0000-0000-000000000001", results[0].Id.ToString());
    }

    [Fact]
    public async Task SearchAsync_LikeFallback_MatchesBody()
    {
        InsertItemWithFts("00000000-0000-0000-0000-000000000001",
            "Title", "SpecialBodyContent123");

        var repo = new SearchRepository(_connection);
        var results = await repo.SearchAsync("SpecialBody");

        Assert.NotEmpty(results);
    }

    [Fact]
    public async Task SearchAsync_FtsMultiKeyword_OR()
    {
        InsertItemWithFts("00000000-0000-0000-0000-000000000001", "Apple", "Fruit");
        InsertItemWithFts("00000000-0000-0000-0000-000000000002", "Banana", "Fruit");
        InsertItemWithFts("00000000-0000-0000-0000-000000000003", "Cherry", "Fruit");

        var repo = new SearchRepository(_connection);
        var results = await repo.SearchAsync("Apple Banana");

        Assert.Equal(2, results.Count);
    }

    [Fact]
    public async Task SearchAsync_NoMatch_ReturnsEmpty()
    {
        InsertItemWithFts("00000000-0000-0000-0000-000000000001", "Apple", "Fruit");

        var repo = new SearchRepository(_connection);
        var results = await repo.SearchAsync("NonExistentXYZ");

        Assert.Empty(results);
    }

    [Fact]
    public async Task SearchAsync_RespectsLimit()
    {
        for (int i = 0; i < 10; i++)
        {
            InsertItemWithFts(
                $"00000000-0000-0000-0000-{i:D12}",
                $"TestItem{i}",
                "SameBody");
        }

        var repo = new SearchRepository(_connection);
        var results = await repo.SearchAsync("TestItem", limit: 3);

        Assert.Equal(3, results.Count);
    }
}
