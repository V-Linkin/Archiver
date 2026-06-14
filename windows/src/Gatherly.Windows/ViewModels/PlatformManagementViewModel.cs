using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Gatherly.Windows.Models;
using Gatherly.Windows.Services;

namespace Gatherly.Windows.ViewModels;

/// <summary>
/// 平台管理 ViewModel — 显示用户自定义平台列表和创建新平台
/// </summary>
public partial class PlatformManagementViewModel : ObservableObject
{
    private readonly CustomPlatformService _platformService;

    public ObservableCollection<CustomPlatform> Platforms { get; } = new();

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isCreating;

    [ObservableProperty]
    private bool _isSaving;

    [ObservableProperty]
    private string _newPlatformName = string.Empty;

    [ObservableProperty]
    private string? _errorMessage;

    public bool IsEmpty => Platforms.Count == 0 && !IsLoading;

    public bool CanSave => !IsSaving && !string.IsNullOrWhiteSpace(NewPlatformName);

    partial void OnNewPlatformNameChanged(string value)
    {
        OnPropertyChanged(nameof(CanSave));
    }

    /// <summary>
    /// 创建成功后的回调（刷新 Sidebar）
    /// </summary>
    public Func<Task>? OnPlatformCreated { get; set; }

    public PlatformManagementViewModel(CustomPlatformService platformService)
    {
        _platformService = platformService;
    }

    /// <summary>
    /// 加载用户自定义平台列表
    /// </summary>
    [RelayCommand]
    public async Task LoadAsync()
    {
        if (IsLoading) return;

        IsLoading = true;
        ErrorMessage = null;

        try
        {
            var platforms = await _platformService.GetAllPlatformsAsync();
            Platforms.Clear();
            foreach (var p in platforms)
            {
                Platforms.Add(p);
            }
            OnPropertyChanged(nameof(IsEmpty));
        }
        catch (Exception ex)
        {
            ErrorMessage = $"加载平台列表失败：{ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// 进入创建模式
    /// </summary>
    [RelayCommand]
    private void BeginCreate()
    {
        IsCreating = true;
        NewPlatformName = string.Empty;
        ErrorMessage = null;
    }

    /// <summary>
    /// 取消创建
    /// </summary>
    [RelayCommand]
    private void CancelCreate()
    {
        IsCreating = false;
        NewPlatformName = string.Empty;
        ErrorMessage = null;
    }

    /// <summary>
    /// 保存新平台
    /// </summary>
    [RelayCommand]
    private async Task SaveCreateAsync()
    {
        if (IsSaving || string.IsNullOrWhiteSpace(NewPlatformName))
        {
            System.Diagnostics.Debug.WriteLine($"[SaveCreate] Blocked: IsSaving={IsSaving}, Name='{NewPlatformName}'");
            return;
        }

        IsSaving = true;
        ErrorMessage = null;

        try
        {
            System.Diagnostics.Debug.WriteLine($"[SaveCreate] Creating: '{NewPlatformName}'");
            var platform = await _platformService.CreatePlatformAsync(NewPlatformName);
            System.Diagnostics.Debug.WriteLine($"[SaveCreate] Created: id={platform.Id}, name={platform.Name}");

            // 从数据库重新加载列表，确保一致性
            System.Diagnostics.Debug.WriteLine($"[SaveCreate] Reloading platforms...");
            await ReloadPlatformsAsync();
            System.Diagnostics.Debug.WriteLine($"[SaveCreate] Reloaded: Platforms.Count={Platforms.Count}");

            // 清空创建状态
            IsCreating = false;
            NewPlatformName = string.Empty;
            ErrorMessage = null;

            // 通知刷新 Sidebar
            if (OnPlatformCreated != null)
            {
                System.Diagnostics.Debug.WriteLine($"[SaveCreate] Calling OnPlatformCreated...");
                await OnPlatformCreated();
            }
            System.Diagnostics.Debug.WriteLine($"[SaveCreate] Done");
        }
        catch (ArgumentException ex)
        {
            ErrorMessage = ex.Message;
            System.Diagnostics.Debug.WriteLine($"[SaveCreate] Validation error: {ex.Message}");
        }
        catch (Exception ex)
        {
            ErrorMessage = $"创建平台失败：{ex.Message}";
            System.Diagnostics.Debug.WriteLine($"[SaveCreate] Error: {ex}");
        }
        finally
        {
            IsSaving = false;
        }
    }

    /// <summary>
    /// 从数据库重新加载平台列表
    /// </summary>
    private async Task ReloadPlatformsAsync()
    {
        var platforms = await _platformService.GetAllPlatformsAsync();
        Platforms.Clear();
        foreach (var p in platforms)
        {
            Platforms.Add(p);
        }
        OnPropertyChanged(nameof(IsEmpty));
    }
}
