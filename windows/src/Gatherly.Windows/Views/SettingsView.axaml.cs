using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.Interactivity;
using Gatherly.Windows.ViewModels;

namespace Gatherly.Windows.Views;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel vm)
        {
            BrowserComboBox.ItemsSource = vm.AvailableBrowsers;
            BrowserComboBox.SelectedIndex = vm.SelectedBrowserIndex;
        }
    }

    private async void CreateBackup_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not SettingsViewModel settingsVm) return;
        var backupVm = settingsVm.BackupVM;
        if (backupVm.IsBackupRunning) return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.StorageProvider == null) return;

        var suggestedName = BackupPackageViewModel.CreateSuggestedBackupFileName(DateTimeOffset.Now);

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
            backupVm.BackupStatusMessage = "请选择本地文件位置";
            return;
        }

        await backupVm.CreateBackupCommand.ExecuteAsync(path);
    }

    private void OpenDataDirectory_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel vm)
            vm.OpenDataDirectoryCommand.Execute(null);
    }

    private async void ModifyDataDirectory_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not SettingsViewModel vm) return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.StorageProvider == null) return;

        var files = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "选择目标数据目录",
            AllowMultiple = false
        });

        if (files == null || files.Count == 0)
        {
            ClearMigrationPlan(vm);
            vm.DataDirStatus = "已取消选择，未更改数据目录";
            vm.HasDataDirStatus = true;
            return;
        }

        var targetPath = files[0].TryGetLocalPath();
        if (string.IsNullOrEmpty(targetPath))
        {
            ClearMigrationPlan(vm);
            vm.DataDirStatus = "无法获取目标目录路径";
            vm.HasDataDirStatus = true;
            return;
        }

        // 生成 dry-run 迁移计划
        var plan = Gatherly.Windows.Database.DataDirectoryMigrationPlan.Generate(targetPath);

        if (plan.IsSameDirectory)
        {
            ClearMigrationPlan(vm);
            vm.DataDirStatus = "目标目录与当前目录相同，无需迁移";
            vm.HasDataDirStatus = true;
            return;
        }

        if (plan.Errors.Count > 0)
        {
            ClearMigrationPlan(vm);
            vm.DataDirStatus = $"迁移计划生成失败：{string.Join("；", plan.Errors)}";
            vm.HasDataDirStatus = true;
            return;
        }

        // 显示 dry-run 迁移计划（结构化）
        vm.PlanCurrentDir = plan.CurrentDirectory;
        vm.PlanTargetDir = plan.TargetDirectory;
        vm.PlanDbSize = FormatSize(plan.DbFileSize);
        vm.PlanMediaInfo = $"{plan.MediaFileCount} 个文件，{FormatSize(plan.MediaTotalSize)}";
        vm.PlanEstimate = $"预计复制 {plan.EstimatedFileCount} 个文件，{FormatSize(plan.EstimatedTotalSize)}";

        if (plan.TargetExists && !plan.TargetEmpty)
            vm.PlanConclusion = "存在冲突风险，建议选择空目录";
        else if (plan.IsMigratable)
            vm.PlanConclusion = "可迁移到目标目录";

        vm.PlanWarning = plan.Warnings.Count > 0
            ? string.Join("；", plan.Warnings) + "。当前仅预览迁移计划，暂不执行迁移。"
            : "当前仅预览迁移计划，暂不执行迁移。";

        vm.DataDirStatus = "";
        vm.HasDataDirStatus = false;
        vm.HasMigrationPlan = true;
    }

    private static string FormatSize(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
        < 1024 * 1024 * 1024 => $"{bytes / (1024.0 * 1024):F1} MB",
        _ => $"{bytes / (1024.0 * 1024 * 1024):F1} GB"
    };

    private static void ClearMigrationPlan(SettingsViewModel vm)
    {
        vm.HasMigrationPlan = false;
        vm.PlanCurrentDir = "";
        vm.PlanTargetDir = "";
        vm.PlanDbSize = "";
        vm.PlanMediaInfo = "";
        vm.PlanEstimate = "";
        vm.PlanConclusion = "";
        vm.PlanWarning = "";
    }

    private void ShowHelp_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel vm)
            vm.ShowHelpCommand.Execute(null);
    }

    private void OpenGitHub_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel vm)
            vm.OpenGitHubCommand.Execute(null);
    }

    private void BrowserComboBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is SettingsViewModel vm && BrowserComboBox.SelectedIndex >= 0)
        {
            vm.SelectedBrowserIndex = BrowserComboBox.SelectedIndex;
            vm.SaveBrowserSettings();
        }
    }

    private async void RestoreBackup_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel vm)
            await vm.RestoreBackupCommand.ExecuteAsync(null);
    }
}
