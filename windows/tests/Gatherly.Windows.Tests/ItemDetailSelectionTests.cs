using Gatherly.Windows.Database;
using Gatherly.Windows.Models;
using Gatherly.Windows.Models.Enums;
using Gatherly.Windows.ViewModels;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Gatherly.Windows.Tests;

public class ItemDetailSelectionTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public ItemDetailSelectionTests()
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

    private MainWindowViewModel CreateViewModel()
    {
        return new MainWindowViewModel(_connection);
    }

    [Fact]
    public void HasSelectedItem_FalseByDefault()
    {
        var vm = CreateViewModel();
        Assert.False(vm.HasSelectedItem);
        Assert.Null(vm.SelectedItem);
    }

    [Fact]
    public void HasSelectedItem_TrueAfterSelection()
    {
        var vm = CreateViewModel();
        var item = new Item
        {
            Id = Guid.NewGuid(),
            Title = "Test Title",
            Author = "Test Author",
            Platform = Platform.bilibili,
            OriginalUrl = "https://example.com",
            NormalizedUrl = "https://example.com",
            ImportDate = DateTimeOffset.FromUnixTimeSeconds(1700000000),
            ModifyDate = DateTimeOffset.FromUnixTimeSeconds(1700000000),
            ContentStatus = ContentStatus.normal,
            ArchiveStatus = ArchiveStatus.pending,
            MediaStatus = MediaStatus.textOnly
        };

        vm.SelectedItem = item;

        Assert.True(vm.HasSelectedItem);
        Assert.Same(item, vm.SelectedItem);
    }

    [Fact]
    public void DisplayTitle_FallbackForEmpty()
    {
        var vm = CreateViewModel();
        Assert.Equal("未命名内容", vm.DisplayTitle);

        vm.SelectedItem = new Item
        {
            Id = Guid.NewGuid(),
            Title = null,
            OriginalUrl = "https://example.com",
            NormalizedUrl = "https://example.com",
            Platform = Platform.bilibili,
            ImportDate = DateTimeOffset.MinValue,
            ModifyDate = DateTimeOffset.MinValue,
            ContentStatus = ContentStatus.normal,
            ArchiveStatus = ArchiveStatus.pending,
            MediaStatus = MediaStatus.textOnly
        };

        Assert.Equal("未命名内容", vm.DisplayTitle);
    }

    [Fact]
    public void DisplayTitle_ShowsTitle()
    {
        var vm = CreateViewModel();
        vm.SelectedItem = new Item
        {
            Id = Guid.NewGuid(),
            Title = "My Content",
            OriginalUrl = "https://example.com",
            NormalizedUrl = "https://example.com",
            Platform = Platform.youtube,
            ImportDate = DateTimeOffset.MinValue,
            ModifyDate = DateTimeOffset.MinValue,
            ContentStatus = ContentStatus.normal,
            ArchiveStatus = ArchiveStatus.pending,
            MediaStatus = MediaStatus.textOnly
        };

        Assert.Equal("My Content", vm.DisplayTitle);
    }

    [Fact]
    public void DisplayAuthor_FallbackForEmpty()
    {
        var vm = CreateViewModel();
        Assert.Equal("未知作者", vm.DisplayAuthor);

        vm.SelectedItem = new Item
        {
            Id = Guid.NewGuid(),
            Author = null,
            OriginalUrl = "https://example.com",
            NormalizedUrl = "https://example.com",
            Platform = Platform.bilibili,
            ImportDate = DateTimeOffset.MinValue,
            ModifyDate = DateTimeOffset.MinValue,
            ContentStatus = ContentStatus.normal,
            ArchiveStatus = ArchiveStatus.pending,
            MediaStatus = MediaStatus.textOnly
        };

        Assert.Equal("未知作者", vm.DisplayAuthor);
    }

    [Fact]
    public void DisplayAuthor_ShowsAuthor()
    {
        var vm = CreateViewModel();
        vm.SelectedItem = new Item
        {
            Id = Guid.NewGuid(),
            Author = "John Doe",
            OriginalUrl = "https://example.com",
            NormalizedUrl = "https://example.com",
            Platform = Platform.github,
            ImportDate = DateTimeOffset.MinValue,
            ModifyDate = DateTimeOffset.MinValue,
            ContentStatus = ContentStatus.normal,
            ArchiveStatus = ArchiveStatus.pending,
            MediaStatus = MediaStatus.textOnly
        };

        Assert.Equal("John Doe", vm.DisplayAuthor);
    }

    [Fact]
    public void DisplayPlatform_ShowsCorrectly()
    {
        var vm = CreateViewModel();
        vm.SelectedItem = new Item
        {
            Id = Guid.NewGuid(),
            OriginalUrl = "https://example.com",
            NormalizedUrl = "https://example.com",
            Platform = Platform.douyin,
            ImportDate = DateTimeOffset.MinValue,
            ModifyDate = DateTimeOffset.MinValue,
            ContentStatus = ContentStatus.normal,
            ArchiveStatus = ArchiveStatus.pending,
            MediaStatus = MediaStatus.textOnly
        };

        Assert.Equal("抖音", vm.DisplayPlatform);
    }

    [Fact]
    public void DisplayUrls_ShowsCorrectly()
    {
        var vm = CreateViewModel();
        vm.SelectedItem = new Item
        {
            Id = Guid.NewGuid(),
            OriginalUrl = "https://example.com/original",
            NormalizedUrl = "https://example.com/normalized",
            Platform = Platform.bilibili,
            ImportDate = DateTimeOffset.MinValue,
            ModifyDate = DateTimeOffset.MinValue,
            ContentStatus = ContentStatus.normal,
            ArchiveStatus = ArchiveStatus.pending,
            MediaStatus = MediaStatus.textOnly
        };

        Assert.Equal("https://example.com/original", vm.DisplayOriginalUrl);
        Assert.Equal("https://example.com/normalized", vm.DisplayNormalizedUrl);
    }

    [Fact]
    public void DisplayBody_ShowsContent()
    {
        var vm = CreateViewModel();
        vm.SelectedItem = new Item
        {
            Id = Guid.NewGuid(),
            Body = "This is the body text",
            OriginalUrl = "https://example.com",
            NormalizedUrl = "https://example.com",
            Platform = Platform.bilibili,
            ImportDate = DateTimeOffset.MinValue,
            ModifyDate = DateTimeOffset.MinValue,
            ContentStatus = ContentStatus.normal,
            ArchiveStatus = ArchiveStatus.pending,
            MediaStatus = MediaStatus.textOnly
        };

        Assert.Equal("This is the body text", vm.DisplayBody);
    }

    [Fact]
    public void DisplayBody_EmptyForNull()
    {
        var vm = CreateViewModel();
        Assert.Equal("", vm.DisplayBody);
    }

    [Fact]
    public void SelectionChange_PropertiesUpdate()
    {
        var vm = CreateViewModel();
        var propertyNames = new List<string>();

        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != null)
                propertyNames.Add(e.PropertyName);
        };

        var item = new Item
        {
            Id = Guid.NewGuid(),
            Title = "Updated",
            Author = "Author",
            OriginalUrl = "https://example.com",
            NormalizedUrl = "https://example.com",
            Platform = Platform.bilibili,
            ImportDate = DateTimeOffset.FromUnixTimeSeconds(1700000000),
            ModifyDate = DateTimeOffset.FromUnixTimeSeconds(1700000000),
            ContentStatus = ContentStatus.normal,
            ArchiveStatus = ArchiveStatus.pending,
            MediaStatus = MediaStatus.textOnly
        };

        vm.SelectedItem = item;

        Assert.Contains("SelectedItem", propertyNames);
        Assert.Contains("HasSelectedItem", propertyNames);
        Assert.Contains("DisplayTitle", propertyNames);
        Assert.Contains("DisplayAuthor", propertyNames);
        Assert.Contains("DisplayBody", propertyNames);
    }

    [Fact]
    public void SelectionClear_ResetsProperties()
    {
        var vm = CreateViewModel();
        vm.SelectedItem = new Item
        {
            Id = Guid.NewGuid(),
            Title = "Something",
            OriginalUrl = "https://example.com",
            NormalizedUrl = "https://example.com",
            Platform = Platform.bilibili,
            ImportDate = DateTimeOffset.MinValue,
            ModifyDate = DateTimeOffset.MinValue,
            ContentStatus = ContentStatus.normal,
            ArchiveStatus = ArchiveStatus.pending,
            MediaStatus = MediaStatus.textOnly
        };

        Assert.True(vm.HasSelectedItem);

        vm.SelectedItem = null;

        Assert.False(vm.HasSelectedItem);
        Assert.Equal("未命名内容", vm.DisplayTitle);
    }

    [Fact]
    public async Task SubViewModel_Selection_PropagatesToMain()
    {
        var vm = CreateViewModel();

        // Wait for home data to load
        await vm.Home.LoadCommand.ExecuteAsync(null);

        if (vm.Home.RecentItems.Count > 0)
        {
            vm.Home.SelectedItem = vm.Home.RecentItems[0];
            Assert.NotNull(vm.SelectedItem);
            Assert.Same(vm.Home.RecentItems[0], vm.SelectedItem);
        }
    }
}
