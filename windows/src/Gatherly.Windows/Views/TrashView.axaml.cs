using Avalonia.Controls;
using Avalonia.Interactivity;
using Gatherly.Windows.Models;
using Gatherly.Windows.ViewModels;

namespace Gatherly.Windows.Views;

public partial class TrashView : UserControl
{
    public TrashView()
    {
        InitializeComponent();
    }

    private async void RowRestore_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not Item item) return;

        var dialog = new ConfirmDialogWindow(
            "恢复内容",
            "确定要将此内容恢复到原来的位置吗？",
            "取消", "恢复", isDangerConfirm: false);

        if (TopLevel.GetTopLevel(this) is Window owner)
            await dialog.ShowDialog(owner);

        if (dialog.Result == true && DataContext is TrashViewModel vm)
        {
            vm.SelectedItem = item;
            await vm.RestoreSelectedItemCommand.ExecuteAsync(null);
        }
    }

    private async void RowDelete_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not Item item) return;

        var dialog = new ConfirmDialogWindow(
            "彻底删除",
            "此操作不可恢复，内容及所有媒体文件将被永久删除。",
            "取消", "删除", isDangerConfirm: true);

        if (TopLevel.GetTopLevel(this) is Window owner)
            await dialog.ShowDialog(owner);

        if (dialog.Result == true && DataContext is TrashViewModel vm)
        {
            vm.SelectedItem = item;
            await vm.PermanentlyDeleteSelectedItemCommand.ExecuteAsync(null);
        }
    }

    private async void ClearAll_Click(object? sender, RoutedEventArgs e)
    {
        var dialog = new ConfirmDialogWindow(
            "清空回收站",
            "此操作不可恢复，所有内容将被永久删除。",
            "取消", "彻底删除全部", isDangerConfirm: true);

        if (TopLevel.GetTopLevel(this) is Window owner)
            await dialog.ShowDialog(owner);

        if (dialog.Result == true && DataContext is TrashViewModel vm)
        {
            await vm.ClearAllCommand.ExecuteAsync(null);
        }
    }
}
