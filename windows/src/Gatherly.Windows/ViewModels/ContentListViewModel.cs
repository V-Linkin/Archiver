using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Gatherly.Windows.Models;
using Gatherly.Windows.Models.Enums;
using Gatherly.Windows.Services;

namespace Gatherly.Windows.ViewModels;

/// <summary>
/// 内容列表 ViewModel — 平台 / 文件夹 / 自定义平台 / 未分类
/// </summary>
public partial class ContentListViewModel : ViewModelBase
{
    private readonly ContentListService _contentService;

    public ObservableCollection<Item> Items { get; } = new();
    public ObservableCollection<Folder> Folders { get; } = new();

    [ObservableProperty]
    private Item? _selectedItem;

    public ContentListViewModel(ContentListService contentService)
    {
        _contentService = contentService;
    }

    public async Task LoadPlatformAsync(Platform platform)
    {
        if (IsBusy) return;

        IsBusy = true;
        ClearError();

        try
        {
            var items = await _contentService.GetPlatformItemsAsync(platform);
            var folders = await _contentService.GetPlatformFoldersAsync(platform);

            Items.Clear();
            foreach (var item in items) Items.Add(item);

            Folders.Clear();
            foreach (var folder in folders) Folders.Add(folder);
        }
        catch (Exception ex)
        {
            SetError(ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task LoadFolderAsync(Guid folderId)
    {
        if (IsBusy) return;

        IsBusy = true;
        ClearError();

        try
        {
            var items = await _contentService.GetFolderItemsAsync(folderId);
            var subfolders = await _contentService.GetChildFoldersAsync(folderId);

            Items.Clear();
            foreach (var item in items) Items.Add(item);

            Folders.Clear();
            foreach (var folder in subfolders) Folders.Add(folder);
        }
        catch (Exception ex)
        {
            SetError(ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task LoadCustomPlatformAsync(Guid customPlatformId)
    {
        if (IsBusy) return;

        IsBusy = true;
        ClearError();

        try
        {
            var items = await _contentService.GetCustomPlatformItemsAsync(customPlatformId);
            var folders = await _contentService.GetCustomPlatformFoldersAsync(customPlatformId);

            Items.Clear();
            foreach (var item in items) Items.Add(item);

            Folders.Clear();
            foreach (var folder in folders) Folders.Add(folder);
        }
        catch (Exception ex)
        {
            SetError(ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task LoadUncategorizedAsync()
    {
        if (IsBusy) return;

        IsBusy = true;
        ClearError();

        try
        {
            var items = await _contentService.GetUncategorizedItemsAsync();
            var folders = await _contentService.GetUncategorizedFoldersAsync();

            Items.Clear();
            foreach (var item in items) Items.Add(item);

            Folders.Clear();
            foreach (var folder in folders) Folders.Add(folder);
        }
        catch (Exception ex)
        {
            SetError(ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }
}
