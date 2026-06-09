using Avalonia.Controls;
using Avalonia.Interactivity;
using Gatherly.Windows.ViewModels;

namespace Gatherly.Windows.Views;

public partial class HomeView : UserControl
{
    public HomeView()
    {
        InitializeComponent();
    }

    private void PlatformEntry_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.DataContext is PlatformEntryDisplay entry)
        {
            // 暂不实现平台导航，后续 Phase 补齐
        }
    }
}
