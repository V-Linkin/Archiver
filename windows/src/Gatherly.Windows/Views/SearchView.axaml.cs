using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Gatherly.Windows.Models;
using Gatherly.Windows.ViewModels;

namespace Gatherly.Windows.Views;

public partial class SearchView : UserControl
{
    public SearchView()
    {
        InitializeComponent();
    }

    public void ClearSelection()
    {
        if (Content is Panel panel)
        {
            foreach (var child in panel.Children)
            {
                if (child is ListBox lb)
                {
                    lb.SelectedItem = null;
                    break;
                }
            }
        }
    }

    private async void SearchTextBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && DataContext is SearchViewModel vm)
        {
            await vm.SearchCommand.ExecuteAsync(null);
            e.Handled = true;
        }
    }

    private void SearchResultCard_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is not SearchViewModel vm) return;
        if (sender is not Border border) return;
        if (border.DataContext is not Item item) return;
        if (!e.GetCurrentPoint(border).Properties.IsLeftButtonPressed) return;

        vm.OnItemSelected?.Invoke(item);
    }

    private void SearchResultMoveToPlatform_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi && mi.Tag is Item item && DataContext is SearchViewModel vm)
            vm.OnMoveToPlatformRequested?.Invoke(item);
    }

    private void SearchResultMoveToFolder_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi && mi.Tag is Item item && DataContext is SearchViewModel vm)
            vm.OnMoveToFolderRequested?.Invoke(item);
    }

    private void SearchResultDelete_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi && mi.Tag is Item item && DataContext is SearchViewModel vm)
            vm.OnDeleteItemRequested?.Invoke(item);
    }
}
