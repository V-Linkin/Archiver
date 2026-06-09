using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.Interactivity;
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
        DetailViewControl.IsVisible = section == "Detail";
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
}
