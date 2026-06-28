using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Gatherly.Windows.Models;
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
        if (sender is Button button && button.DataContext is PlatformEntryDisplay entry
            && DataContext is HomeViewModel vm)
        {
            vm.NotifyPlatformEntryClicked(entry);
        }
    }

    private void RecentItemCard_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is not HomeViewModel vm) return;
        if (sender is not Border border) return;
        if (border.DataContext is not Item item) return;
        if (!e.GetCurrentPoint(border).Properties.IsLeftButtonPressed) return;

        vm.OnItemSelected?.Invoke(item);
    }

    private void SearchResultCard_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is not HomeViewModel vm) return;
        if (sender is not Border border) return;
        if (border.DataContext is not Item item) return;
        if (!e.GetCurrentPoint(border).Properties.IsLeftButtonPressed) return;

        vm.OnItemSelected?.Invoke(item);
    }

    private void HideRecentItem_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi && mi.Tag is Item item && DataContext is HomeViewModel vm)
        {
            vm.OnHideItemRequested?.Invoke(item);
        }
    }

    private void DeleteRecentItem_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi && mi.Tag is Item item && DataContext is HomeViewModel vm)
        {
            vm.OnDeleteItemRequested?.Invoke(item);
        }
    }

    private void SearchResultMoveToPlatform_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi && mi.Tag is Item item && DataContext is HomeViewModel vm)
            vm.OnMoveToPlatformRequested?.Invoke(item);
    }

    private void SearchResultMoveToFolder_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi && mi.Tag is Item item && DataContext is HomeViewModel vm)
            vm.OnMoveToFolderRequested?.Invoke(item);
    }

    private void SearchResultDelete_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi && mi.Tag is Item item && DataContext is HomeViewModel vm)
            vm.OnDeleteItemRequested?.Invoke(item);
    }
}
