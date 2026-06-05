using System.Text.Json;
using Gatherly.Windows.Database;
using Gatherly.Windows.Models;
using Gatherly.Windows.Models.Enums;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Gatherly.Windows.Tests;

public class RepositoryReadTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public RepositoryReadTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        MigrationRunner.RunAll(_connection);
        // custom_platforms 表由 CustomPlatformRepository.setupTable() 创建，需手动建
        using (var cmd = _connection.CreateCommand())
        {
            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS custom_platforms (
                id TEXT PRIMARY KEY, name TEXT NOT NULL, logo_path TEXT,
                created_at REAL NOT NULL, sort_order INTEGER NOT NULL DEFAULT 0)";
            cmd.ExecuteNonQuery();
        }
    }

    public void Dispose()
    {
        _connection.Close();
        _connection.Dispose();
    }

    private void InsertTestItem(string id = "00000000-0000-0000-0000-000000000001", string? customPlatformId = null, string? deletedAt = null, string platform = "bilibili")
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO items (id, title, body, original_url, platform, normalized_url, 
                import_date, modify_date, content_status, archive_status, media_status, 
                custom_platform_id, deleted_at)
            VALUES ($id, 'Test Title', 'Test Body', 'https://example.com', $platform, 
                'https://example.com', 1700000000, 1700000000, 'normal', 'pending', 'textOnly',
                $cpId, $deletedAt)";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.Parameters.AddWithValue("$platform", platform);
        cmd.Parameters.AddWithValue("$cpId", (object?)customPlatformId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$deletedAt", (object?)deletedAt ?? DBNull.Value);
        cmd.ExecuteNonQuery();
    }

    private void InsertTestFolder(string id = "00000000-0000-0000-0000-000000000010", string? customPlatformId = null, string? parentId = null)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO folders (id, name, platform, created_at, sort_order, custom_platform_id, parent_id)
            VALUES ($id, 'Test Folder', 'custom', 1700000000, 0, $cpId, $parentId)";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.Parameters.AddWithValue("$cpId", (object?)customPlatformId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$parentId", (object?)parentId ?? DBNull.Value);
        cmd.ExecuteNonQuery();
    }

    private void InsertTestMediaAsset(string id = "00000000-0000-0000-0000-000000000020", string itemId = "00000000-0000-0000-0000-000000000001")
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO media_assets (id, item_id, type, file_name, file_size, download_status, created_at)
            VALUES ($id, $itemId, 'image', 'test.jpg', 1024, 'completed', 1700000000)";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.Parameters.AddWithValue("$itemId", itemId);
        cmd.ExecuteNonQuery();
    }

    private void InsertTestCustomPlatform(string id = "00000000-0000-0000-0000-000000000030", string name = "Test Platform")
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO custom_platforms (id, name, created_at, sort_order)
            VALUES ($id, $name, 1700000000, 0)";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.Parameters.AddWithValue("$name", name);
        cmd.ExecuteNonQuery();
    }

    [Fact]
    public async Task ItemRepository_GetByIdAsync_ReadsCorrectly()
    {
        InsertTestItem();
        var repo = new ItemRepository(_connection);
        var item = await repo.GetByIdAsync(Guid.Parse("00000000-0000-0000-0000-000000000001"));
        
        Assert.NotNull(item);
        Assert.Equal("Test Title", item!.Title);
        Assert.Equal("Test Body", item.Body);
        Assert.Equal("https://example.com", item.OriginalUrl);
        Assert.Equal(Platform.bilibili, item.Platform);
        Assert.Equal(ContentStatus.normal, item.ContentStatus);
        Assert.Equal(ArchiveStatus.pending, item.ArchiveStatus);
        Assert.Equal(MediaStatus.textOnly, item.MediaStatus);
        Assert.Equal(1700000000, item.ImportDate.ToUnixTimeSeconds());
    }

    [Fact]
    public async Task ItemRepository_GetByPlatformAsync_FiltersCorrectly()
    {
        InsertTestItem("00000000-0000-0000-0000-000000000001", platform: "bilibili");
        InsertTestItem("00000000-0000-0000-0000-000000000002", platform: "youtube");
        
        var repo = new ItemRepository(_connection);
        var bilibiliItems = await repo.GetByPlatformAsync(Platform.bilibili);
        var youtubeItems = await repo.GetByPlatformAsync(Platform.youtube);
        
        Assert.Single(bilibiliItems);
        Assert.Single(youtubeItems);
        Assert.Equal("00000000-0000-0000-0000-000000000001", bilibiliItems[0].Id.ToString());
        Assert.Equal("00000000-0000-0000-0000-000000000002", youtubeItems[0].Id.ToString());
    }

    [Fact]
    public async Task ItemRepository_GetUncategorizedItemsAsync_FiltersCorrectly()
    {
        InsertTestItem("00000000-0000-0000-0000-000000000003", customPlatformId: null, platform: "custom");
        InsertTestItem("00000000-0000-0000-0000-000000000004", customPlatformId: "00000000-0000-0000-0000-000000000004", platform: "custom");
        
        var repo = new ItemRepository(_connection);
        var uncategorized = await repo.GetUncategorizedItemsAsync();
        
        Assert.Single(uncategorized);
        Assert.Equal("00000000-0000-0000-0000-000000000003", uncategorized[0].Id.ToString());
    }

    [Fact]
    public async Task ItemRepository_ExcludesDeletedItems()
    {
        InsertTestItem("00000000-0000-0000-0000-000000000005");
        InsertTestItem("00000000-0000-0000-0000-000000000006", deletedAt: "1700000000");
        
        var repo = new ItemRepository(_connection);
        var items = await repo.GetRecentAsync();
        
        Assert.Single(items);
        Assert.Equal("00000000-0000-0000-0000-000000000005", items[0].Id.ToString());
    }

    [Fact]
    public async Task ItemRepository_GetTrashedAsync_IncludesDeletedItems()
    {
        InsertTestItem("00000000-0000-0000-0000-000000000005");
        InsertTestItem("00000000-0000-0000-0000-000000000006", deletedAt: "1700000000");
        
        var repo = new ItemRepository(_connection);
        var trashed = await repo.GetTrashedAsync();
        
        Assert.Single(trashed);
        Assert.Equal("00000000-0000-0000-0000-000000000006", trashed[0].Id.ToString());
    }

    [Fact]
    public async Task FolderRepository_GetByPlatformAsync_ReadsCorrectly()
    {
        InsertTestFolder("00000000-0000-0000-0000-000000000010");
        var repo = new FolderRepository(_connection);
        var folders = await repo.GetByPlatformAsync(Platform.custom);
        
        Assert.Single(folders);
        Assert.Equal("Test Folder", folders[0].Name);
    }

    [Fact]
    public async Task FolderRepository_GetByParentIdAsync_ReadsSubfolders()
    {
        InsertTestFolder("00000000-0000-0000-0000-000000000011");
        InsertTestFolder("00000000-0000-0000-0000-000000000012", parentId: "00000000-0000-0000-0000-000000000011");
        
        var repo = new FolderRepository(_connection);
        var subfolders = await repo.GetByParentIdAsync(Guid.Parse("00000000-0000-0000-0000-000000000011"));
        
        Assert.Single(subfolders);
        Assert.Equal("00000000-0000-0000-0000-000000000012", subfolders[0].Id.ToString());
    }

    [Fact]
    public async Task MediaRepository_GetByItemIdAsync_ReadsCorrectly()
    {
        InsertTestItem();
        InsertTestMediaAsset();
        
        var repo = new MediaRepository(_connection);
        var assets = await repo.GetByItemIdAsync(Guid.Parse("00000000-0000-0000-0000-000000000001"));
        
        Assert.Single(assets);
        Assert.Equal("test.jpg", assets[0].FileName);
        Assert.Equal(MediaType.image, assets[0].Type);
        Assert.Equal(1024, assets[0].FileSize);
    }

    [Fact]
    public async Task CustomPlatformRepository_GetAllAsync_ReadsCorrectly()
    {
        InsertTestCustomPlatform("00000000-0000-0000-0000-000000000030", "Platform A");
        InsertTestCustomPlatform("00000000-0000-0000-0000-000000000031", "Platform B");
        
        var repo = new CustomPlatformRepository(_connection);
        var platforms = await repo.GetAllAsync();
        
        Assert.Equal(2, platforms.Count);
    }

    [Fact]
    public async Task TrashRepository_ReadsMediaPaths()
    {
        var mediaPaths = new List<string> { "item-uuid/img1.jpg", "item-uuid/video1.mp4" };
        var mediaPathsJson = JsonSerializer.Serialize(mediaPaths);
        
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO items (id, original_url, platform, normalized_url, import_date, modify_date, content_status, archive_status, media_status, deleted_at)
            VALUES ('trashed-item', 'https://example.com', 'bilibili', 'https://example.com', 1700000000, 1700000000, 'trashed', 'pending', 'textOnly', 1700000000)";
        cmd.ExecuteNonQuery();
        
        using (var cmd2 = _connection.CreateCommand())
        {
            cmd2.CommandText = @"
                INSERT INTO trash_records (id, item_id, deleted_at, auto_delete_at, original_archive_status, media_paths)
                VALUES ('trash-1', 'trashed-item', 1700000000, 1702592000, 'pending', $mediaPaths)";
            cmd2.Parameters.AddWithValue("$mediaPaths", mediaPathsJson);
            cmd2.ExecuteNonQuery();
        }
        
        var repo = new TrashRepository(_connection);
        var records = await repo.GetAllAsync();
        
        Assert.Single(records);
        Assert.Equal(2, records[0].MediaPaths.Count);
        Assert.Equal("item-uuid/img1.jpg", records[0].MediaPaths[0]);
    }
}
