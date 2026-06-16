using Avalonia.Controls;
using Avalonia.Interactivity;
using Gatherly.Windows.ViewModels;

namespace Gatherly.Windows.Views;

public partial class PlatformManagementWindow : Window
{
    public PlatformManagementWindow()
    {
        InitializeComponent();
    }

    private void CloseButton_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private void EditButton_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is PlatformManagementEntry entry)
        {
            if (DataContext is PlatformManagementViewModel vm)
            {
                vm.BeginEditCommand.Execute(entry);
            }
        }
    }

    private void DeleteButton_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is PlatformManagementEntry entry)
        {
            if (DataContext is PlatformManagementViewModel vm)
            {
                vm.BeginDeleteCommand.Execute(entry);
            }
        }
    }
}
