using Gatherly.Windows.Database;
using Gatherly.Windows.Models;
using Gatherly.Windows.Models.Enums;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Gatherly.Windows.Tests;

public class WriteRepositoryTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public WriteRepositoryTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        MigrationRunner.RunAll(_connection);
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

    private void InsertTestItem(string id = "00000000-0000-0000-0000-000000000001")
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO items (id, original_url, platform, normalized_url,
                import_date, modify_date, content_status, archive_status, media_status)
            VALUES ($id, 'https://example.com', 'bilibili', 'https://example.com',
                1700000000, 1700000000, 'normal', 'pending', 'textOnly')";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }

    // ==================== ItemRepository.UpdateAsync ====================

    [Fact]
    public async Task ItemRepository_UpdateAsync_UpdatesItem()
    {
        InsertTestItem();
        var repo = new ItemRepository(_connection);

        var item = await repo.GetByIdAsync(Guid.Parse("00000000-0000-0000-0000-000000000001"));
        Assert.NotNull(item);

        item!.ContentStatus = ContentStatus.trashed;
        item.DeletedAt = DateTimeOffset.UtcNow;
        item.Remark = "test remark";
        item.IsStarred = true;
        await repo.UpdateAsync(item);

        var updated = await repo.GetByIdAsync(Guid.Parse("00000000-0000-0000-0000-000000000001"));
        Assert.NotNull(updated);
        Assert.Equal(ContentStatus.trashed, updated!.ContentStatus);
        Assert.NotNull(updated.DeletedAt);
        Assert.Equal("test remark", updated.Remark);
        Assert.True(updated.IsStarred);
    }

    [Fact]
    public async Task ItemRepository_UpdateAsync_UpdatesFolderAndPlatform()
    {
        InsertTestItem();
        var repo = new ItemRepository(_connection);

        var item = await repo.GetByIdAsync(Guid.Parse("00000000-0000-0000-0000-000000000001"));
        Assert.NotNull(item);

        var newFolderId = Guid.NewGuid();
        var newCpId = Guid.NewGuid();
        item!.FolderId = newFolderId;
        item.CustomPlatformId = newCpId;
        await repo.UpdateAsync(item);

        var updated = await repo.GetByIdAsync(Guid.Parse("00000000-0000-0000-0000-000000000001"));
        Assert.Equal(newFolderId, updated!.FolderId);
        Assert.Equal(newCpId, updated.CustomPlatformId);
    }

    [Fact]
    public async Task ItemRepository_UpdateAsync_PreservesVersion()
    {
        InsertTestItem();
        var repo = new ItemRepository(_connection);

        var item = await repo.GetByIdAsync(Guid.Parse("00000000-0000-0000-0000-000000000001"));
        Assert.NotNull(item);
        Assert.Equal(1, item!.Version);

        item.Version = 5;
        await repo.UpdateAsync(item);

        var updated = await repo.GetByIdAsync(Guid.Parse("00000000-0000-0000-0000-000000000001"));
        Assert.Equal(5, updated!.Version);
    }

    // ==================== TrashRepository.InsertAsync ====================

    [Fact]
    public async Task TrashRepository_InsertAsync_InsertsRecord()
    {
        InsertTestItem("00000000-0000-0000-0000-000000000099");
        var repo = new TrashRepository(_connection);

        var record = new TrashRecord
        {
            Id = Guid.NewGuid(),
            ItemId = Guid.Parse("00000000-0000-0000-0000-000000000099"),
            DeletedAt = DateTimeOffset.FromUnixTimeSeconds(1700000000),
            AutoDeleteAt = DateTimeOffset.FromUnixTimeSeconds(1702592000),
            OriginalFolderId = null,
            OriginalArchiveStatus = ArchiveStatus.pending,
            MediaPaths = new List<string>()
        };

        await repo.InsertAsync(record);

        var records = await repo.GetAllAsync();
        Assert.Single(records);
        Assert.Equal("00000000-0000-0000-0000-000000000099", records[0].ItemId.ToString());
    }

    [Fact]
    public async Task TrashRepository_InsertAsync_SavesMediaPaths()
    {
        InsertTestItem("00000000-0000-0000-0000-000000000099");
        var repo = new TrashRepository(_connection);

        var record = new TrashRecord
        {
            Id = Guid.NewGuid(),
            ItemId = Guid.Parse("00000000-0000-0000-0000-000000000099"),
            DeletedAt = DateTimeOffset.FromUnixTimeSeconds(1700000000),
            AutoDeleteAt = DateTimeOffset.FromUnixTimeSeconds(1702592000),
            OriginalArchiveStatus = ArchiveStatus.pending,
            MediaPaths = new List<string> { "path/img1.jpg", "path/video1.mp4" }
        };

        await repo.InsertAsync(record);

        var records = await repo.GetAllAsync();
        Assert.Single(records);
        Assert.Equal(2, records[0].MediaPaths.Count);
        Assert.Equal("path/img1.jpg", records[0].MediaPaths[0]);
        Assert.Equal("path/video1.mp4", records[0].MediaPaths[1]);
    }

    [Fact]
    public async Task TrashRepository_InsertAsync_SavesOriginalFolderId()
    {
        InsertTestItem("00000000-0000-0000-0000-000000000099");
        var repo = new TrashRepository(_connection);
        var folderId = Guid.NewGuid();

        var record = new TrashRecord
        {
            Id = Guid.NewGuid(),
            ItemId = Guid.Parse("00000000-0000-0000-0000-000000000099"),
            DeletedAt = DateTimeOffset.FromUnixTimeSeconds(1700000000),
            AutoDeleteAt = DateTimeOffset.FromUnixTimeSeconds(1702592000),
            OriginalFolderId = folderId,
            OriginalArchiveStatus = ArchiveStatus.pending,
            MediaPaths = new List<string>()
        };

        await repo.InsertAsync(record);

        var records = await repo.GetAllAsync();
        Assert.Equal(folderId, records[0].OriginalFolderId);
    }

    [Fact]
    public async Task TrashRepository_GetByItemIdAsync_ReadsInsertedRecord()
    {
        InsertTestItem("00000000-0000-0000-0000-000000000099");
        var repo = new TrashRepository(_connection);

        var record = new TrashRecord
        {
            Id = Guid.NewGuid(),
            ItemId = Guid.Parse("00000000-0000-0000-0000-000000000099"),
            DeletedAt = DateTimeOffset.FromUnixTimeSeconds(1700000000),
            AutoDeleteAt = DateTimeOffset.FromUnixTimeSeconds(1702592000),
            OriginalArchiveStatus = ArchiveStatus.pending,
            MediaPaths = new List<string>()
        };

        await repo.InsertAsync(record);

        var found = await repo.GetByItemIdAsync(Guid.Parse("00000000-0000-0000-0000-000000000099"));
        Assert.NotNull(found);
        Assert.Equal(record.Id, found!.Id);
    }
}
