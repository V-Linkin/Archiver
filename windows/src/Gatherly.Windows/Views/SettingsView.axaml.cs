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

    private async void RestoreBackup_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel vm)
            await vm.RestoreBackupCommand.ExecuteAsync(null);
    }
}
