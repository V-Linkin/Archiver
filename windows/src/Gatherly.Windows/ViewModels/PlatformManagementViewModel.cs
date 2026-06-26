using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Gatherly.Windows.Database;
using Gatherly.Windows.Models;
using Gatherly.Windows.Models.Enums;
using Gatherly.Windows.Services;

namespace Gatherly.Windows.ViewModels;

public partial class PlatformManagementViewModel : ObservableObject
{
    private readonly CustomPlatformService _platformService;
    private readonly ItemRepository _itemRepo;

    public ObservableCollection<PlatformManagementEntry> Entries { get; } = new();

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _isCreating;
    [ObservableProperty] private bool _isEditing;
    [ObservableProperty] private bool _isSaving;
    [ObservableProperty] private PlatformManagementEntry? _editingEntry;
    [ObservableProperty] private string _platformName = string.Empty;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private bool _isConfirmingDelete;
    [ObservableProperty] private PlatformManagementEntry? _deletingEntry;
    [ObservableProperty] private string? _deleteConfirmMessage;

    public bool IsEmpty => Entries.Count == 0 && !IsLoading;
    public bool CanSave => !IsSaving && !string.IsNullOrWhiteSpace(PlatformName);

    partial void OnPlatformNameChanged(string value) => OnPropertyChanged(nameof(CanSave));

    public Func<Task>? OnPlatformChanged { get; set; }

    public PlatformManagementViewModel(CustomPlatformService platformService, ItemRepository itemRepo)
    {
        _platformService = platformService;
        _itemRepo = itemRepo;
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        if (IsLoading) return;
        IsLoading = true;
        ErrorMessage = null;
        try { await BuildEntriesAsync(); OnPropertyChanged(nameof(IsEmpty)); }
        catch (Exception ex) { ErrorMessage = $"加载平台列表失败：{ex.Message}"; }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private void BeginCreate()
    {
        IsCreating = true; IsEditing = false; EditingEntry = null;
        PlatformName = string.Empty; ErrorMessage = null;
    }

    [RelayCommand]
    private void CancelEdit()
    {
        IsCreating = false; IsEditing = false; EditingEntry = null;
        PlatformName = string.Empty; ErrorMessage = null;
    }

    [RelayCommand]
    private void BeginEdit(PlatformManagementEntry? entry)
    {
        if (entry == null) return;
        IsEditing = true; IsCreating = false; EditingEntry = entry;
        PlatformName = entry.DisplayName; ErrorMessage = null;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (IsSaving || string.IsNullOrWhiteSpace(PlatformName)) return;
        IsSaving = true; ErrorMessage = null;
        try
        {
            if (IsEditing && EditingEntry?.CustomPlatformId.HasValue == true)
            {
                var updated = await _platformService.UpdatePlatformAsync(
                    EditingEntry.CustomPlatformId.Value, name: PlatformName);
                if (updated == null) { ErrorMessage = "平台不存在或更新失败"; return; }
            }
            else
            {
                await _platformService.CreatePlatformAsync(PlatformName);
            }
            await BuildEntriesAsync();
            IsCreating = false; IsEditing = false; EditingEntry = null;
            PlatformName = string.Empty; ErrorMessage = null;
            if (OnPlatformChanged != null) await OnPlatformChanged();
        }
        catch (ArgumentException ex) { ErrorMessage = ex.Message; }
        catch (Exception ex) { ErrorMessage = $"操作失败：{ex.Message}"; }
        finally { IsSaving = false; }
    }

    [RelayCommand]
    private async Task BeginDelete(PlatformManagementEntry? entry)
    {
        if (entry?.CustomPlatformId == null) return;
        var itemCount = await _itemRepo.CountByCustomPlatformIdAsync(entry.CustomPlatformId.Value);
        DeletingEntry = entry; IsConfirmingDelete = true;
        DeleteConfirmMessage = itemCount > 0
            ? $"平台「{entry.DisplayName}」仍有 {itemCount} 条内容。删除平台后，这些内容将移动到「未分类内容」。确定继续吗？"
            : $"确定删除平台「{entry.DisplayName}」吗？";
    }

    [RelayCommand]
    private async Task ConfirmDelete()
    {
        if (DeletingEntry?.CustomPlatformId == null) return;
        try
        {
            await _platformService.DeletePlatformAsync(DeletingEntry.CustomPlatformId.Value);
            IsConfirmingDelete = false; DeletingEntry = null; DeleteConfirmMessage = null;
            await BuildEntriesAsync();
            if (OnPlatformChanged != null) await OnPlatformChanged();
        }
        catch (Exception ex) { ErrorMessage = $"删除失败：{ex.Message}"; IsConfirmingDelete = false; DeletingEntry = null; DeleteConfirmMessage = null; }
    }

    [RelayCommand]
    private void CancelDelete()
    {
        IsConfirmingDelete = false; DeletingEntry = null; DeleteConfirmMessage = null;
    }

    private async Task BuildEntriesAsync()
    {
        Entries.Clear();
        var customPlatforms = await _platformService.GetAllPlatformsAsync();
        foreach (var cp in customPlatforms)
        {
            Entries.Add(new PlatformManagementEntry
            {
                Kind = PlatformManagementEntryKind.Custom,
                CustomPlatformId = cp.Id,
                DisplayName = cp.Name
            });
        }
        OnPropertyChanged(nameof(IsEmpty));
    }
}
