using Gatherly.Windows.Database;
using Gatherly.Windows.Models;
using Gatherly.Windows.Models.Enums;
using Gatherly.Windows.Services;
using Gatherly.Windows.ViewModels;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Gatherly.Windows.Tests.Services;

public class PlatformManagementViewModelTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly CustomPlatformService _platformService;
    private readonly ItemRepository _itemRepo;

    public PlatformManagementViewModelTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        MigrationRunner.RunAll(_connection);
        using (var cmd = _connection.CreateCommand())
        {
            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS custom_platforms (
                id TEXT PRIMARY KEY,
                name TEXT NOT NULL,
                logo_path TEXT,
                created_at REAL NOT NULL,
                sort_order INTEGER NOT NULL DEFAULT 0
            )";
            cmd.ExecuteNonQuery();
        }
        _platformService = new CustomPlatformService(new CustomPlatformRepository(_connection));
        _itemRepo = new ItemRepository(_connection);
    }

    public void Dispose()
    {
        _connection.Close();
        _connection.Dispose();
    }

    private PlatformManagementViewModel CreateVm() => new(_platformService, _itemRepo);

    [Fact]
    public async Task LoadAsync_ShowsAllCustomPlatforms()
    {
        await _platformService.CreatePlatformAsync("YouTube");
        await _platformService.CreatePlatformAsync("工作");
        var vm = CreateVm();
        await vm.LoadCommand.ExecuteAsync(null);
        Assert.Equal(2, vm.Entries.Count);
    }

    [Fact]
    public async Task LoadAsync_OnlyShowsCustomPlatforms()
    {
        var vm = CreateVm();
        await vm.LoadCommand.ExecuteAsync(null);
        foreach (var e in vm.Entries)
            Assert.Equal(PlatformManagementEntryKind.Custom, e.Kind);
    }

    [Fact]
    public async Task LoadAsync_EachCustomPlatformIdExactlyOnce()
    {
        var cp1 = await _platformService.CreatePlatformAsync("YouTube");
        var cp2 = await _platformService.CreatePlatformAsync("Bilibili");
        var vm = CreateVm();
        await vm.LoadCommand.ExecuteAsync(null);
        Assert.Single(vm.Entries, e => e.CustomPlatformId == cp1.Id);
        Assert.Single(vm.Entries, e => e.CustomPlatformId == cp2.Id);
    }

    [Fact]
    public async Task EditCustomPlatform_UpdatesName()
    {
        var cp = await _platformService.CreatePlatformAsync("工作");
        var vm = CreateVm();
        await vm.LoadCommand.ExecuteAsync(null);
        var entry = vm.Entries.First(e => e.CustomPlatformId == cp.Id);
        vm.BeginEditCommand.Execute(entry);
        vm.PlatformName = "我的工作";
        await vm.SaveCommand.ExecuteAsync(null);
        var updated = await _platformService.GetAllPlatformsAsync();
        Assert.Contains(updated, p => p.Name == "我的工作");
    }

    [Fact]
    public async Task EditCustomPlatform_IdUnchanged()
    {
        var cp = await _platformService.CreatePlatformAsync("工作");
        var vm = CreateVm();
        await vm.LoadCommand.ExecuteAsync(null);
        var entry = vm.Entries.First(e => e.CustomPlatformId == cp.Id);
        vm.BeginEditCommand.Execute(entry);
        vm.PlatformName = "我的工作";
        await vm.SaveCommand.ExecuteAsync(null);
        var updated = await _platformService.GetAllPlatformsAsync();
        Assert.Contains(updated, p => p.Id == cp.Id && p.Name == "我的工作");
    }

    [Fact]
    public async Task CustomPlatform_RenameMultipleTimes()
    {
        var cp = await _platformService.CreatePlatformAsync("工作");
        var vm = CreateVm();
        await vm.LoadCommand.ExecuteAsync(null);
        var entry = vm.Entries.First(e => e.CustomPlatformId == cp.Id);

        vm.BeginEditCommand.Execute(entry); vm.PlatformName = "我的工作";
        await vm.SaveCommand.ExecuteAsync(null);
        vm.BeginEditCommand.Execute(entry); vm.PlatformName = "项目资料";
        await vm.SaveCommand.ExecuteAsync(null);

        var updated = await _platformService.GetAllPlatformsAsync();
        Assert.Contains(updated, p => p.Id == cp.Id && p.Name == "项目资料");
    }

    [Fact]
    public async Task BeginCreate_CreatesCustomPlatform()
    {
        var vm = CreateVm();
        await vm.LoadCommand.ExecuteAsync(null);
        vm.BeginCreateCommand.Execute(null);
        vm.PlatformName = "新平台";
        await vm.SaveCommand.ExecuteAsync(null);
        var all = await _platformService.GetAllPlatformsAsync();
        Assert.Contains(all, p => p.Name == "新平台");
    }

    [Fact]
    public async Task CancelEdit_DoesNotSave()
    {
        var cp = await _platformService.CreatePlatformAsync("工作");
        var vm = CreateVm();
        await vm.LoadCommand.ExecuteAsync(null);
        var entry = vm.Entries.First(e => e.CustomPlatformId == cp.Id);
        vm.BeginEditCommand.Execute(entry);
        vm.PlatformName = "其他";
        vm.CancelEditCommand.Execute(null);
        var all = await _platformService.GetAllPlatformsAsync();
        Assert.Contains(all, p => p.Name == "工作");
    }

    [Fact]
    public async Task Delete_ZeroContentCustomPlatform_Succeeds()
    {
        var cp = await _platformService.CreatePlatformAsync("测试平台");
        var vm = CreateVm();
        await vm.LoadCommand.ExecuteAsync(null);
        var entry = vm.Entries.First(e => e.CustomPlatformId == cp.Id);
        vm.BeginDeleteCommand.Execute(entry);
        Assert.True(vm.IsConfirmingDelete);
        await vm.ConfirmDeleteCommand.ExecuteAsync(null);
        Assert.False(vm.IsConfirmingDelete);
        var remaining = await _platformService.GetAllPlatformsAsync();
        Assert.DoesNotContain(remaining, p => p.Id == cp.Id);
    }

    [Fact]
    public async Task RenameCustom_DoesNotAffectOtherPlatforms()
    {
        var cp = await _platformService.CreatePlatformAsync("YouTube");
        var cp2 = await _platformService.CreatePlatformAsync("工作");
        var vm = CreateVm();
        await vm.LoadCommand.ExecuteAsync(null);

        var entry = vm.Entries.First(e => e.CustomPlatformId == cp.Id);
        vm.BeginEditCommand.Execute(entry);
        vm.PlatformName = "我的影片";
        await vm.SaveCommand.ExecuteAsync(null);

        var allPlatforms = await _platformService.GetAllPlatformsAsync();
        Assert.Contains(allPlatforms, p => p.Name == "我的影片");
        Assert.Contains(allPlatforms, p => p.Name == "工作");
    }

    [Fact]
    public async Task CancelDelete_DoesNotDelete()
    {
        var cp = await _platformService.CreatePlatformAsync("测试平台");
        var vm = CreateVm();
        await vm.LoadCommand.ExecuteAsync(null);
        var entry = vm.Entries.First(e => e.CustomPlatformId == cp.Id);
        vm.BeginDeleteCommand.Execute(entry);
        vm.CancelDeleteCommand.Execute(null);
        var remaining = await _platformService.GetAllPlatformsAsync();
        Assert.Contains(remaining, p => p.Id == cp.Id);
    }

    [Fact]
    public async Task DisplayName_CasePreserved()
    {
        var cp = await _platformService.CreatePlatformAsync("YouTube");
        var vm = CreateVm();
        await vm.LoadCommand.ExecuteAsync(null);
        var entry = vm.Entries.First(e => e.CustomPlatformId == cp.Id);
        vm.BeginEditCommand.Execute(entry);
        vm.PlatformName = "yOuTuBe Pro";
        await vm.SaveCommand.ExecuteAsync(null);
        var updated = await _platformService.GetAllPlatformsAsync();
        Assert.Contains(updated, p => p.Id == cp.Id && p.Name == "yOuTuBe Pro");
    }

    [Fact]
    public async Task DatabaseCaseDuplicate_DoesNotCrash()
    {
        await _platformService.CreatePlatformAsync("YouTube");
        using (var cmd = _connection.CreateCommand())
        {
            cmd.CommandText = "INSERT INTO custom_platforms (id, name, created_at, sort_order) VALUES ($id, $name, $ts, 0)";
            cmd.Parameters.AddWithValue("$id", Guid.NewGuid().ToString("D"));
            cmd.Parameters.AddWithValue("$name", "youtube");
            cmd.Parameters.AddWithValue("$ts", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            cmd.ExecuteNonQuery();
        }
        var vm = CreateVm();
        await vm.LoadCommand.ExecuteAsync(null);
        Assert.Equal(2, vm.Entries.Count);
    }

    [Fact]
    public async Task SidebarAndManagement_SameCustomPlatforms()
    {
        await _platformService.CreatePlatformAsync("YouTube");
        await _platformService.CreatePlatformAsync("工作");
        var vm = CreateVm();
        await vm.LoadCommand.ExecuteAsync(null);
        Assert.Equal(2, vm.Entries.Count);
    }
}
