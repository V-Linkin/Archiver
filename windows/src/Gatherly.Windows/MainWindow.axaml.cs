using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Avalonia.Interactivity;
using Gatherly.Windows.Models.Enums;
using Gatherly.Windows.ViewModels;

namespace Gatherly.Windows;

public partial class MainWindow : Window
{
    private MainWindowViewModel? _subscribedVm;

    public MainWindow()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        // Unsubscribe old ViewModel
        if (_subscribedVm != null)
        {
            _subscribedVm.PropertyChanged -= OnVmPropertyChanged;
            _subscribedVm = null;
        }

        // Subscribe new ViewModel
        if (DataContext is MainWindowViewModel vm)
        {
            _subscribedVm = vm;
            vm.PropertyChanged += OnVmPropertyChanged;
            UpdateSectionVisibility(vm.CurrentSection);
        }
    }

    private void OnVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (_subscribedVm != null && e.PropertyName == nameof(_subscribedVm.CurrentSection))
            UpdateSectionVisibility(_subscribedVm.CurrentSection);
    }

    private void UpdateSectionVisibility(string section)
    {
        HomeViewControl.IsVisible = section == "Home";
        SearchViewControl.IsVisible = section == "Search";
        TrashViewControl.IsVisible = section == "Trash";
        ContentListViewControl.IsVisible = section == "PlatformContent";
        DetailViewControl.IsVisible = section == "Detail";
        SettingsViewControl.IsVisible = section == "Settings";

        // Clear selection when returning to list views to prevent stale focus/selection
        if (section == "PlatformContent")
            ContentListViewControl.ClearSelection();
        else if (section == "Search")
            SearchViewControl.ClearSelection();
    }

    private async void PlatformEntry_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;
        if (sender is not Button button) return;

        if (button.DataContext is PlatformEntryDisplay entry)
        {
            if (entry.IsUncategorized)
            {
                await vm.ShowUncategorizedCommand.ExecuteAsync(null);
            }
            else if (entry.IsStandardPlatform && entry.StandardPlatform.HasValue
                     && entry.CustomPlatformIds.Count > 0)
            {
                // 有标准平台且有自定义平台 ID → 合并查询
                await vm.ShowMergedPlatformCommand.ExecuteAsync(entry);
            }
            else if (entry.IsStandardPlatform && entry.StandardPlatform.HasValue
                     && entry.CustomPlatformIds.Count == 0)
            {
                // 仅有标准平台，无自定义平台 ID → 标准查询
                await vm.ShowStandardPlatformCommand.ExecuteAsync(entry.StandardPlatform.Value);
            }
            else if (entry.CustomPlatformIds.Count == 1)
            {
                await vm.ShowCustomPlatformCommand.ExecuteAsync(entry.CustomPlatformIds[0]);
            }
            else
            {
                await vm.ShowCustomPlatformCommand.ExecuteAsync(entry.Id);
            }
        }
    }

    private static bool SupportsMergedPlatform(Platform platform)
    {
        return platform == Platform.youtube || platform == Platform.bilibili;
    }

    private async void ImportBackup_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.StorageProvider == null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "选择备份文件",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("ZIP 备份文件") { Patterns = new[] { "*.zip" } }
            }
        });

        if (files.Count == 0) return;

        var path = files[0].TryGetLocalPath();
        if (string.IsNullOrEmpty(path)) return;

        await vm.ImportBackupAsync(path);
    }

    private async void CreateBackup_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;
        if (vm.BackupVM.IsBackupRunning) return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.StorageProvider == null) return;

        var suggestedName = $"Gatherly-Backup-{DateTime.Now:yyyyMMdd-HHmmss}.zip";

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "创建备份",
            SuggestedFileName = suggestedName,
            DefaultExtension = ".zip",
            FileTypeChoices = new[]
            {
                new FilePickerFileType("Gatherly 备份文件") { Patterns = new[] { "*.zip" } }
            }
        });

        if (file == null) return;

        var path = file.TryGetLocalPath();
        if (string.IsNullOrEmpty(path))
        {
            vm.BackupVM.BackupStatusMessage = "请选择本地文件位置";
            return;
        }

        await vm.BackupVM.CreateBackupCommand.ExecuteAsync(path);
    }
}
