using Avalonia.Controls;
using Avalonia.Input;
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

    public void ClearSelection()
    {
        ItemListBox.SelectedItem = null;
    }

    /// <summary>
    /// 平台页：移动到平台 + 移动到文件夹
    /// 文件夹页：移动到文件夹 + 删除
    /// </summary>
    private void UpdateContextMenuVisibility(Border cardBorder, bool isInFolder)
    {
        if (cardBorder.ContextFlyout is MenuFlyout flyout)
        {
            foreach (var item in flyout.Items)
            {
                if (item is MenuItem mi)
                {
                    var header = mi.Header?.ToString();
                    if (header == "移动到平台")
                        mi.IsVisible = !isInFolder;
                    else if (header == "删除")
                        mi.IsVisible = isInFolder;
                }
            }
        }
    }

    private void CardBorder_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is not ContentListViewModel vm) return;
        if (sender is not Border border) return;
        if (border.DataContext is not Item item) return;

        // Update menu visibility based on context
        UpdateContextMenuVisibility(border, vm.IsInFolder);

        // Only navigate on left-click; right-click is handled by ContextFlyout
        if (e.GetCurrentPoint(border).Properties.IsLeftButtonPressed)
        {
            vm.OnItemSelected?.Invoke(item);
        }
    }

    private void MoveToPlatform_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem) return;
        if (menuItem.Tag is not Item item) return;
        if (DataContext is not ContentListViewModel vm) return;

        vm.OnMoveToPlatformRequested?.Invoke(item);
    }

    private void MoveToFolder_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem) return;
        if (menuItem.Tag is not Item item) return;
        if (DataContext is not ContentListViewModel vm) return;

        vm.OnMoveToFolderRequested?.Invoke(item);
    }

    private void DeleteItem_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem) return;
        if (menuItem.Tag is not Item item) return;
        if (DataContext is not ContentListViewModel vm) return;

        vm.OnDeleteItemRequested?.Invoke(item);
    }

    private void NewFolder_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ContentListViewModel vm) return;
        vm.OnNewFolderRequested?.Invoke();
    }

    private void BackButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ContentListViewModel vm) return;
        vm.OnNavigateBackRequested?.Invoke();
    }

    private void FolderTag_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is not ContentListViewModel vm) return;
        if (sender is not Border border) return;
        if (border.Tag is not Folder folder) return;

        // Only left-click enters the folder; right-click is handled by ContextFlyout
        if (e.GetCurrentPoint(border).Properties.IsLeftButtonPressed)
        {
            vm.OnFolderClicked?.Invoke(folder);
        }
    }

    private void FolderRename_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem) return;
        if (menuItem.Tag is not Folder folder) return;
        if (DataContext is not ContentListViewModel vm) return;

        vm.OnFolderRenameRequested?.Invoke(folder);
    }

    private void FolderDelete_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem) return;
        if (menuItem.Tag is not Folder folder) return;
        if (DataContext is not ContentListViewModel vm) return;

        vm.OnFolderDeleteRequested?.Invoke(folder);
    }

    private void SortToggle_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ContentListViewModel vm)
            vm.ToggleSort();
    }

    private void ViewToggle_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ContentListViewModel vm)
            vm.ToggleViewMode();
    }
}
