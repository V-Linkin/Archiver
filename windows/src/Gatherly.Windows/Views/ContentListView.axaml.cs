using Avalonia.Controls;
using Avalonia.Interactivity;
using Gatherly.Windows.Models;
using Gatherly.Windows.ViewModels;

namespace Gatherly.Windows.Views;

public partial class ContentListView : UserControl
{
    public ContentListView()
    {
        InitializeComponent();
    }

    private void MoveToPlatform_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem) return;
        if (menuItem.Tag is not Item item) return;
        if (DataContext is not ContentListViewModel vm) return;

        vm.OnMoveToPlatformRequested?.Invoke(item);
    }
}
