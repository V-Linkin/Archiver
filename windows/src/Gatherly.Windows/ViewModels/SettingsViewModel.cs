using CommunityToolkit.Mvvm.ComponentModel;

namespace Gatherly.Windows.ViewModels;

/// <summary>
/// 设置页面 ViewModel
/// </summary>
public partial class SettingsViewModel : ObservableObject
{
    public BackupPackageViewModel BackupVM { get; }

    public SettingsViewModel(BackupPackageViewModel backupVM)
    {
        BackupVM = backupVM;
    }
}
