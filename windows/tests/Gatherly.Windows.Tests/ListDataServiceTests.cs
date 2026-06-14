using Gatherly.Windows.Database;
using Gatherly.Windows.Models;
using Gatherly.Windows.Models.Enums;
using Gatherly.Windows.Services;
using Gatherly.Windows.ViewModels;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Gatherly.Windows.Tests;

public class ListDataServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public ListDataServiceTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        MigrationRunner.RunAll(_connection);
        // custom_platforms 表需手动建
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

    private void InsertItem(string id, string platform = "bilibili", double importDate = 1700000000,
        string? customPlatformId = null, string? folderId = null, string? deletedAt = null)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO items (id, original_url, platform, normalized_url,
                import_date, modify_date, content_status, archive_status, media_status,
                custom_platform_id, folder_id, deleted_at)
            VALUES ($id, 'https://example.com', $platform, 'https://example.com',
                $importDate, $importDate, 'normal', 'pending', 'textOnly',
                $cpId, $folderId, $deletedAt)";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.Parameters.AddWithValue("$platform", platform);
        cmd.Parameters.AddWithValue("$importDate", importDate);
        cmd.Parameters.AddWithValue("$cpId", (object?)customPlatformId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$folderId", (object?)folderId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$deletedAt", (object?)deletedAt ?? DBNull.Value);
        cmd.ExecuteNonQuery();
    }

    private void InsertFolder(string id, string name = "Test Folder", string platform = "bilibili",
        string? customPlatformId = null, string? parentId = null)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO folders (id, name, platform, created_at, sort_order, custom_platform_id, parent_id)
            VALUES ($id, $name, $platform, 1700000000, 0, $cpId, $parentId)";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.Parameters.AddWithValue("$name", name);
        cmd.Parameters.AddWithValue("$platform", platform);
        cmd.Parameters.AddWithValue("$cpId", (object?)customPlatformId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$parentId", (object?)parentId ?? DBNull.Value);
        cmd.ExecuteNonQuery();
    }

    private void InsertTrashRecord(string id, string itemId)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO trash_records (id, item_id, deleted_at, auto_delete_at, original_archive_status)
            VALUES ($id, $itemId, 1700000000, 1702592000, 'pending')";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.Parameters.AddWithValue("$itemId", itemId);
        cmd.ExecuteNonQuery();
    }

    // ==================== HomeDataService ====================

    [Fact]
    public async Task HomeDataService_GetRecentItemsAsync_ReturnsOrdered()
    {
        InsertItem("00000000-0000-0000-0000-000000000001", importDate: 1700000000);
        InsertItem("00000000-0000-0000-0000-000000000002", importDate: 1700001000);
        InsertItem("00000000-0000-0000-0000-000000000003", importDate: 1700002000);

        var service = new HomeDataService(new ItemRepository(_connection), new MediaRepository(_connection), new CustomPlatformRepository(_connection), _connection);
        var items = await service.GetRecentItemsAsync();

        Assert.Equal(3, items.Count);
        Assert.Equal("00000000-0000-0000-0000-000000000003", items[0].Id.ToString());
        Assert.Equal("00000000-0000-0000-0000-000000000002", items[1].Id.ToString());
        Assert.Equal("00000000-0000-0000-0000-000000000001", items[2].Id.ToString());
    }

    [Fact]
    public async Task HomeDataService_GetRecentItemsAsync_RespectsLimit()
    {
        for (int i = 0; i < 10; i++)
            InsertItem($"00000000-0000-0000-0000-{i:D12}", importDate: 1700000000 + i);

        var service = new HomeDataService(new ItemRepository(_connection), new MediaRepository(_connection), new CustomPlatformRepository(_connection), _connection);
        var items = await service.GetRecentItemsAsync(limit: 3);

        Assert.Equal(3, items.Count);
    }

    [Fact]
    public async Task HomeDataService_GetRecentItemsAsync_ExcludesDeleted()
    {
        InsertItem("00000000-0000-0000-0000-000000000001", importDate: 1700000000);
        InsertItem("00000000-0000-0000-0000-000000000002", importDate: 1700001000, deletedAt: "1700001000");

        var service = new HomeDataService(new ItemRepository(_connection), new MediaRepository(_connection), new CustomPlatformRepository(_connection), _connection);
        var items = await service.GetRecentItemsAsync();

        Assert.Single(items);
    }

    // ==================== ContentListService ====================

    [Fact]
    public async Task ContentListService_GetPlatformItemsAsync()
    {
        InsertItem("00000000-0000-0000-0000-000000000001", platform: "bilibili");
        InsertItem("00000000-0000-0000-0000-000000000002", platform: "youtube");

        var service = new ContentListService(
            new ItemRepository(_connection),
            new FolderRepository(_connection));

        var bilibiliItems = await service.GetPlatformItemsAsync(Platform.bilibili);
        var youtubeItems = await service.GetPlatformItemsAsync(Platform.youtube);

        Assert.Single(bilibiliItems);
        Assert.Single(youtubeItems);
    }

    [Fact]
    public async Task ContentListService_GetPlatformFoldersAsync()
    {
        InsertFolder("00000000-0000-0000-0000-000000000010", "Folder A", platform: "bilibili");
        InsertFolder("00000000-0000-0000-0000-000000000011", "Folder B", platform: "youtube");

        var service = new ContentListService(
            new ItemRepository(_connection),
            new FolderRepository(_connection));

        var bilibiliFolders = await service.GetPlatformFoldersAsync(Platform.bilibili);

        Assert.Single(bilibiliFolders);
        Assert.Equal("Folder A", bilibiliFolders[0].Name);
    }

    [Fact]
    public async Task ContentListService_GetFolderItemsAsync()
    {
        var folderId = Guid.Parse("00000000-0000-0000-0000-000000000010");
        InsertFolder("00000000-0000-0000-0000-000000000010");
        InsertItem("00000000-0000-0000-0000-000000000001", folderId: folderId.ToString());
        InsertItem("00000000-0000-0000-0000-000000000002", folderId: null);

        var service = new ContentListService(
            new ItemRepository(_connection),
            new FolderRepository(_connection));

        var items = await service.GetFolderItemsAsync(folderId);

        Assert.Single(items);
        Assert.Equal("00000000-0000-0000-0000-000000000001", items[0].Id.ToString());
    }

    [Fact]
    public async Task ContentListService_GetChildFoldersAsync()
    {
        var parentId = Guid.Parse("00000000-0000-0000-0000-000000000010");
        InsertFolder("00000000-0000-0000-0000-000000000010");
        InsertFolder("00000000-0000-0000-0000-000000000011", parentId: parentId.ToString());
        InsertFolder("00000000-0000-0000-0000-000000000012"); // no parent

        var service = new ContentListService(
            new ItemRepository(_connection),
            new FolderRepository(_connection));

        var subfolders = await service.GetChildFoldersAsync(parentId);

        Assert.Single(subfolders);
        Assert.Equal("00000000-0000-0000-0000-000000000011", subfolders[0].Id.ToString());
    }

    [Fact]
    public async Task ContentListService_GetCustomPlatformItemsAsync()
    {
        var cpId = Guid.Parse("00000000-0000-0000-0000-000000000030");
        InsertItem("00000000-0000-0000-0000-000000000001",
            platform: "custom", customPlatformId: cpId.ToString());
        InsertItem("00000000-0000-0000-0000-000000000002",
            platform: "custom", customPlatformId: null);

        var service = new ContentListService(
            new ItemRepository(_connection),
            new FolderRepository(_connection));

        var items = await service.GetCustomPlatformItemsAsync(cpId);

        Assert.Single(items);
    }

    [Fact]
    public async Task ContentListService_GetCustomPlatformFoldersAsync()
    {
        var cpId = Guid.Parse("00000000-0000-0000-0000-000000000030");
        InsertFolder("00000000-0000-0000-0000-000000000010",
            platform: "custom", customPlatformId: cpId.ToString());
        InsertFolder("00000000-0000-0000-0000-000000000011",
            platform: "custom", customPlatformId: null);

        var service = new ContentListService(
            new ItemRepository(_connection),
            new FolderRepository(_connection));

        var folders = await service.GetCustomPlatformFoldersAsync(cpId);

        Assert.Single(folders);
    }

    [Fact]
    public async Task ContentListService_GetUncategorizedItemsAsync()
    {
        InsertItem("00000000-0000-0000-0000-000000000001",
            platform: "custom", customPlatformId: null);
        InsertItem("00000000-0000-0000-0000-000000000002",
            platform: "custom", customPlatformId: "00000000-0000-0000-0000-000000000030");
        InsertItem("00000000-0000-0000-0000-000000000003",
            platform: "bilibili");

        var service = new ContentListService(
            new ItemRepository(_connection),
            new FolderRepository(_connection));

        var items = await service.GetUncategorizedItemsAsync();

        // 未分类 = custom_platform_id IS NULL 且 platform 不是可见平台（youtube/bilibili）
        // 只有 platform=custom 且 custom_platform_id=NULL 的 item 在未分类
        // platform=bilibili 的 item 被 B站 平台认领，不在未分类
        Assert.Single(items);
        Assert.Equal("00000000-0000-0000-0000-000000000001", items[0].Id.ToString());
    }

    [Fact]
    public async Task ContentListService_GetUncategorizedFoldersAsync()
    {
        InsertFolder("00000000-0000-0000-0000-000000000010",
            platform: "custom", customPlatformId: null);
        InsertFolder("00000000-0000-0000-0000-000000000011",
            platform: "custom", customPlatformId: "00000000-0000-0000-0000-000000000030");

        var service = new ContentListService(
            new ItemRepository(_connection),
            new FolderRepository(_connection));

        var folders = await service.GetUncategorizedFoldersAsync();

        Assert.Single(folders);
    }

    // ==================== TrashDataService ====================

    [Fact]
    public async Task TrashDataService_GetTrashedItemsAsync()
    {
        InsertItem("00000000-0000-0000-0000-000000000001");
        InsertItem("00000000-0000-0000-0000-000000000002", deletedAt: "1700000000");

        var service = new TrashDataService(
            new ItemRepository(_connection),
            new TrashRepository(_connection));

        var trashed = await service.GetTrashedItemsAsync();

        Assert.Single(trashed);
        Assert.Equal("00000000-0000-0000-0000-000000000002", trashed[0].Id.ToString());
    }

    [Fact]
    public async Task TrashDataService_GetTrashRecordAsync()
    {
        InsertItem("00000000-0000-0000-0000-000000000001", deletedAt: "1700000000");
        InsertTrashRecord("00000000-0000-0000-0000-000000000040",
            "00000000-0000-0000-0000-000000000001");

        var service = new TrashDataService(
            new ItemRepository(_connection),
            new TrashRepository(_connection));

        var record = await service.GetTrashRecordAsync(
            Guid.Parse("00000000-0000-0000-0000-000000000001"));

        Assert.NotNull(record);
        Assert.Equal("00000000-0000-0000-0000-000000000040", record!.Id.ToString());
    }

    [Fact]
    public async Task TrashDataService_GetTrashRecordAsync_NotFound()
    {
        var service = new TrashDataService(
            new ItemRepository(_connection),
            new TrashRepository(_connection));

        var record = await service.GetTrashRecordAsync(Guid.NewGuid());

        Assert.Null(record);
    }

    // ==================== SearchService ====================

    [Fact]
    public async Task SearchService_SearchAsync_EmptyQuery()
    {
        var service = new SearchService(new SearchRepository(_connection));
        var results = await service.SearchAsync("");
        Assert.Empty(results);
    }

    [Fact]
    public async Task SearchService_SearchAsync_FindsMatch()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO items (id, original_url, platform, normalized_url, title, body,
                import_date, modify_date, content_status, archive_status, media_status)
            VALUES ('00000000-0000-0000-0000-000000000001', 'https://example.com', 'bilibili',
                'https://example.com', 'My Special Title', 'My Special Body',
                1700000000, 1700000000, 'normal', 'pending', 'textOnly')";
        cmd.ExecuteNonQuery();

        var service = new SearchService(new SearchRepository(_connection));
        var results = await service.SearchAsync("Special");

        Assert.NotEmpty(results);
    }

    // ==================== ContentListService Merged Platform ====================

    [Fact]
    public async Task ContentListService_GetMergedPlatformItemsAsync_CombinesStandardAndCustom()
    {
        var cpId = Guid.Parse("00000000-0000-0000-0000-000000000030");
        InsertItem("00000000-0000-0000-0000-000000000001", platform: "youtube");
        InsertItem("00000000-0000-0000-0000-000000000002", platform: "custom", customPlatformId: cpId.ToString());
        InsertItem("00000000-0000-0000-0000-000000000003", platform: "bilibili");

        var service = new ContentListService(new ItemRepository(_connection), new FolderRepository(_connection));
        var items = await service.GetMergedPlatformItemsAsync(Platform.youtube, new List<Guid> { cpId });

        Assert.Equal(2, items.Count);
        Assert.Contains(items, i => i.Id.ToString() == "00000000-0000-0000-0000-000000000001");
        Assert.Contains(items, i => i.Id.ToString() == "00000000-0000-0000-0000-000000000002");
    }

    [Fact]
    public async Task ContentListService_GetMergedPlatformItemsAsync_ExcludesDeleted()
    {
        var cpId = Guid.Parse("00000000-0000-0000-0000-000000000030");
        InsertItem("00000000-0000-0000-0000-000000000001", platform: "youtube");
        InsertItem("00000000-0000-0000-0000-000000000002", platform: "custom", customPlatformId: cpId.ToString(), deletedAt: "1700000000");

        var service = new ContentListService(new ItemRepository(_connection), new FolderRepository(_connection));
        var items = await service.GetMergedPlatformItemsAsync(Platform.youtube, new List<Guid> { cpId });

        Assert.Single(items);
        Assert.Equal("00000000-0000-0000-0000-000000000001", items[0].Id.ToString());
    }

    [Fact]
    public async Task ContentListService_GetMergedPlatformItemsAsync_EmptyCustomIds_FallsBackToStandard()
    {
        InsertItem("00000000-0000-0000-0000-000000000001", platform: "youtube");
        InsertItem("00000000-0000-0000-0000-000000000002", platform: "bilibili");

        var service = new ContentListService(new ItemRepository(_connection), new FolderRepository(_connection));
        var items = await service.GetMergedPlatformItemsAsync(Platform.youtube, new List<Guid>());

        Assert.Single(items);
        Assert.Equal("00000000-0000-0000-0000-000000000001", items[0].Id.ToString());
    }

    [Fact]
    public async Task ContentListService_GetMergedPlatformItemsAsync_MultipleCustomIds()
    {
        var cpId1 = Guid.Parse("00000000-0000-0000-0000-000000000030");
        var cpId2 = Guid.Parse("00000000-0000-0000-0000-000000000031");
        InsertItem("00000000-0000-0000-0000-000000000001", platform: "youtube");
        InsertItem("00000000-0000-0000-0000-000000000002", platform: "custom", customPlatformId: cpId1.ToString());
        InsertItem("00000000-0000-0000-0000-000000000003", platform: "custom", customPlatformId: cpId2.ToString());

        var service = new ContentListService(new ItemRepository(_connection), new FolderRepository(_connection));
        var items = await service.GetMergedPlatformItemsAsync(Platform.youtube, new List<Guid> { cpId1, cpId2 });

        Assert.Equal(3, items.Count);
    }

    [Fact]
    public async Task ContentListService_GetMergedPlatformItemsAsync_OnlyCustomNoStandard()
    {
        var cpId = Guid.Parse("00000000-0000-0000-0000-000000000030");
        InsertItem("00000000-0000-0000-0000-000000000001", platform: "custom", customPlatformId: cpId.ToString());

        var service = new ContentListService(new ItemRepository(_connection), new FolderRepository(_connection));
        var items = await service.GetMergedPlatformItemsAsync(Platform.youtube, new List<Guid> { cpId });

        Assert.Single(items);
    }

    [Fact]
    public async Task ContentListService_GetMergedPlatformItemsAsync_OnlyStandardNoCustom()
    {
        InsertItem("00000000-0000-0000-0000-000000000001", platform: "youtube");
        InsertItem("00000000-0000-0000-0000-000000000002", platform: "custom", customPlatformId: "00000000-0000-0000-0000-000000000030");

        var service = new ContentListService(new ItemRepository(_connection), new FolderRepository(_connection));
        var items = await service.GetMergedPlatformItemsAsync(Platform.youtube, new List<Guid>());

        Assert.Single(items);
        Assert.Equal("00000000-0000-0000-0000-000000000001", items[0].Id.ToString());
    }

    [Fact]
    public async Task ContentListService_GetMergedPlatformFoldersAsync_CombinesBothTypes()
    {
        var cpId = Guid.Parse("00000000-0000-0000-0000-000000000030");
        InsertFolder("00000000-0000-0000-0000-000000000010", "Standard Folder", platform: "youtube");
        InsertFolder("00000000-0000-0000-0000-000000000011", "Custom Folder", platform: "custom", customPlatformId: cpId.ToString());

        var service = new ContentListService(new ItemRepository(_connection), new FolderRepository(_connection));
        var folders = await service.GetMergedPlatformFoldersAsync(Platform.youtube, new List<Guid> { cpId });

        Assert.Equal(2, folders.Count);
    }

    [Fact]
    public async Task ContentListService_GetMergedPlatformFoldersAsync_EmptyCustomIds()
    {
        InsertFolder("00000000-0000-0000-0000-000000000010", "Standard Folder", platform: "youtube");
        InsertFolder("00000000-0000-0000-0000-000000000011", "Custom Folder", platform: "custom", customPlatformId: "00000000-0000-0000-0000-000000000030");

        var service = new ContentListService(new ItemRepository(_connection), new FolderRepository(_connection));
        var folders = await service.GetMergedPlatformFoldersAsync(Platform.youtube, new List<Guid>());

        Assert.Single(folders);
        Assert.Equal("Standard Folder", folders[0].Name);
    }

    // ==================== HomeDataService Platform Stats Merged ====================

    [Fact]
    public async Task HomeDataService_GetPlatformStatsAsync_MergedEntry_YouTubeWithCustom()
    {
        var cpId = Guid.Parse("00000000-0000-0000-0000-000000000030");
        InsertItem("00000000-0000-0000-0000-000000000001", platform: "youtube");
        InsertItem("00000000-0000-0000-0000-000000000002", platform: "custom", customPlatformId: cpId.ToString());

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"INSERT INTO custom_platforms (id, name, logo_path, created_at, sort_order)
            VALUES ($id, 'YouTube', null, 1700000000, 0)";
        cmd.Parameters.AddWithValue("$id", cpId.ToString("D"));
        cmd.ExecuteNonQuery();

        var service = new HomeDataService(new ItemRepository(_connection), new MediaRepository(_connection), new CustomPlatformRepository(_connection), _connection);
        var stats = await service.GetPlatformStatsAsync();

        var youtube = stats.FirstOrDefault(s => s.StandardPlatform == Platform.youtube);
        Assert.NotNull(youtube);
        Assert.Equal(2, youtube!.Count);
        Assert.True(youtube.IsStandardPlatform);
        Assert.Contains(cpId, youtube.CustomPlatformIds);
    }

    [Fact]
    public async Task HomeDataService_GetPlatformStatsAsync_StandardOnly_NoCustomIds()
    {
        InsertItem("00000000-0000-0000-0000-000000000001", platform: "github");

        var service = new HomeDataService(new ItemRepository(_connection), new MediaRepository(_connection), new CustomPlatformRepository(_connection), _connection);
        var stats = await service.GetPlatformStatsAsync();

        // 没有用户创建的 GitHub 平台时，GitHub 不显示在 sidebar
        // GitHub item 应显示在未分类
        var github = stats.FirstOrDefault(s => s.StandardPlatform == Platform.github);
        Assert.Null(github);
    }

    [Fact]
    public async Task HomeDataService_GetPlatformStatsAsync_CustomOnlyEntry()
    {
        var cpId = Guid.Parse("00000000-0000-0000-0000-000000000030");
        InsertItem("00000000-0000-0000-0000-000000000001", platform: "custom", customPlatformId: cpId.ToString());

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"INSERT INTO custom_platforms (id, name, logo_path, created_at, sort_order)
            VALUES ($id, 'My Custom', null, 1700000000, 0)";
        cmd.Parameters.AddWithValue("$id", cpId.ToString("D"));
        cmd.ExecuteNonQuery();

        var service = new HomeDataService(new ItemRepository(_connection), new MediaRepository(_connection), new CustomPlatformRepository(_connection), _connection);
        var stats = await service.GetPlatformStatsAsync();

        var custom = stats.FirstOrDefault(s => s.Name == "My Custom");
        Assert.NotNull(custom);
        Assert.Equal(1, custom!.Count);
        Assert.False(custom.IsStandardPlatform);
    }

    [Fact]
    public async Task HomeDataService_GetPlatformStatsAsync_UncategorizedAlwaysShown()
    {
        var service = new HomeDataService(new ItemRepository(_connection), new MediaRepository(_connection), new CustomPlatformRepository(_connection), _connection);
        var stats = await service.GetPlatformStatsAsync();

        var uncategorized = stats.FirstOrDefault(s => s.IsUncategorized);
        Assert.NotNull(uncategorized);
    }

    [Fact]
    public async Task HomeDataService_GetPlatformStatsAsync_MergedCount_MatchesPlatformAndCustomCombined()
    {
        var cpId = Guid.Parse("00000000-0000-0000-0000-000000000030");
        InsertItem("00000000-0000-0000-0000-000000000001", platform: "bilibili");
        InsertItem("00000000-0000-0000-0000-000000000002", platform: "bilibili");
        InsertItem("00000000-0000-0000-0000-000000000003", platform: "custom", customPlatformId: cpId.ToString());

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"INSERT INTO custom_platforms (id, name, logo_path, created_at, sort_order)
            VALUES ($id, 'B站', null, 1700000000, 0)";
        cmd.Parameters.AddWithValue("$id", cpId.ToString("D"));
        cmd.ExecuteNonQuery();

        var service = new HomeDataService(new ItemRepository(_connection), new MediaRepository(_connection), new CustomPlatformRepository(_connection), _connection);
        var stats = await service.GetPlatformStatsAsync();

        var bilibili = stats.FirstOrDefault(s => s.StandardPlatform == Platform.bilibili);
        Assert.NotNull(bilibili);
        Assert.Equal(3, bilibili!.Count);
    }

    // ==================== ItemRepository GetByPlatformWithCustomAsync ====================

    [Fact]
    public async Task ItemRepository_GetByPlatformWithCustomAsync_DeduplicatesById()
    {
        var cpId = Guid.Parse("00000000-0000-0000-0000-000000000030");
        var itemId = "00000000-0000-0000-0000-000000000001";
        InsertItem(itemId, platform: "youtube");

        var repo = new ItemRepository(_connection);
        var items = await repo.GetByPlatformWithCustomAsync(Platform.youtube, new List<Guid> { cpId });

        Assert.Single(items);
    }

    [Fact]
    public async Task ItemRepository_GetByPlatformWithCustomAsync_OrderByImportDateDesc()
    {
        var cpId = Guid.Parse("00000000-0000-0000-0000-000000000030");
        InsertItem("00000000-0000-0000-0000-000000000001", platform: "youtube", importDate: 1700000000);
        InsertItem("00000000-0000-0000-0000-000000000002", platform: "custom", customPlatformId: cpId.ToString(), importDate: 1700001000);

        var repo = new ItemRepository(_connection);
        var items = await repo.GetByPlatformWithCustomAsync(Platform.youtube, new List<Guid> { cpId });

        Assert.Equal(2, items.Count);
        Assert.Equal("00000000-0000-0000-0000-000000000002", items[0].Id.ToString());
        Assert.Equal("00000000-0000-0000-0000-000000000001", items[1].Id.ToString());
    }

    [Fact]
    public async Task ItemRepository_GetByPlatformWithCustomAsync_RespectsLimit()
    {
        var cpId = Guid.Parse("00000000-0000-0000-0000-000000000030");
        for (int i = 0; i < 5; i++)
            InsertItem($"00000000-0000-0000-0000-{i:D12}", platform: "youtube", importDate: 1700000000 + i);
        InsertItem("00000000-0000-0000-0000-000000000009", platform: "custom", customPlatformId: cpId.ToString(), importDate: 1700005000);

        var repo = new ItemRepository(_connection);
        var items = await repo.GetByPlatformWithCustomAsync(Platform.youtube, new List<Guid> { cpId }, limit: 3);

        Assert.Equal(3, items.Count);
    }

    // ==================== Routing Logic: YouTube vs Other Platforms ====================

    [Fact]
    public async Task YouTube_WithCustomIds_ShouldUseMergedPath()
    {
        var cpId = Guid.Parse("00000000-0000-0000-0000-000000000030");
        InsertItem("00000000-0000-0000-0000-000000000001", platform: "youtube");
        InsertItem("00000000-0000-0000-0000-000000000002", platform: "custom", customPlatformId: cpId.ToString());

        var entry = new PlatformEntryDisplay
        {
            Name = "YouTube",
            IsStandardPlatform = true,
            StandardPlatform = Platform.youtube,
            CustomPlatformIds = new List<Guid> { cpId }
        };

        Assert.Equal(Platform.youtube, entry.StandardPlatform);
        Assert.True(entry.CustomPlatformIds.Count > 0);

        var service = new ContentListService(new ItemRepository(_connection), new FolderRepository(_connection));
        var items = await service.GetMergedPlatformItemsAsync(entry.StandardPlatform!.Value, entry.CustomPlatformIds);
        Assert.Equal(2, items.Count);
    }

    [Fact]
    public async Task Xiaohongshu_WithCustomIds_ShouldUseCustomPath_NotMerged()
    {
        var cpId = Guid.Parse("00000000-0000-0000-0000-000000000030");
        InsertItem("00000000-0000-0000-0000-000000000001", platform: "custom", customPlatformId: cpId.ToString());
        InsertItem("00000000-0000-0000-0000-000000000002", platform: "custom", customPlatformId: cpId.ToString());

        var entry = new PlatformEntryDisplay
        {
            Name = "小红书",
            IsStandardPlatform = true,
            StandardPlatform = Platform.xiaohongshu,
            CustomPlatformIds = new List<Guid> { cpId }
        };

        Assert.Equal(Platform.xiaohongshu, entry.StandardPlatform);
        Assert.True(entry.CustomPlatformIds.Count > 0);

        var service = new ContentListService(new ItemRepository(_connection), new FolderRepository(_connection));
        var customItems = await service.GetCustomPlatformItemsAsync(cpId);
        Assert.Equal(2, customItems.Count);

        var standardItems = await service.GetPlatformItemsAsync(Platform.xiaohongshu);
        Assert.Empty(standardItems);
    }

    [Fact]
    public async Task Bilibili_WithCustomIds_ShouldNotAutoUseMerged()
    {
        var cpId = Guid.Parse("00000000-0000-0000-0000-000000000030");
        InsertItem("00000000-0000-0000-0000-000000000001", platform: "bilibili");
        InsertItem("00000000-0000-0000-0000-000000000002", platform: "custom", customPlatformId: cpId.ToString());

        var entry = new PlatformEntryDisplay
        {
            Name = "B站",
            IsStandardPlatform = true,
            StandardPlatform = Platform.bilibili,
            CustomPlatformIds = new List<Guid> { cpId }
        };

        Assert.Equal(Platform.bilibili, entry.StandardPlatform);
        Assert.True(entry.CustomPlatformIds.Count > 0);

        var service = new ContentListService(new ItemRepository(_connection), new FolderRepository(_connection));
        var standardItems = await service.GetPlatformItemsAsync(Platform.bilibili);
        Assert.Single(standardItems);

        var customItems = await service.GetCustomPlatformItemsAsync(cpId);
        Assert.Single(customItems);
    }

    [Fact]
    public async Task GitHub_WithCustomIds_ShouldUseStandardPath()
    {
        var cpId = Guid.Parse("00000000-0000-0000-0000-000000000030");
        InsertItem("00000000-0000-0000-0000-000000000001", platform: "github");

        var entry = new PlatformEntryDisplay
        {
            Name = "GitHub",
            IsStandardPlatform = true,
            StandardPlatform = Platform.github,
            CustomPlatformIds = new List<Guid>()
        };

        Assert.Equal(Platform.github, entry.StandardPlatform);
        Assert.Empty(entry.CustomPlatformIds);

        var service = new ContentListService(new ItemRepository(_connection), new FolderRepository(_connection));
        var items = await service.GetPlatformItemsAsync(Platform.github);
        Assert.Single(items);
    }

    [Fact]
    public async Task Weibo_WithCustomIds_ShouldUseCustomPath()
    {
        var cpId = Guid.Parse("00000000-0000-0000-0000-000000000030");
        InsertItem("00000000-0000-0000-0000-000000000001", platform: "custom", customPlatformId: cpId.ToString());

        var entry = new PlatformEntryDisplay
        {
            Name = "微博",
            IsStandardPlatform = true,
            StandardPlatform = Platform.weibo,
            CustomPlatformIds = new List<Guid> { cpId }
        };

        var service = new ContentListService(new ItemRepository(_connection), new FolderRepository(_connection));
        var customItems = await service.GetCustomPlatformItemsAsync(cpId);
        Assert.Single(customItems);

        var standardItems = await service.GetPlatformItemsAsync(Platform.weibo);
        Assert.Empty(standardItems);
    }

    [Fact]
    public async Task PureCustomPlatform_ShouldUseCustomPath()
    {
        var cpId = Guid.Parse("00000000-0000-0000-0000-000000000030");
        InsertItem("00000000-0000-0000-0000-000000000001", platform: "custom", customPlatformId: cpId.ToString());

        var entry = new PlatformEntryDisplay
        {
            Id = cpId,
            Name = "My Platform",
            IsStandardPlatform = false,
            CustomPlatformIds = new List<Guid> { cpId }
        };

        Assert.False(entry.IsStandardPlatform);

        var service = new ContentListService(new ItemRepository(_connection), new FolderRepository(_connection));
        var items = await service.GetCustomPlatformItemsAsync(cpId);
        Assert.Single(items);
    }

    [Fact]
    public async Task Uncategorized_ShouldUseUncategorizedPath()
    {
        InsertItem("00000000-0000-0000-0000-000000000001", platform: "custom", customPlatformId: null);

        var entry = new PlatformEntryDisplay
        {
            Name = "未分类内容",
            IsUncategorized = true
        };

        Assert.True(entry.IsUncategorized);

        var service = new ContentListService(new ItemRepository(_connection), new FolderRepository(_connection));
        var items = await service.GetUncategorizedItemsAsync();
        Assert.Single(items);
    }

    [Fact]
    public async Task Xiaohongshu_CustomCount_MatchesListCount()
    {
        var cpId = Guid.Parse("00000000-0000-0000-0000-000000000030");
        InsertItem("00000000-0000-0000-0000-000000000001", platform: "custom", customPlatformId: cpId.ToString());
        InsertItem("00000000-0000-0000-0000-000000000002", platform: "custom", customPlatformId: cpId.ToString());
        InsertItem("00000000-0000-0000-0000-000000000003", platform: "custom", customPlatformId: cpId.ToString());

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"INSERT INTO custom_platforms (id, name, logo_path, created_at, sort_order)
            VALUES ($id, '小红书', null, 1700000000, 0)";
        cmd.Parameters.AddWithValue("$id", cpId.ToString("D"));
        cmd.ExecuteNonQuery();

        var service = new HomeDataService(new ItemRepository(_connection), new MediaRepository(_connection), new CustomPlatformRepository(_connection), _connection);
        var stats = await service.GetPlatformStatsAsync();

        var custom = stats.FirstOrDefault(s => s.Name == "小红书");
        Assert.NotNull(custom);
        Assert.Equal(3, custom!.Count);

        var contentService = new ContentListService(new ItemRepository(_connection), new FolderRepository(_connection));
        var items = await contentService.GetCustomPlatformItemsAsync(cpId);
        Assert.Equal(3, items.Count);
    }

    [Fact]
    public async Task YouTube_MergedCount_MatchesMergedList()
    {
        var cpId = Guid.Parse("00000000-0000-0000-0000-000000000030");
        InsertItem("00000000-0000-0000-0000-000000000001", platform: "youtube");
        InsertItem("00000000-0000-0000-0000-000000000002", platform: "youtube");
        InsertItem("00000000-0000-0000-0000-000000000003", platform: "custom", customPlatformId: cpId.ToString());

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"INSERT INTO custom_platforms (id, name, logo_path, created_at, sort_order)
            VALUES ($id, 'YouTube', null, 1700000000, 0)";
        cmd.Parameters.AddWithValue("$id", cpId.ToString("D"));
        cmd.ExecuteNonQuery();

        var service = new HomeDataService(new ItemRepository(_connection), new MediaRepository(_connection), new CustomPlatformRepository(_connection), _connection);
        var stats = await service.GetPlatformStatsAsync();

        var youtube = stats.FirstOrDefault(s => s.StandardPlatform == Platform.youtube);
        Assert.NotNull(youtube);
        Assert.Equal(3, youtube!.Count);

        var contentService = new ContentListService(new ItemRepository(_connection), new FolderRepository(_connection));
        var items = await contentService.GetMergedPlatformItemsAsync(Platform.youtube, new List<Guid> { cpId });
        Assert.Equal(3, items.Count);
    }

    [Fact]
    public async Task SwitchYouTubeToXiaohongshu_NoStaleContent()
    {
        var cpIdXhs = Guid.Parse("00000000-0000-0000-0000-000000000030");
        var cpIdYt = Guid.Parse("00000000-0000-0000-0000-000000000031");
        InsertItem("00000000-0000-0000-0000-000000000001", platform: "youtube");
        InsertItem("00000000-0000-0000-0000-000000000002", platform: "custom", customPlatformId: cpIdYt.ToString());
        InsertItem("00000000-0000-0000-0000-000000000003", platform: "custom", customPlatformId: cpIdXhs.ToString());
        InsertItem("00000000-0000-0000-0000-000000000004", platform: "custom", customPlatformId: cpIdXhs.ToString());

        var contentService = new ContentListService(new ItemRepository(_connection), new FolderRepository(_connection));

        var ytItems = await contentService.GetMergedPlatformItemsAsync(Platform.youtube, new List<Guid> { cpIdYt });
        Assert.Equal(2, ytItems.Count);

        var xhsItems = await contentService.GetCustomPlatformItemsAsync(cpIdXhs);
        Assert.Equal(2, xhsItems.Count);

        Assert.DoesNotContain(ytItems, i => i.Id.ToString() == "00000000-0000-0000-0000-000000000003");
        Assert.DoesNotContain(ytItems, i => i.Id.ToString() == "00000000-0000-0000-0000-000000000004");
    }

    // ==================== Count/List Consistency Tests ====================

    [Fact]
    public async Task Xiaohongshu_Standard0_Custom15_EntryTypeIsCustom()
    {
        var cpId = Guid.Parse("00000000-0000-0000-0000-000000000030");
        for (int i = 0; i < 15; i++)
            InsertItem($"00000000-0000-0000-0000-{i:D12}", platform: "custom", customPlatformId: cpId.ToString());

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"INSERT INTO custom_platforms (id, name, logo_path, created_at, sort_order)
            VALUES ($id, '小红书', null, 1700000000, 0)";
        cmd.Parameters.AddWithValue("$id", cpId.ToString("D"));
        cmd.ExecuteNonQuery();

        var entry = new PlatformEntryDisplay
        {
            Name = "小红书",
            IsStandardPlatform = true,
            StandardPlatform = Platform.xiaohongshu,
            CustomPlatformIds = new List<Guid> { cpId }
        };

        var contentService = new ContentListService(new ItemRepository(_connection), new FolderRepository(_connection));
        var customItems = await contentService.GetCustomPlatformItemsAsync(cpId);
        Assert.Equal(15, customItems.Count);

        var standardItems = await contentService.GetPlatformItemsAsync(Platform.xiaohongshu);
        Assert.Empty(standardItems);
    }

    [Fact]
    public async Task Xiaohongshu_HasCustomIds_DoesNotUseMerged()
    {
        var cpId = Guid.Parse("00000000-0000-0000-0000-000000000030");
        InsertItem("00000000-0000-0000-0000-000000000001", platform: "custom", customPlatformId: cpId.ToString());

        var entry = new PlatformEntryDisplay
        {
            Name = "小红书",
            IsStandardPlatform = true,
            StandardPlatform = Platform.xiaohongshu,
            CustomPlatformIds = new List<Guid> { cpId }
        };

        Assert.Equal(Platform.xiaohongshu, entry.StandardPlatform);
        Assert.True(entry.CustomPlatformIds.Count > 0);
    }

    [Fact]
    public async Task Bilibili_Standard1_Custom1_EntryTypeIsMerged()
    {
        var cpId = Guid.Parse("00000000-0000-0000-0000-000000000030");
        InsertItem("00000000-0000-0000-0000-000000000001", platform: "bilibili");
        InsertItem("00000000-0000-0000-0000-000000000002", platform: "custom", customPlatformId: cpId.ToString());

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"INSERT INTO custom_platforms (id, name, logo_path, created_at, sort_order)
            VALUES ($id, 'B站', null, 1700000000, 0)";
        cmd.Parameters.AddWithValue("$id", cpId.ToString("D"));
        cmd.ExecuteNonQuery();

        var service = new HomeDataService(new ItemRepository(_connection), new MediaRepository(_connection), new CustomPlatformRepository(_connection), _connection);
        var stats = await service.GetPlatformStatsAsync();

        var bilibili = stats.FirstOrDefault(s => s.StandardPlatform == Platform.bilibili);
        Assert.NotNull(bilibili);
        Assert.Equal(2, bilibili!.Count);

        var contentService = new ContentListService(new ItemRepository(_connection), new FolderRepository(_connection));
        var items = await contentService.GetMergedPlatformItemsAsync(Platform.bilibili, new List<Guid> { cpId });
        Assert.Equal(2, items.Count);
    }

    [Fact]
    public async Task Bilibili_MergedCount_DeduplicatesById()
    {
        var cpId = Guid.Parse("00000000-0000-0000-0000-000000000030");
        InsertItem("00000000-0000-0000-0000-000000000001", platform: "bilibili");
        InsertItem("00000000-0000-0000-0000-000000000002", platform: "custom", customPlatformId: cpId.ToString());

        var contentService = new ContentListService(new ItemRepository(_connection), new FolderRepository(_connection));
        var items = await contentService.GetMergedPlatformItemsAsync(Platform.bilibili, new List<Guid> { cpId });

        var ids = items.Select(i => i.Id).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    [Fact]
    public async Task Bilibili_DeletedStandard_NotInCountOrList()
    {
        var cpId = Guid.Parse("00000000-0000-0000-0000-000000000030");
        InsertItem("00000000-0000-0000-0000-000000000001", platform: "bilibili", deletedAt: "1700000000");
        InsertItem("00000000-0000-0000-0000-000000000002", platform: "custom", customPlatformId: cpId.ToString());

        var contentService = new ContentListService(new ItemRepository(_connection), new FolderRepository(_connection));
        var items = await contentService.GetMergedPlatformItemsAsync(Platform.bilibili, new List<Guid> { cpId });

        Assert.Single(items);
        Assert.Equal("00000000-0000-0000-0000-000000000002", items[0].Id.ToString());
    }

    [Fact]
    public async Task Bilibili_TrashedCustom_NotInCountOrList()
    {
        var cpId = Guid.Parse("00000000-0000-0000-0000-000000000030");
        InsertItem("00000000-0000-0000-0000-000000000001", platform: "bilibili");
        InsertItem("00000000-0000-0000-0000-000000000002", platform: "custom", customPlatformId: cpId.ToString(), deletedAt: "1700000000");

        var contentService = new ContentListService(new ItemRepository(_connection), new FolderRepository(_connection));
        var items = await contentService.GetMergedPlatformItemsAsync(Platform.bilibili, new List<Guid> { cpId });

        Assert.Single(items);
        Assert.Equal("00000000-0000-0000-0000-000000000001", items[0].Id.ToString());
    }

    [Fact]
    public async Task YouTube_ContinuesToUseMerged()
    {
        var cpId = Guid.Parse("00000000-0000-0000-0000-000000000030");
        InsertItem("00000000-0000-0000-0000-000000000001", platform: "youtube");
        InsertItem("00000000-0000-0000-0000-000000000002", platform: "custom", customPlatformId: cpId.ToString());

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"INSERT INTO custom_platforms (id, name, logo_path, created_at, sort_order)
            VALUES ($id, 'YouTube', null, 1700000000, 0)";
        cmd.Parameters.AddWithValue("$id", cpId.ToString("D"));
        cmd.ExecuteNonQuery();

        var service = new HomeDataService(new ItemRepository(_connection), new MediaRepository(_connection), new CustomPlatformRepository(_connection), _connection);
        var stats = await service.GetPlatformStatsAsync();

        var youtube = stats.FirstOrDefault(s => s.StandardPlatform == Platform.youtube);
        Assert.NotNull(youtube);
        Assert.Equal(2, youtube!.Count);

        var contentService = new ContentListService(new ItemRepository(_connection), new FolderRepository(_connection));
        var items = await contentService.GetMergedPlatformItemsAsync(Platform.youtube, new List<Guid> { cpId });
        Assert.Equal(2, items.Count);
    }

    [Fact]
    public async Task Weibo_WithCustomIds_DoesNotAutoUseMerged()
    {
        var cpId = Guid.Parse("00000000-0000-0000-0000-000000000030");
        InsertItem("00000000-0000-0000-0000-000000000001", platform: "custom", customPlatformId: cpId.ToString());

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"INSERT INTO custom_platforms (id, name, logo_path, created_at, sort_order)
            VALUES ($id, '微博', null, 1700000000, 0)";
        cmd.Parameters.AddWithValue("$id", cpId.ToString("D"));
        cmd.ExecuteNonQuery();

        var service = new HomeDataService(new ItemRepository(_connection), new MediaRepository(_connection), new CustomPlatformRepository(_connection), _connection);
        var stats = await service.GetPlatformStatsAsync();

        var weibo = stats.FirstOrDefault(s => s.StandardPlatform == Platform.weibo);
        Assert.NotNull(weibo);
        Assert.Equal(1, weibo!.Count);

        var contentService = new ContentListService(new ItemRepository(_connection), new FolderRepository(_connection));
        var customItems = await contentService.GetCustomPlatformItemsAsync(cpId);
        Assert.Equal(1, customItems.Count);

        var standardItems = await contentService.GetPlatformItemsAsync(Platform.weibo);
        Assert.Empty(standardItems);
    }

    [Fact]
    public async Task Zhihu_WithCustomIds_DoesNotAutoUseMerged()
    {
        var cpId = Guid.Parse("00000000-0000-0000-0000-000000000030");
        InsertItem("00000000-0000-0000-0000-000000000001", platform: "custom", customPlatformId: cpId.ToString());

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"INSERT INTO custom_platforms (id, name, logo_path, created_at, sort_order)
            VALUES ($id, '知乎', null, 1700000000, 0)";
        cmd.Parameters.AddWithValue("$id", cpId.ToString("D"));
        cmd.ExecuteNonQuery();

        var service = new HomeDataService(new ItemRepository(_connection), new MediaRepository(_connection), new CustomPlatformRepository(_connection), _connection);
        var stats = await service.GetPlatformStatsAsync();

        var zhihu = stats.FirstOrDefault(s => s.StandardPlatform == Platform.zhihu);
        Assert.NotNull(zhihu);
        Assert.Equal(1, zhihu!.Count);

        var contentService = new ContentListService(new ItemRepository(_connection), new FolderRepository(_connection));
        var customItems = await contentService.GetCustomPlatformItemsAsync(cpId);
        Assert.Equal(1, customItems.Count);
    }

    [Fact]
    public async Task PureCustomPlatform_ContinuesToUseCustom()
    {
        var cpId = Guid.Parse("00000000-0000-0000-0000-000000000030");
        InsertItem("00000000-0000-0000-0000-000000000001", platform: "custom", customPlatformId: cpId.ToString());

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"INSERT INTO custom_platforms (id, name, logo_path, created_at, sort_order)
            VALUES ($id, 'My Platform', null, 1700000000, 0)";
        cmd.Parameters.AddWithValue("$id", cpId.ToString("D"));
        cmd.ExecuteNonQuery();

        var service = new HomeDataService(new ItemRepository(_connection), new MediaRepository(_connection), new CustomPlatformRepository(_connection), _connection);
        var stats = await service.GetPlatformStatsAsync();

        var custom = stats.FirstOrDefault(s => s.Name == "My Platform");
        Assert.NotNull(custom);
        Assert.Equal(1, custom!.Count);
        Assert.False(custom!.IsStandardPlatform);
    }

    [Fact]
    public async Task Uncategorized_ContinuesToUseUncategorizedPath()
    {
        InsertItem("00000000-0000-0000-0000-000000000001", platform: "custom", customPlatformId: null);

        var service = new HomeDataService(new ItemRepository(_connection), new MediaRepository(_connection), new CustomPlatformRepository(_connection), _connection);
        var stats = await service.GetPlatformStatsAsync();

        var uncategorized = stats.FirstOrDefault(s => s.IsUncategorized);
        Assert.NotNull(uncategorized);
        Assert.Equal(1, uncategorized!.Count);
    }

    [Fact]
    public async Task EntryType_CountAndListUseSameParameters()
    {
        var cpId = Guid.Parse("00000000-0000-0000-0000-000000000030");
        InsertItem("00000000-0000-0000-0000-000000000001", platform: "custom", customPlatformId: cpId.ToString());
        InsertItem("00000000-0000-0000-0000-000000000002", platform: "custom", customPlatformId: cpId.ToString());

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"INSERT INTO custom_platforms (id, name, logo_path, created_at, sort_order)
            VALUES ($id, 'Test Custom', null, 1700000000, 0)";
        cmd.Parameters.AddWithValue("$id", cpId.ToString("D"));
        cmd.ExecuteNonQuery();

        var contentService = new ContentListService(new ItemRepository(_connection), new FolderRepository(_connection));
        var items = await contentService.GetCustomPlatformItemsAsync(cpId);
        Assert.Equal(2, items.Count);
    }

    [Fact]
    public async Task DeleteXiaohongshuItem_CustomCountAndListDecrease()
    {
        var cpId = Guid.Parse("00000000-0000-0000-0000-000000000030");
        InsertItem("00000000-0000-0000-0000-000000000001", platform: "custom", customPlatformId: cpId.ToString());
        InsertItem("00000000-0000-0000-0000-000000000002", platform: "custom", customPlatformId: cpId.ToString());

        var contentService = new ContentListService(new ItemRepository(_connection), new FolderRepository(_connection));
        var items = await contentService.GetCustomPlatformItemsAsync(cpId);
        Assert.Equal(2, items.Count);

        await new ItemRepository(_connection).DeleteAsync(Guid.Parse("00000000-0000-0000-0000-000000000001"));

        var itemsAfter = await contentService.GetCustomPlatformItemsAsync(cpId);
        Assert.Single(itemsAfter);
    }

    [Fact]
    public async Task DeleteBilibiliItem_MergedCountAndListDecrease()
    {
        var cpId = Guid.Parse("00000000-0000-0000-0000-000000000030");
        InsertItem("00000000-0000-0000-0000-000000000001", platform: "bilibili");
        InsertItem("00000000-0000-0000-0000-000000000002", platform: "custom", customPlatformId: cpId.ToString());

        var contentService = new ContentListService(new ItemRepository(_connection), new FolderRepository(_connection));
        var items = await contentService.GetMergedPlatformItemsAsync(Platform.bilibili, new List<Guid> { cpId });
        Assert.Equal(2, items.Count);

        await new ItemRepository(_connection).DeleteAsync(Guid.Parse("00000000-0000-0000-0000-000000000001"));

        var itemsAfter = await contentService.GetMergedPlatformItemsAsync(Platform.bilibili, new List<Guid> { cpId });
        Assert.Single(itemsAfter);
    }

    [Fact]
    public async Task RestoreItem_CountAndListRestore()
    {
        var cpId = Guid.Parse("00000000-0000-0000-0000-000000000030");
        InsertItem("00000000-0000-0000-0000-000000000001", platform: "custom", customPlatformId: cpId.ToString());
        InsertItem("00000000-0000-0000-0000-000000000002", platform: "custom", customPlatformId: cpId.ToString(), deletedAt: "1700000000");

        var contentService = new ContentListService(new ItemRepository(_connection), new FolderRepository(_connection));
        var items = await contentService.GetCustomPlatformItemsAsync(cpId);
        Assert.Single(items);
    }

    [Fact]
    public async Task SwitchYouTubeToXiaohongshu_VerifyNoOverlap()
    {
        var cpIdXhs = Guid.Parse("00000000-0000-0000-0000-000000000030");
        var cpIdYt = Guid.Parse("00000000-0000-0000-0000-000000000031");
        InsertItem("00000000-0000-0000-0000-000000000001", platform: "youtube");
        InsertItem("00000000-0000-0000-0000-000000000002", platform: "custom", customPlatformId: cpIdYt.ToString());
        InsertItem("00000000-0000-0000-0000-000000000003", platform: "custom", customPlatformId: cpIdXhs.ToString());
        InsertItem("00000000-0000-0000-0000-000000000004", platform: "custom", customPlatformId: cpIdXhs.ToString());

        var contentService = new ContentListService(new ItemRepository(_connection), new FolderRepository(_connection));

        var ytItems = await contentService.GetMergedPlatformItemsAsync(Platform.youtube, new List<Guid> { cpIdYt });
        Assert.Equal(2, ytItems.Count);

        var xhsItems = await contentService.GetCustomPlatformItemsAsync(cpIdXhs);
        Assert.Equal(2, xhsItems.Count);

        Assert.DoesNotContain(ytItems, i => i.Id.ToString() == "00000000-0000-0000-0000-000000000003");
        Assert.DoesNotContain(xhsItems, i => i.Id.ToString() == "00000000-0000-0000-0000-000000000001");
    }

    [Fact]
    public async Task SwitchXiaohongshuToBilibili_NoStaleContent()
    {
        var cpIdXhs = Guid.Parse("00000000-0000-0000-0000-000000000030");
        var cpIdBl = Guid.Parse("00000000-0000-0000-0000-000000000031");
        InsertItem("00000000-0000-0000-0000-000000000001", platform: "custom", customPlatformId: cpIdXhs.ToString());
        InsertItem("00000000-0000-0000-0000-000000000002", platform: "bilibili");
        InsertItem("00000000-0000-0000-0000-000000000003", platform: "custom", customPlatformId: cpIdBl.ToString());

        var contentService = new ContentListService(new ItemRepository(_connection), new FolderRepository(_connection));

        var xhsItems = await contentService.GetCustomPlatformItemsAsync(cpIdXhs);
        Assert.Single(xhsItems);

        var blItems = await contentService.GetMergedPlatformItemsAsync(Platform.bilibili, new List<Guid> { cpIdBl });
        Assert.Equal(2, blItems.Count);

        Assert.DoesNotContain(xhsItems, i => i.Id.ToString() == "00000000-0000-0000-0000-000000000002");
        Assert.DoesNotContain(blItems, i => i.Id.ToString() == "00000000-0000-0000-0000-000000000001");
    }

    // ==================== GUID Case Sensitivity Tests ====================

    [Fact]
    public async Task Bilibili_MergedQuery_UppercaseGuidInDb_LowercaseParam_MatchesWithCollateNocase()
    {
        var cpId = Guid.Parse("00000000-0000-0000-0000-000000000030");
        InsertItem("00000000-0000-0000-0000-000000000001", platform: "bilibili");
        InsertItem("00000000-0000-0000-0000-000000000002", platform: "custom", customPlatformId: cpId.ToString());

        var contentService = new ContentListService(new ItemRepository(_connection), new FolderRepository(_connection));
        var items = await contentService.GetMergedPlatformItemsAsync(Platform.bilibili, new List<Guid> { cpId });

        Assert.Equal(2, items.Count);
    }

    [Fact]
    public async Task Bilibili_MergedQuery_MixedCaseGuids_AllMatch()
    {
        var cpId1 = Guid.Parse("00000000-0000-0000-0000-000000000030");
        var cpId2 = Guid.Parse("00000000-0000-0000-0000-000000000031");
        InsertItem("00000000-0000-0000-0000-000000000001", platform: "bilibili");
        InsertItem("00000000-0000-0000-0000-000000000002", platform: "custom", customPlatformId: cpId1.ToString());
        InsertItem("00000000-0000-0000-0000-000000000003", platform: "custom", customPlatformId: cpId2.ToString());

        var contentService = new ContentListService(new ItemRepository(_connection), new FolderRepository(_connection));
        var items = await contentService.GetMergedPlatformItemsAsync(Platform.bilibili, new List<Guid> { cpId1, cpId2 });

        Assert.Equal(3, items.Count);
    }

    [Fact]
    public async Task Bilibili_MultipleCustomPlatformIds_AllReturn()
    {
        var cpId1 = Guid.Parse("00000000-0000-0000-0000-000000000030");
        var cpId2 = Guid.Parse("00000000-0000-0000-0000-000000000031");
        var cpId3 = Guid.Parse("00000000-0000-0000-0000-000000000032");
        InsertItem("00000000-0000-0000-0000-000000000001", platform: "custom", customPlatformId: cpId1.ToString());
        InsertItem("00000000-0000-0000-0000-000000000002", platform: "custom", customPlatformId: cpId2.ToString());
        InsertItem("00000000-0000-0000-0000-000000000003", platform: "custom", customPlatformId: cpId3.ToString());

        var contentService = new ContentListService(new ItemRepository(_connection), new FolderRepository(_connection));
        var items = await contentService.GetMergedPlatformItemsAsync(Platform.bilibili, new List<Guid> { cpId1, cpId2, cpId3 });

        Assert.Equal(3, items.Count);
    }

    [Fact]
    public async Task Bilibili_CustomPlatformIds_PreservedThroughEntryToService()
    {
        var cpId = Guid.Parse("00000000-0000-0000-0000-000000000030");
        InsertItem("00000000-0000-0000-0000-000000000001", platform: "bilibili");
        InsertItem("00000000-0000-0000-0000-000000000002", platform: "custom", customPlatformId: cpId.ToString());

        var entry = new PlatformEntryDisplay
        {
            Name = "B站",
            IsStandardPlatform = true,
            StandardPlatform = Platform.bilibili,
            CustomPlatformIds = new List<Guid> { cpId }
        };

        Assert.Single(entry.CustomPlatformIds);
        Assert.Equal(cpId, entry.CustomPlatformIds[0]);

        var contentService = new ContentListService(new ItemRepository(_connection), new FolderRepository(_connection));
        var items = await contentService.GetMergedPlatformItemsAsync(entry.StandardPlatform!.Value, entry.CustomPlatformIds);

        Assert.Equal(2, items.Count);
    }

    [Fact]
    public async Task Bilibili_MergedCount_MatchesMergedList()
    {
        var cpId = Guid.Parse("00000000-0000-0000-0000-000000000030");
        InsertItem("00000000-0000-0000-0000-000000000001", platform: "bilibili");
        InsertItem("00000000-0000-0000-0000-000000000002", platform: "custom", customPlatformId: cpId.ToString());

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"INSERT INTO custom_platforms (id, name, logo_path, created_at, sort_order)
            VALUES ($id, 'B站', null, 1700000000, 0)";
        cmd.Parameters.AddWithValue("$id", cpId.ToString("D"));
        cmd.ExecuteNonQuery();

        var service = new HomeDataService(new ItemRepository(_connection), new MediaRepository(_connection), new CustomPlatformRepository(_connection), _connection);
        var stats = await service.GetPlatformStatsAsync();

        var bilibili = stats.FirstOrDefault(s => s.StandardPlatform == Platform.bilibili);
        Assert.NotNull(bilibili);
        Assert.Equal(2, bilibili!.Count);

        var contentService = new ContentListService(new ItemRepository(_connection), new FolderRepository(_connection));
        var items = await contentService.GetMergedPlatformItemsAsync(Platform.bilibili, new List<Guid> { cpId });
        Assert.Equal(2, items.Count);
    }

    [Fact]
    public async Task Bilibili_Alias_BStation_Identified()
    {
        var cpId = Guid.Parse("00000000-0000-0000-0000-000000000030");
        InsertItem("00000000-0000-0000-0000-000000000001", platform: "custom", customPlatformId: cpId.ToString());

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"INSERT INTO custom_platforms (id, name, logo_path, created_at, sort_order)
            VALUES ($id, 'B站', null, 1700000000, 0)";
        cmd.Parameters.AddWithValue("$id", cpId.ToString("D"));
        cmd.ExecuteNonQuery();

        var service = new HomeDataService(new ItemRepository(_connection), new MediaRepository(_connection), new CustomPlatformRepository(_connection), _connection);
        var stats = await service.GetPlatformStatsAsync();

        var bilibili = stats.FirstOrDefault(s => s.StandardPlatform == Platform.bilibili);
        Assert.NotNull(bilibili);
        Assert.Contains(cpId, bilibili!.CustomPlatformIds);
    }

    [Fact]
    public async Task Bilibili_Alias_Bilibili_Identified()
    {
        var cpId = Guid.Parse("00000000-0000-0000-0000-000000000030");
        InsertItem("00000000-0000-0000-0000-000000000001", platform: "custom", customPlatformId: cpId.ToString());

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"INSERT INTO custom_platforms (id, name, logo_path, created_at, sort_order)
            VALUES ($id, 'Bilibili', null, 1700000000, 0)";
        cmd.Parameters.AddWithValue("$id", cpId.ToString("D"));
        cmd.ExecuteNonQuery();

        var service = new HomeDataService(new ItemRepository(_connection), new MediaRepository(_connection), new CustomPlatformRepository(_connection), _connection);
        var stats = await service.GetPlatformStatsAsync();

        var bilibili = stats.FirstOrDefault(s => s.StandardPlatform == Platform.bilibili);
        Assert.NotNull(bilibili);
        Assert.Contains(cpId, bilibili!.CustomPlatformIds);
    }

    [Fact]
    public async Task Bilibili_Alias_BiLiBiLi_Identified()
    {
        var cpId = Guid.Parse("00000000-0000-0000-0000-000000000030");
        InsertItem("00000000-0000-0000-0000-000000000001", platform: "custom", customPlatformId: cpId.ToString());

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"INSERT INTO custom_platforms (id, name, logo_path, created_at, sort_order)
            VALUES ($id, '哔哩哔哩', null, 1700000000, 0)";
        cmd.Parameters.AddWithValue("$id", cpId.ToString("D"));
        cmd.ExecuteNonQuery();

        var service = new HomeDataService(new ItemRepository(_connection), new MediaRepository(_connection), new CustomPlatformRepository(_connection), _connection);
        var stats = await service.GetPlatformStatsAsync();

        var bilibili = stats.FirstOrDefault(s => s.StandardPlatform == Platform.bilibili);
        Assert.NotNull(bilibili);
        Assert.Contains(cpId, bilibili!.CustomPlatformIds);
    }

    [Fact]
    public async Task UnrelatedCustomPlatform_NotIncludedInBilibiliIds()
    {
        var cpIdBili = Guid.Parse("00000000-0000-0000-0000-000000000030");
        var cpIdOther = Guid.Parse("00000000-0000-0000-0000-000000000031");
        InsertItem("00000000-0000-0000-0000-000000000001", platform: "custom", customPlatformId: cpIdBili.ToString());
        InsertItem("00000000-0000-0000-0000-000000000002", platform: "custom", customPlatformId: cpIdOther.ToString());

        using (var cmd = _connection.CreateCommand())
        {
            cmd.CommandText = @"INSERT INTO custom_platforms (id, name, logo_path, created_at, sort_order)
                VALUES ($id, 'B站', null, 1700000000, 0)";
            cmd.Parameters.AddWithValue("$id", cpIdBili.ToString("D"));
            cmd.ExecuteNonQuery();
        }
        using (var cmd = _connection.CreateCommand())
        {
            cmd.CommandText = @"INSERT INTO custom_platforms (id, name, logo_path, created_at, sort_order)
                VALUES ($id, 'Other Platform', null, 1700000001, 0)";
            cmd.Parameters.AddWithValue("$id", cpIdOther.ToString("D"));
            cmd.ExecuteNonQuery();
        }

        var service = new HomeDataService(new ItemRepository(_connection), new MediaRepository(_connection), new CustomPlatformRepository(_connection), _connection);
        var stats = await service.GetPlatformStatsAsync();

        var bilibili = stats.FirstOrDefault(s => s.StandardPlatform == Platform.bilibili);
        Assert.NotNull(bilibili);
        Assert.Contains(cpIdBili, bilibili!.CustomPlatformIds);
        Assert.DoesNotContain(cpIdOther, bilibili!.CustomPlatformIds);
    }
}
